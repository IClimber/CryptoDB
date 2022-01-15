﻿using CryptoDataBase.CDB.Exceptions;
using CryptoDataBase.CDB.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CDB
{
	class XDB : DirElement, IDisposable
	{
		public const byte CurrentVersion = 5;

		public readonly bool IsReadOnly = true;
		private AesCryptoServiceProvider _aes;
		private HeaderRepository _headerRepository;
		private FileStream _headersFileStream;
		private FileStream _dataFileStream;

		public XDB(string fileName, string password, HeaderRepository.ProgressCallback progress = null)
		{
			progress?.Invoke(0, "Creating AES key");

			AddElementLocker = new object();
			ChangeElementsLocker = new object();

			string dataFilePath = Path.GetDirectoryName(fileName) + "\\" + Path.GetFileNameWithoutExtension(fileName) + ".Data";

			try
			{
				bool writeVersion = !File.Exists(fileName);
				_headersFileStream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
				_dataFileStream = new FileStream(dataFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
				if (writeVersion)
				{
					_headersFileStream.WriteByte(CurrentVersion);
				}
				IsReadOnly = false;
			}
			catch
			{
				if (_headersFileStream != null)
				{
					_headersFileStream.Close();
				}

				_headersFileStream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
				_dataFileStream = new FileStream(dataFilePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
				IsReadOnly = true;
			}

			byte version = ReadVersion(_headersFileStream);

			try
			{
				_headerRepository = HeaderRepositoryFactory.GetRepositoryByVersion(version, _headersFileStream, password);
			}
			catch (Exception exception)
			{
				_headersFileStream.Close();
				_dataFileStream.Close();

				throw exception;
			}

			_aes = _headerRepository.GetDek();
			DataRepository = new DataRepository(_dataFileStream, _aes);
			Header = new Header(_headerRepository, ElementType.Dir);

			try
			{
				ReadFileStruct(progress);
			}
			catch (Exception)
			{
				throw new ReadingDataException("Wrong password");
			}
		}

		public void ExportStructToFile(string fileName, string password)
		{
			var stream = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
			var repository = HeaderRepositoryFactory.GetRepositoryByVersion(CurrentVersion, stream, password, _aes.Key);
			List<Element> allElements = new List<Element>();
			AddElementsToList(Elements, allElements);
			allElements.Sort(new TimeComparer());
			repository.ExportStructToFile(allElements);
			allElements.Clear();
			stream.Close();
		}

		public bool CanChangePassword()
		{
			return _headerRepository.CanChangePassword();
		}

		public void ChangePassword(string newPassword)
		{
			_headerRepository.ChangePassword(newPassword);
		}

		private void AddElementsToList(IList<Element> inputElementsList, List<Element> outputElementsList)
		{
			outputElementsList.AddRange(inputElementsList);

			foreach (Element element in inputElementsList)
			{
				if (element is DirElement)
				{
					AddElementsToList((element as DirElement).Elements, outputElementsList);
				}
			}
		}

		private byte ReadVersion(Stream stream)
		{
			stream.Position = 0;
			byte[] buf = new byte[1];
			stream.Read(buf, 0, 1);

			return buf[0];
		}

		private void ReadFileStruct(HeaderRepository.ProgressCallback progress)
		{
			List<DirElement> dirs = new List<DirElement>();
			List<Element> elements = new List<Element>();
			dirs.Add(this);

			List<Header> headers = _headerRepository.ReadFileStruct(progress);
			int index = 0;
			double percent = 0;
			int lastProgress = 0;
			foreach (Header header in headers)
			{
				AddElementByHeader(dirs, elements, header);
				index++;

				percent = index / (double)headers.Count * 100.0;
				if ((progress != null) && (lastProgress != (int)percent))
				{
					progress(percent, "Parsing elements");
					lastProgress = (int)percent;
				}
			}

			DataRepository.FreeSpaceAnalyse();

			FillParents(dirs, elements, progress);
			elements.Clear();
			dirs.Clear();
		}

		private void AddElementByHeader(List<DirElement> dirsList, List<Element> elementList, Header header)
		{
			Element element = null;
			if (header.ElementType == ElementType.File)
			{
				element = new FileElement(header, DataRepository, AddElementLocker, ChangeElementsLocker);
			}
			else if (header.ElementType == ElementType.Dir)
			{
				element = new DirElement(header, DataRepository, AddElementLocker, ChangeElementsLocker);
			}

			elementList.Add(element);
			if (element is DirElement)
			{
				dirsList.Add(element as DirElement);
			}

			if ((element is FileElement) && ((element as FileElement).Size > 0))
			{
				DataRepository.RemoveFreeSpace((element as FileElement).FileStartPos, Crypto.GetMod16((element as FileElement).Size));
			}

			if (element.IconSize > 0)
			{
				DataRepository.RemoveFreeSpace(element.IconStartPosition, Crypto.GetMod16(element.IconSize));
			}
		}

		private void FillParents(List<DirElement> dirsList, List<Element> elementList, HeaderRepository.ProgressCallback progress)
		{
			dirsList.Sort(new IDComparer());
			int index = 0;
			int count = elementList.Count;
			int lastProgress = 0;
			foreach (var element in elementList)
			{
				DirElement parent = FindParentByID(dirsList, element.ParentId);
				try
				{
					element.SetVirtualParent(parent != null ? parent : this);
				}
				catch
				{ }
				index++;

				double percent = index / (double)count * 100.0;
				if ((progress != null) && (lastProgress != (int)percent))
				{
					progress(percent, "Creating elements structure");
					lastProgress = (int)percent;
				}
			}
		}

		//Шукає в сортованому по ID списку
		private DirElement FindParentByID(List<DirElement> dirs, ulong parentId)
		{
			var dir = new DirElement(parentId);
			int index = dirs.BinarySearch(dir, new IDComparer());

			return index >= 0 ? dirs[index] : null;
		}

		public void Dispose()
		{
			DataRepository.Dispose();
			_headerRepository.Dispose();
		}
	}
}