using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase
{
	class XDB : Element
	{
		public delegate void ProgressCallback(double percent, string message);

		byte Version = 3;
		AesCryptoServiceProvider AES = new AesCryptoServiceProvider();
		FileStream _headersFileStream;
		FileStream _dataFileStream;

		public XDB(string FileName, string Password, ProgressCallback Progress = null) : base(true)
		{
			Progress?.Invoke(0, "Creating AES key");
			InitKey(Password);
			head = new Head();
			head.AES = AES;

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

			headersFileStream = new SafeStreamAccess(_headersFileStream);
			dataFileStream = new SafeStreamAccess(_dataFileStream);

			ReadFileStruct(Progress);
		}

		private void ReadFileStruct(ProgressCallback Progress)
		{
			List<Element> dirs = new List<Element>(); //Потім зробити приватним
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
			headers.Position = 1;

			int lastProgress = 0;

			while (headers.Position < headers.Length) //Читаємо список файлів з пам’яті
			{
				Head head = new Head(headers, (UInt64)headers.Position, AES);
				if (head.Exists)
				{
					AddElement(dirs, elements, new Element(headers, headersFileStream, dataFileStream, head));
				}
				else
				{
					headers.Position += head.InfSize;
				}

				percent = headers.Position / (double)headers.Length * 100.0;
				if ((Progress != null) && (lastProgress != (int)percent))
				{
					Progress(percent, "Parse elements");
					lastProgress = (int)percent;
				}
			}

			headers.Dispose();

			FillParents(dirs, elements);
			elements.Clear();
			elements = null;
			dirs.Clear();
			dirs = null;
			FreeSpaceAnalyse();
		}

		private void AddElement(List<Element> DirsList, List<Element> elementList, Element element)
		{
			elementList.Add(element);
			if (element.Type == ElementType.Dir)
			{
				DirsList.Add(element);
			}

			if ((element.Type == ElementType.File) && (element.Size > 0))
			{
				FreeSpaceMap.Add(new SPoint(element.FileStartPos, Element.GetMod16(element.Size)));
			}

			if (element.IconSize > 0)
			{
				FreeSpaceMap.Add(new SPoint(element.IconStartPos, Element.GetMod16(element.IconSize)));
			}
		}

		private void FreeSpaceAnalyse()
		{
			FreeSpaceMap.Sort(new PosComparer());
			int count = 0;
			UInt64 start = 0, size = 0;

			if (FreeSpaceMap.Count == 0)
			{
				if (_dataFileStream.Length > 0)
				{
					FreeSpaceMap.Add(new SPoint(0, (UInt64)_dataFileStream.Length));
				}
				return;
			}

			if (FreeSpaceMap[0].Start > 0)
			{
				FreeSpaceMap.Insert(0, new SPoint(0, FreeSpaceMap[0].Start));
				count++;
			}

			for (int i = count; i < FreeSpaceMap.Count - 1; i++)
			{
				if ((FreeSpaceMap[i].Start + FreeSpaceMap[i].Size) < FreeSpaceMap[i + 1].Start)
				{
					start = (FreeSpaceMap[i].Start + FreeSpaceMap[i].Size);
					size = FreeSpaceMap[i + 1].Start - start;
					FreeSpaceMap[count] = new SPoint(start, size);
					count++;
				}
			}

			if ((long)(FreeSpaceMap[FreeSpaceMap.Count - 1].Start + FreeSpaceMap[FreeSpaceMap.Count - 1].Size) < _dataFileStream.Length)
			{
				start = FreeSpaceMap[FreeSpaceMap.Count - 1].Start + FreeSpaceMap[FreeSpaceMap.Count - 1].Size;
				size = (UInt64)_dataFileStream.Length - start;
				FreeSpaceMap[count] = new SPoint(start, size);
				count++;
			}

			FreeSpaceMap.RemoveRange(count, FreeSpaceMap.Count - count);
			FreeSpaceMap.Sort(new PSizeComparer());
		}

		private void FillParents(List<Element> dirsList, List<Element> elementList)
		{
			dirsList.Sort(new IDComparer());
			foreach (var element in elementList)
			{
				Element parent = FindParentByID(dirsList, element.ParentID);
				if (parent != null)
				{
					element.LocalParent = parent;
				}
				else
				{
					element.LocalParent = this;
				}
			}
		}

		private Element FindParentByID(List<Element> dirs, UInt64 ParentID) //Шукає в сортованому по ID списку
		{
			var dir = new Element(ParentID);
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