using CryptoDataBase.CDB.Exceptions;
using CryptoDataBase.CDB.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoDataBase.CDB
{
	class XDB : DirElement, IDisposable
	{
		const byte CURRENT_VERSION = 5;

		AesCryptoServiceProvider AES;
		HeaderRepository headerRepository;
		FileStream _headersFileStream;
		FileStream _dataFileStream;
		public readonly bool IsReadOnly = true;

		public XDB(string FileName, string Password, HeaderRepository.ProgressCallback Progress = null)
		{
			Progress?.Invoke(0, "Creating AES key");

			_addElementLocker = new Object();
			_changeElementsLocker = new Object();

			string DataFilename = Path.GetDirectoryName(FileName) + "\\" + Path.GetFileNameWithoutExtension(FileName) + ".Data";

			try
			{
				bool writeVersion = !File.Exists(FileName);
				_headersFileStream = new FileStream(FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
				_dataFileStream = new FileStream(DataFilename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
				if (writeVersion)
				{
					_headersFileStream.WriteByte(CURRENT_VERSION);
				}
				IsReadOnly = false;
			}
			catch
			{
				if (_headersFileStream != null)
				{
					_headersFileStream.Close();
				}

				_headersFileStream = new FileStream(FileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
				_dataFileStream = new FileStream(DataFilename, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
				IsReadOnly = true;
			}

			byte version = ReadVersion(_headersFileStream);

			try
			{
				headerRepository = HeaderRepositoryFactory.GetRepositoryByVersion(version, _headersFileStream, Password);
			}
			catch (Exception exception)
			{
				_headersFileStream.Close();
				_dataFileStream.Close();

				throw exception;
			}

			AES = headerRepository.GetDek();
			dataRepository = new DataRepository(_dataFileStream, AES);
			header = new Header(headerRepository, ElementType.Dir);

			try
			{
				ReadFileStruct(Progress);
			}
			catch (Exception)
			{
				throw new ReadingDataException("Помилка читання даних. Можливо невірний пароль.");
			}
		}

		public void ExportStructToFile(string FileName, string password)
		{
			var stream = new FileStream(FileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
			var repository = HeaderRepositoryFactory.GetRepositoryByVersion(CURRENT_VERSION, stream, password, AES.Key);
			List<Element> allElements = new List<Element>();
			AddElementsToList(Elements, allElements);
			allElements.Sort(new TimeComparer());
			repository.ExportStructToFile(allElements);
			allElements.Clear();
			stream.Close();
		}

		public bool CanChangePassword()
		{
			return headerRepository.CanChangePassword();
		}

		public void ChangePassword(string newPassword)
		{
			headerRepository.ChangePassword(newPassword);
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

		private void ReadFileStruct(HeaderRepository.ProgressCallback Progress)
		{
			List<DirElement> dirs = new List<DirElement>(); //Потім зробити приватним
			List<Element> elements = new List<Element>();
			dirs.Add(this);

			List<Header> headers = headerRepository.ReadFileStruct(Progress);
			int index = 0;
			double percent = 0;
			int lastProgress = 0;
			foreach (Header header in headers)
			{
				AddElementByHeader(dirs, elements, header);
				index++;

				percent = index / (double)headers.Count * 100.0;
				if ((Progress != null) && (lastProgress != (int)percent))
				{
					Progress(percent, "Parsing elements");
					lastProgress = (int)percent;
				}
			}

			dataRepository.FreeSpaceAnalyse();

			FillParents(dirs, elements, Progress);
			elements.Clear();
			dirs.Clear();
		}

		private void AddElementByHeader(List<DirElement> DirsList, List<Element> elementList, Header header)
		{
			Element element = null;
			if (header.ElType == ElementType.File)
			{
				element = new FileElement(header, dataRepository, _addElementLocker, _changeElementsLocker);
			}
			else if (header.ElType == ElementType.Dir)
			{
				element = new DirElement(header, dataRepository, _addElementLocker, _changeElementsLocker);
			}

			elementList.Add(element);
			if (element is DirElement)
			{
				DirsList.Add(element as DirElement);
			}

			if ((element is FileElement) && ((element as FileElement).Size > 0))
			{
				dataRepository.RemoveFreeSpace((element as FileElement).FileStartPos, Crypto.GetMod16((element as FileElement).Size));
			}

			if (element.IconSize > 0)
			{
				dataRepository.RemoveFreeSpace(element.IconStartPos, Crypto.GetMod16(element.IconSize));
			}
		}

		private void FillParents(List<DirElement> dirsList, List<Element> elementList, HeaderRepository.ProgressCallback Progress)
		{
			dirsList.Sort(new IDComparer());
			int index = 0;
			int count = elementList.Count;
			double percent = 0;
			int lastProgress = 0;
			foreach (var element in elementList)
			{
				DirElement parent = FindParentByID(dirsList, element.ParentID);
				try
				{
					element.SetVirtualParent(parent != null ? parent : this);
				}
				catch
				{ }
				index++;

				percent = index / (double)count * 100.0;
				if ((Progress != null) && (lastProgress != (int)percent))
				{
					Progress(percent, "Creating elements structure");
					lastProgress = (int)percent;
				}
			}
		}

		//Шукає в сортованому по ID списку
		private DirElement FindParentByID(List<DirElement> dirs, UInt64 ParentID)
		{
			var dir = new DirElement(ParentID);
			int index = dirs.BinarySearch(dir, new IDComparer());

			return index >= 0 ? dirs[index] : null;
		}

		public void Dispose()
		{
			dataRepository.Dispose();
			headerRepository.Dispose();
		}
	}
}