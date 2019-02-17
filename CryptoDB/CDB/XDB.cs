using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CDB
{
	class XDB : DirElement
	{
		public delegate void ProgressCallback(double percent, string message);

		byte Version = 3;
		AesCryptoServiceProvider AES = new AesCryptoServiceProvider();
		FileStream _headersFileStream;
		FileStream _dataFileStream;

		public XDB(string FileName, string Password, ProgressCallback Progress = null)
		{
			Progress?.Invoke(0, "Creating AES key");
			InitKey(Password);

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
					_headersFileStream.WriteByte(Version);
				}
			}
			catch
			{
				if (_headersFileStream != null)
				{
					_headersFileStream.Close();
				}

				_headersFileStream = new FileStream(FileName, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
				_dataFileStream = new FileStream(DataFilename, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
			}

			header = new Header(new SafeStreamAccess(_headersFileStream), AES, ElementType.Dir);

			dataFileStream = new SafeStreamAccess(_dataFileStream);

			ReadFileStruct(Progress);
		}

		private void ReadVersion(Stream stream)
		{
			stream.Position = 0;
			byte[] buf = new byte[1];
			stream.Read(buf, 0, 1);
			Version = buf[0];
		}

		private void ReadFileStruct(ProgressCallback Progress)
		{
			List<DirElement> dirs = new List<DirElement>(); //Потім зробити приватним
			List<Element> elements = new List<Element>();
			dirs.Add(this);

			_headersFileStream.Position = 0;
			byte[] buf = new byte[1048576];
			double percent = 0;

			MemoryStream headers = new MemoryStream();
			while (_headersFileStream.Position < _headersFileStream.Length) //Читаємо список файлів з диску в пам’ять
			{
				int count = _headersFileStream.Read(buf, 0, buf.Length);
				headers.Write(buf, 0, count);

				Progress?.Invoke(_headersFileStream.Position / (double)_headersFileStream.Length * 100.0, "Read file list");
			}
			//headers.Position = 1;
			ReadVersion(headers);

			int lastProgress = 0;

			while (headers.Position < headers.Length) //Читаємо список файлів з пам’яті
			{
				Element element = GetNextElementFromStream(headers);
				if (element != null)
				{
					AddElement(dirs, elements, element);
				}

				percent = headers.Position / (double)headers.Length * 100.0;
				if ((Progress != null) && (lastProgress != (int)percent))
				{
					Progress(percent, "Parse elements");
					lastProgress = (int)percent;
				}
			}

			dataFileStream.FreeSpaceAnalyse();
			headers.Dispose();

			FillParents(dirs, elements);
			elements.Clear();
			elements = null;
			dirs.Clear();
			dirs = null;
		}

		private Element GetNextElementFromStream(Stream stream)
		{
			Header header = new Header(stream, (UInt64)stream.Position, this.header.headersFileStream, AES);

			if (header.Exists)
			{
				if (header.ElType == ElementType.File)
				{
					return new FileElement(header, dataFileStream, _addElementLocker, _changeElementsLocker);
				}
				else if (header.ElType == ElementType.Dir)
				{
					return new DirElement(header, dataFileStream, _addElementLocker, _changeElementsLocker);
				}
			}
			else
			{
				stream.Position += header.InfSize;
			}

			return null;
		}

		private void AddElement(List<DirElement> DirsList, List<Element> elementList, Element element)
		{
			elementList.Add(element);
			if (element is DirElement)
			{
				DirsList.Add(element as DirElement);
			}

			if ((element is FileElement) && ((element as FileElement).Size > 0))
			{
				dataFileStream.RemoveFreeSpace((element as FileElement).FileStartPos, Crypto.GetMod16((element as FileElement).Size));
			}

			if (element.IconSize > 0)
			{
				dataFileStream.RemoveFreeSpace(element.IconStartPos, Crypto.GetMod16(element.IconSize));
			}
		}

		private void FillParents(List<DirElement> dirsList, List<Element> elementList)
		{
			dirsList.Sort(new IDComparer());
			foreach (var element in elementList)
			{
				DirElement parent = FindParentByID(dirsList, element.ParentID);
				try
				{
					element.Parent = parent != null ? parent : this;
				}
				catch
				{

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

		private void InitKey(string Password)
		{
			SHA256 hash = SHA256.Create();
			byte[] salt = hash.ComputeHash(Encoding.UTF8.GetBytes(Password));
			for (int i = 0; i < 50000; i++)
			{
				salt = hash.ComputeHash(salt);
			}

			var key = new Rfc2898DeriveBytes(Password, salt, 100000);

			AES.KeySize = 256;
			AES.BlockSize = 128;
			AES.Key = key.GetBytes(AES.KeySize / 8);
			AES.Mode = CipherMode.CBC;
		}
	}
}