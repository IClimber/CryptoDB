﻿using CryptoDataBase.CDB.Exceptions;
using CryptoDataBase.CDB.Repositories;
using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoDataBase.CDB
{
	public class FileElement : Element
	{
		public const int RawInfLength = 63;
		public override ElementType Type { get { return ElementType.File; } }
		public override UInt64 Size { get { return GetSize(); } }
		public override UInt64 FullSize { get { return GetFullSize(); } }
		public override UInt64 FullEncryptSize { get { return GetFullEncryptSize(); } }
		public byte[] Hash { get { return _Hash; } }
		public bool IsCompressed { get { return _IsCompressed; } }
		public UInt64 FileStartPos { get { return _FileStartPos; } }
		public override DirElement Parent { get { return _Parent; } set { ChangeParent(value); } }

		private byte[] _FileIV { get { return __FileIV == null ? (__FileIV = Crypto.GetMD5(_IconIV)) : __FileIV; } set { __FileIV = value; } }
		private byte[] __FileIV;
		private UInt64 _FileSize;
		private byte[] _Hash;
		private bool _IsCompressed;
		private UInt64 _FileStartPos;

		//Створення файлу при читані з файлу
		public FileElement(Header header, DataRepository dataRepository, Object addElementLocker, Object changeElementsLocker) : base(header, dataRepository, addElementLocker, changeElementsLocker)
		{
			byte[] buf = header.GetInfoBuf();

			ReadElementParamsFromBuffer(buf);

			buf = null;
		}

		//Створення файлу вручну
		public FileElement(DirElement parent, Header parentHeader, DataRepository dataRepository, string Name, Stream fileStream, bool isCompressed,
			Object addElementLocker, Object changeElementsLocker, Bitmap Icon = null, SafeStreamAccess.ProgressCallback Progress = null) : base(addElementLocker, changeElementsLocker)
		{
			lock (_addElementLocker)
			{
				UInt64 fileSize = (UInt64)fileStream.Length;

				byte[] icon = GetIconBytes(Icon);
				UInt32 iconSize = icon == null ? 0 : (UInt32)icon.Length;

				UInt64 fileStartPos = dataRepository.GetFreeSpaceStartPos(Crypto.GetMod16(fileSize)); //Вибираємо місце куди писати файл
				UInt64 iconStartPos = dataRepository.GetFreeSpaceStartPos(Crypto.GetMod16(iconSize)); //Вибираємо місце куди писати іконку
				iconStartPos = (iconStartPos == fileStartPos) ? iconStartPos += Crypto.GetMod16(fileSize) : iconStartPos;

				lock (dataRepository.writeLock)
				{
					header = new Header(parentHeader.repository, ElementType.File);
					this.dataRepository = dataRepository;

					_Name = Name;
					_ParentID = parent.ID;
					_FileStartPos = fileStartPos;
					_FileSize = fileSize;
					_IconStartPos = iconStartPos;
					_IconSize = iconSize;
					_IsCompressed = isCompressed;
					_Hash = new byte[16];
					CryptoRandom.GetBytes(_Hash);
					_PHash = GetPHash(Icon);

					SaveInf();
					_Exists = false;
				}

				if (fileSize > 0)
				{
					dataRepository.WriteEncrypt((long)fileStartPos, fileStream, _FileIV, out _Hash, Progress);
				}

				if ((icon != null) && (iconSize > 0))
				{
					dataRepository.WriteEncrypt((long)iconStartPos, icon, _IconIV);
				}

				//Закидаємо файл в потрібну папку і записуємо зміни
				_Exists = true;
				ChangeParent(parent, true);
			}
		}

		//for read from file
		private void ReadElementParamsFromBuffer(byte[] buf)
		{
			_Hash = new byte[16];
			_PHash = new byte[8];

			_FileStartPos = BitConverter.ToUInt64(buf, 0);
			_IconStartPos = BitConverter.ToUInt64(buf, 8);
			_FileSize = BitConverter.ToUInt64(buf, 16);
			_IconSize = BitConverter.ToUInt32(buf, 24);
			_IsCompressed = buf[28] < 128;
			Buffer.BlockCopy(buf, 29, _Hash, 0, 16);
			Buffer.BlockCopy(buf, 45, _PHash, 0, 8);
			_ParentID = BitConverter.ToUInt64(buf, 53);
			int lengthName = BitConverter.ToUInt16(buf, 61);
			_Name = Encoding.UTF8.GetString(buf, 63, lengthName);
		}

		public override ushort GetRawInfoLength()
		{
			byte[] UTF8Name = Encoding.UTF8.GetBytes(_Name);
			int realLength = RawInfLength + UTF8Name.Length;

			return Header.GetNewInfSizeByBufLength(realLength);
		}

		protected override byte[] GetRawInfo()
		{
			byte[] UTF8Name = Encoding.UTF8.GetBytes(_Name);
			int realLength = RawInfLength + UTF8Name.Length;
			ushort newInfSize = Header.GetNewInfSizeByBufLength(realLength);

			byte[] buf = new byte[newInfSize];
			Buffer.BlockCopy(BitConverter.GetBytes(_FileStartPos), 0, buf, 0, 8);
			Buffer.BlockCopy(BitConverter.GetBytes(_IconStartPos), 0, buf, 8, 8);
			Buffer.BlockCopy(BitConverter.GetBytes(_FileSize), 0, buf, 16, 8);
			Buffer.BlockCopy(BitConverter.GetBytes(_IconSize), 0, buf, 24, 4);
			byte isCompressed = (byte)(Convert.ToByte(!_IsCompressed) * 128 + (int)CryptoRandom.Random(128));
			Buffer.BlockCopy(BitConverter.GetBytes(isCompressed), 0, buf, 28, 1);
			Buffer.BlockCopy(_Hash, 0, buf, 29, 16);
			Buffer.BlockCopy(_PHash, 0, buf, 45, 8);
			Buffer.BlockCopy(BitConverter.GetBytes(_ParentID), 0, buf, 53, 8);
			Buffer.BlockCopy(BitConverter.GetBytes((UInt16)UTF8Name.Length), 0, buf, 61, 2);
			Buffer.BlockCopy(UTF8Name, 0, buf, 63, UTF8Name.Length);

			CryptoRandom.GetBytes(buf, realLength, buf.Length - realLength);

			return buf;
		}

		protected override void SaveInf()
		{
			header.SaveInfo(GetRawInfo());
		}

		public override void ExportInfTo(HeaderRepository repository, ulong position)
		{
			header.ExportInfoTo(repository, position, GetRawInfo());
		}

		private UInt64 GetSize()
		{
			return _FileSize;
		}

		private UInt64 GetFullSize()
		{
			return _FileSize + _IconSize;
		}

		private UInt64 GetFullEncryptSize()
		{
			return Crypto.GetMod16(_FileSize) + Crypto.GetMod16(_IconSize);
		}

		public void SaveTo(Stream stream, SafeStreamAccess.ProgressCallback Progress = null)
		{
			if (_FileSize == 0)
			{
				return;
			}

			dataRepository.MultithreadDecrypt((long)_FileStartPos, stream, (long)_FileSize, _FileIV, Progress);
		}

		public override void SaveTo(string PathToSave, SafeStreamAccess.ProgressCallback Progress = null)
		{
			Directory.CreateDirectory(PathToSave);
			using (FileStream stream = new FileStream(PathToSave + '\\' + _Name, FileMode.Create, FileAccess.Write, FileShare.Read))
			{
				SaveTo(stream, Progress);
			}

		}

		public override void SaveAs(string FullName, SafeStreamAccess.ProgressCallback Progress = null, Func<string, string> GetFileName = null)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FullName));
			using (FileStream stream = new FileStream(FullName, FileMode.Create, FileAccess.Write, FileShare.Read))
			{
				SaveTo(stream, Progress);
			}
		}

		protected override void Rename(string newName)
		{
			if ((_Parent == null) || (String.IsNullOrEmpty(newName)) || (newName == Name))
			{
				return;
			}

			if ((_Parent as DirElement).FileExists(newName))
			{
				throw new DuplicatesFileNameException("Файл з таким ім'ям вже є");
			}

			lock (dataRepository.writeLock)
			{
				lock (_changeElementsLocker)
				{
					_Name = newName;
					(_Parent as DirElement).RefreshChildOrders(); //ускорити це!
					SaveInf();
				}

				base.Rename(newName);
			}
		}

		public void ChangeContent(Stream NewData, SafeStreamAccess.ProgressCallback Progress = null)
		{
			lock (_addElementLocker)
			{
				UInt64 fileSize = (UInt64)NewData.Length;
				UInt64 fileStartPos = _FileStartPos;
				if (fileSize >= Crypto.GetMod16(_FileSize))
				{
					fileStartPos = dataRepository.GetFreeSpaceStartPos(Crypto.GetMod16(fileSize)); //Вибираємо місце куди писати файл
				}

				byte[] tempHash;

				try
				{
					if (fileSize > 0)
					{
						dataRepository.WriteEncrypt((long)fileStartPos, NewData, _FileIV, out tempHash, Progress);
					}
					else
					{
						tempHash = new byte[16];
						CryptoRandom.GetBytes(tempHash);
					}
				}
				catch
				{
					throw new DataWasNotWrittenException("Файл не записався.");
				}

				try
				{
					_FileStartPos = fileStartPos;
					_FileSize = fileSize;
					_Hash = tempHash;
					SaveInf();
				}
				catch
				{
					throw new HeaderWasNotWrittenException("Інформація про зміни не записалась!");
				}
			}
		}

		public override bool SetVirtualParent(DirElement NewParent)
		{
			if (NewParent == null)
			{
				return false;
			}

			lock (_changeElementsLocker)
			{
				if (NewParent.FileExists(_Name))
				{
					return false;
				}

				if (_Parent != null)
				{
					_Parent.RemoveElementFromElementsList(this);
				}

				_Parent = NewParent;
				_ParentID = NewParent.ID;

				_Parent.InsertElementToElementsList(this);
			}

			return true;
		}

		private void ChangeParent(DirElement NewParent, bool withWrite = false)
		{
			if (NewParent == null)
			{
				return;
			}

			lock (_changeElementsLocker)
			{
				if (NewParent.FileExists(_Name))
				{
					return;
				}

				if (_Parent != null)
				{
					(_Parent as DirElement).RemoveElementFromElementsList(this);
				}

				bool writeToFile = _ParentID != NewParent.ID || withWrite;
				_Parent = NewParent;
				_ParentID = NewParent.ID;

				(_Parent as DirElement).InsertElementToElementsList(this);

				if (writeToFile)
				{
					SaveInf();
				}
			}
		}

		public override bool Delete()
		{
			lock (_changeElementsLocker)
			{
				if (_Delete())
				{
					if (_Parent != null)
					{
						Parent.RemoveElementFromElementsList(this);
					}

					return true;
				}

				return false;
			}
		}

		public bool _Delete()
		{
			try
			{
				header.Delete();
				dataRepository.AddFreeSpace(_FileStartPos, Crypto.GetMod16(_FileSize));
				dataRepository.AddFreeSpace(_IconStartPos, Crypto.GetMod16(_IconSize));
			}
			catch
			{
				return false;
			}

			return true;
		}

		public override bool Restore()
		{
			if (_Exists)
			{
				return true;
			}

			if ((_FileSize == 0 || dataRepository.IsFreeSpace(_FileStartPos, _FileSize)) && (_IconSize == 0 || dataRepository.IsFreeSpace(_IconStartPos, _IconSize)))
			{
				try
				{
					Parent = _Parent;
					_Exists = true;
				}
				catch
				{
					return false;
				}

				return true;
			}

			return false;
		}
	}
}
