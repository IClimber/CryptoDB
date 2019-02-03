using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CryptoDataBase
{
	public class Element : INotifyPropertyChanged
	{
		private static Object _changeElementsLocker = new Object();
		private static Object _addFileLocker = new Object();
		private static Object _freeSpaceMapLocker = new Object();

		protected static List<SPoint> FreeSpaceMap = new List<SPoint>();
		private static long AllElementsCount = 0;
		private const int FileInfLength = 63;
		private const int DirInfLength = 38;

		public event PropertyChangedEventHandler PropertyChanged;
		protected Head head;
		public IList<Element> Elements { get { return _Elements.AsReadOnly(); } }
		private List<Element> _Elements;
		public ElementType Type { get { return _Type; } }
		private ElementType _Type;
		public Bitmap Icon { get { return GetIcon(); } set { SetIcon(value); } }
		public UInt64 FileStartPos { get { return _FileStartPos; } }
		private UInt64 _FileStartPos;
		public UInt64 IconStartPos { get { return _IconStartPos; } }
		private UInt64 _IconStartPos;
		public UInt64 Size { get { return GetSize(0); } }
		private UInt64 _FileSize;
		public UInt32 IconSize { get { return _IconSize; } }
		private UInt32 _IconSize;
		public bool IsCompressed { get { return _IsCompressed; } }
		private bool _IsCompressed;
		public byte[] Hash { get { return _Hash; } }
		private byte[] _Hash;
		public byte[] PHash { get { return _PHash; } }
		private byte[] _PHash;
		private byte[] _FileIV;
		private byte[] _IconIV;
		public UInt64 ID { get { return _ID; } }
		private UInt64 _ID;
		public UInt64 ParentID { get { return _ParentID; } }
		private UInt64 _ParentID;
		/// <summary>
		/// Змінює батьківську папку з збереженням змін
		/// </summary>
		public Element Parent { get { return _Parent; } set { ChangeParent(value, true); } }
		/// <summary>
		/// Змінює батьківську папку без збереження змін
		/// </summary>
		public Element LocalParent { get { return _Parent; } set { ChangeParent(value, false); } }
		private Element _Parent;
		public string Name { get { return _Name; } set { Rename(value); } }
		private string _Name;
		public long TimeIndex { get { return _TimeIndex; } }
		protected long _TimeIndex;
		public string GetPath { get { return _GetPath(); } }
		protected SafeStreamAccess headersFileStream;
		protected SafeStreamAccess dataFileStream;

		private void NotifyPropertyChanged(String propertyName = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private Element()
		{
			AllElementsCount++;
			_TimeIndex = AllElementsCount;
		}

		protected Element(bool isRootElement = false) : this()
		{
			_ID = 0;
			_ParentID = 0;
			_Type = ElementType.Dir;
			_Elements = new List<Element>();
		}

		private Element(string Name)
		{
			_Name = Name;
		}

		public Element(UInt64 ID)
		{
			_ID = ID;
		}

		private Element(SafeStreamAccess headersFileStream, SafeStreamAccess dataFileStream) : this()
		{
			_Elements = new List<Element>();
			this.headersFileStream = headersFileStream;
			this.dataFileStream = dataFileStream;
		}

		/*public Element(FileStream fileStream, FileStream dataFileStream, Head head) : this(fileStream, dataFileStream) //reading from file
		{
			this.head = head;
			_Type = head.ElType;
			byte[] buf = new byte[head.InfSize];
			fileStream.Position = (int)head.StartPos + Head.Length;
			fileStream.Read(buf, 0, head.InfSize);

			if (head.ElType == ElementType.File)
			{
				ReadFileElement(buf);
			}
			else
			{
				ReadDirElement(buf);
			}
		}*/

		//reading from MemoryStream
		public Element(MemoryStream headerMemoryStream, SafeStreamAccess headersFileStream, SafeStreamAccess dataFileStream, Head head) : this(headersFileStream, dataFileStream)
		{
			this.head = head;
			_Type = head.ElType;
			byte[] buf = new byte[head.InfSize];

			head.AES.IV = head.IV;
			head.AES.Padding = PaddingMode.None;
			headerMemoryStream.Position = (int)head.StartPos + Head.Length;
			Crypto.AES_Decrypt(headerMemoryStream, buf, buf.Length, head.AES);

			if (head.ElType == ElementType.File)
			{
				ReadFileElement(buf);
			}
			else
			{
				ReadDirElement(buf);
			}
		}

		public static UInt16 GetMod16(UInt16 length)
		{
			return (UInt16)GetMod16((UInt64)length);
		}

		public static UInt64 GetMod16(UInt64 length)
		{
			return length % 16 == 0 ? length + 16 : (UInt64)(Math.Ceiling(length / 16.0) * 16);
		}

		// Конструктор для папки
		private Element(SafeStreamAccess headersFileStream, SafeStreamAccess dataFileStream, string Name, UInt64 ID, UInt64 iconStartPos, UInt32 iconSize,
			byte[] PHash, AesCryptoServiceProvider AES) : this(headersFileStream, dataFileStream)
		{
			try
			{
				UInt64 startPos = (UInt64)headersFileStream.Length;
				UInt16 infSize = (UInt16)(DirInfLength + Encoding.UTF8.GetByteCount(Name));
				head = new Head(startPos, infSize, AES, ElementType.Dir, true);

				_Type = ElementType.Dir;
				_Name = Name;
				_ID = ID;
				_ParentID = 1; //1 зарезервована і не може бути ID папки, тому при зміні власника папка запишеться в файл
				_IconStartPos = iconStartPos;
				_IconSize = iconSize;
				_PHash = PHash;
				_IconIV = Crypto.GetMD5(head.IV);
				//Parent = parent; // використовується Parent, щоб зразу внести в список, і, якщо потрібно, записати в файл
			}
			catch
			{
				throw new Exception("Щось пішло не по плану з папкою.");
			}
		}

		// Конструктор для файла
		public Element(SafeStreamAccess headersFileStream, SafeStreamAccess dataFileStream, string Name, UInt64 fileStartPos, UInt64 fileSize, UInt64 iconStartPos, UInt32 iconSize,
			bool isCompressed, byte[] PHash, AesCryptoServiceProvider AES) : this(headersFileStream, dataFileStream)
		{
			/*UInt64 startPos = (UInt64)headersFileStream.Length;
			UInt16 InfSize = (UInt16)(FileLengthInf + Encoding.UTF8.GetByteCount(Name));
			head = new Head(startPos, InfSize, AES, ElementType.File, true);*/

			_Type = ElementType.File;
			_Name = Name;
			_ParentID = 1; //1 зарезервована і не може бути ID папки, тому при зміні власника папка запишеться в файл
			_FileStartPos = fileStartPos;
			_FileSize = fileSize;
			_IconStartPos = iconStartPos;
			_IconSize = iconSize;
			_IsCompressed = isCompressed;
			//_Hash = ; //присвоюється після запису файла
			_PHash = PHash;
			//IconIV = Crypto.GetMD5(head.IV);
			//FileIV = Crypto.GetMD5(IconIV);
			//Parent = parent; // використовується Parent, щоб зразу внести в список, і, якщо потрібно, записати в файл //Присвоїти parent потім
		}

		//for read from file
		private void ReadFileElement(byte[] buf)
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

		//for read from file
		private void ReadDirElement(byte[] buf)
		{
			_PHash = new byte[8];

			_IconStartPos = BitConverter.ToUInt64(buf, 0);
			_IconSize = BitConverter.ToUInt32(buf, 8);
			Buffer.BlockCopy(buf, 12, _PHash, 0, 8);
			_ParentID = BitConverter.ToUInt64(buf, 20);
			_ID = BitConverter.ToUInt64(buf, 28);
			int lengthName = BitConverter.ToUInt16(buf, 36);
			_Name = Encoding.UTF8.GetString(buf, 38, lengthName);
		}

		private byte[] ToBufferFileInf()
		{
			byte[] UTF8Name = Encoding.UTF8.GetBytes(_Name);
			byte[] buf = new byte[head.InfSize];
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
			byte[] trash = new byte[head.InfSize - (FileInfLength + UTF8Name.Length)];
			CryptoRandom.GetBytes(trash);
			Buffer.BlockCopy(trash, 0, buf, FileInfLength + UTF8Name.Length, trash.Length);

			return buf;
		}

		private byte[] ToBufferDirInf()
		{
			byte[] UTF8Name = Encoding.UTF8.GetBytes(_Name);
			byte[] buf = new byte[head.InfSize];
			Buffer.BlockCopy(BitConverter.GetBytes(_IconStartPos), 0, buf, 0, 8);
			Buffer.BlockCopy(BitConverter.GetBytes(_IconSize), 0, buf, 8, 4);
			Buffer.BlockCopy(_PHash, 0, buf, 12, 8);
			Buffer.BlockCopy(BitConverter.GetBytes(_ParentID), 0, buf, 20, 8);
			Buffer.BlockCopy(BitConverter.GetBytes(_ID), 0, buf, 28, 8);
			Buffer.BlockCopy(BitConverter.GetBytes((UInt16)UTF8Name.Length), 0, buf, 36, 2);
			Buffer.BlockCopy(UTF8Name, 0, buf, 38, UTF8Name.Length);
			byte[] trash = new byte[head.InfSize - (DirInfLength + UTF8Name.Length)];
			CryptoRandom.GetBytes(trash);
			Buffer.BlockCopy(trash, 0, buf, DirInfLength + UTF8Name.Length, trash.Length);

			return buf;
		}

		private byte[] ToBufferInf()
		{
			return _Type == ElementType.File ? ToBufferFileInf() : ToBufferDirInf();
		}

		private UInt64 GenID()
		{
			return CryptoRandom.Random(UInt64.MaxValue - 2) + 2;
		}

		private int GetStartPos(UInt64 Size)
		{
			int index = -1;
			int min = 0,
				max = FreeSpaceMap.Count - 1;

			while (min <= max)
			{
				int mid = (min + max) / 2;
				if (Size == FreeSpaceMap[mid].Size)
				{
					return mid;// ++mid;
				}
				else if (Size < FreeSpaceMap[mid].Size)
				{
					max = mid - 1;
					index = mid;
				}
				else
				{
					min = mid + 1;
				}
			}
			return index;
		}

		private UInt64 GetStartPosAndSaveChange(UInt64 Size, UInt64 defStartPos)
		{
			lock (_freeSpaceMapLocker)
			{
				if (Size == 0) //Вибираємо місце куди писати файл
				{
					return GenID();
				}

				UInt64 result = defStartPos;
				int pos = GetStartPos(Size);
				if (pos >= 0)
				{
					result = FreeSpaceMap[pos].Start;
					FreeSpaceMap[pos] = new SPoint(FreeSpaceMap[pos].Start + Size, FreeSpaceMap[pos].Size - Size);
				}

				return result;
			}
		}

		public Element AddFile(Stream stream, string destFileName, bool compressFile = false, Bitmap Icon = null, SafeStreamAccess.ProgressCallback Progress = null)
		{
			if (_Type == ElementType.File)
			{
				throw new Exception("В файлі не можна створювати файли!");
			}

			lock (_addFileLocker)
			{
				lock (_changeElementsLocker)
				{
					Element file = FindByName(_Elements, destFileName);
					if (file != null)
					{
						return file.Type == ElementType.File ? file : null; //Зробити, щоб не повертати папку, кидати виключення
																			//throw new Exception("Файл з таким ім’ям вже є.");
					}
				}

				UInt64 fileSize = (UInt64)stream.Length;
				UInt64 fileStartPos = GetStartPosAndSaveChange(GetMod16(fileSize), (UInt64)dataFileStream.Length); //Вибираємо місце куди писати файл
				byte[] icon = GetIconBytes(Icon);
				UInt32 iconSize = icon == null ? 0 : (UInt32)icon.Length;

				UInt64 iconStartPos = iconSize == 0
					? GenID() : GetStartPosAndSaveChange(GetMod16(iconSize), (UInt64)dataFileStream.Length + (fileStartPos == (UInt64)dataFileStream.Length
					? GetMod16(fileSize) : 0)); //Вибираємо місце куди писати іконку

				Head tempHead = new Head(0, (UInt16)(FileInfLength + Encoding.UTF8.GetByteCount(destFileName)), head.AES, ElementType.File, true);
				byte[] tempHash;
				var _tempIconIV = Crypto.GetMD5(tempHead.IV);
				var _tempFileIV = Crypto.GetMD5(_tempIconIV);

				try
				{
					if (fileSize > 0)
					{
						tempHead.AES.IV = _tempFileIV;
						tempHead.AES.Padding = PaddingMode.ISO10126;
						dataFileStream.WriteEncrypt((long)fileStartPos, stream, tempHead.AES, out tempHash, Progress);
					}
					else
					{
						tempHash = new byte[16];
						CryptoRandom.GetBytes(tempHash);
					}

					if ((icon != null) && (iconSize > 0))
					{
						tempHead.AES.IV = _tempIconIV;
						tempHead.AES.Padding = PaddingMode.ISO10126;
						dataFileStream.WriteEncrypt((long)iconStartPos, icon, tempHead.AES);
					}
				}
				catch
				{
					throw new Exception("Файл або іконка не записались.");
				}

				lock (_changeElementsLocker)
				{
					Element file;
					try
					{
						tempHead.StartPos = (UInt64)headersFileStream.Length;
						file = new Element(headersFileStream, dataFileStream, destFileName, fileStartPos, fileSize, iconStartPos, iconSize, compressFile, GetPHash(Icon), tempHead.AES);
						file.head = tempHead;
						file._IconIV = _tempIconIV;
						file._FileIV = _tempFileIV;
						file._Hash = tempHash;
					}
					catch
					{
						throw new Exception("Якась херня з формуванням заголовка файла");
					}

					file.Parent = this;
					return file;
				}
			}
		}

		public Element AddFile(string sourceFileName, string destFileName, bool compressFile = false, Bitmap Icon = null, SafeStreamAccess.ProgressCallback Progress = null)
		{
			try
			{
				using (FileStream f = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					return AddFile(f, destFileName, compressFile, Icon, Progress);
				}
			}
			catch
			{
				return null;
			}
		}

		public void ChangeContent(Stream NewData, SafeStreamAccess.ProgressCallback Progress = null)
		{
			if (_Type != ElementType.File)
			{
				throw new Exception("Дані можна змінювати тільки в файлі!");
			}

			lock (_addFileLocker)
			{
				UInt64 fileSize = (UInt64)NewData.Length;
				UInt64 fileStartPos = _FileStartPos;
				if (NewData.Length >= (long)GetMod16(_FileSize))
				{
					fileStartPos = GetStartPosAndSaveChange(GetMod16(fileSize), (UInt64)dataFileStream.Length); //Вибираємо місце куди писати файл
				}
				//Зробити видалення / додавання вільного місця в FreeSpaceMap

				try
				{
					if (fileSize > 0)
					{
						head.AES.IV = _FileIV;
						head.AES.Padding = PaddingMode.ISO10126;
						dataFileStream.WriteEncrypt((long)fileStartPos, NewData, head.AES, out _Hash, Progress);
					}
					else
					{
						_Hash = new byte[16];
						CryptoRandom.GetBytes(_Hash);
					}
				}
				catch
				{
					throw new Exception("Файл не записався.");
				}

				try
				{
					head.AES.IV = head.IV;
					head.AES.Padding = PaddingMode.None;
					_FileStartPos = fileStartPos;
					_FileSize = fileSize;
					headersFileStream.WriteEncrypt((long)(head.StartPos + Head.Length), ToBufferInf(), head.AES);
				}
				catch
				{
					throw new Exception("Інформація про зміни не записалась!");
				}
			}
		}

		public Element CreateDir(string Name, Bitmap Icon)
		{
			if (_Type == ElementType.File)
			{
				throw new Exception("В файлі не можна створювати папки!");
			}

			int index;
			Element dir;
			lock (_changeElementsLocker)
			{
				dir = FindByName(_Elements, Name, out index);
			}

			if (dir != null)
			{
				if (dir._Type == ElementType.Dir)
				{
					return dir;
				}
				else
				{
					throw new Exception("Не можна створити папку. Файл з таким ім’ям вже є.");
				}
			}

			byte[] icon = GetIconBytes(Icon);
			UInt32 iconSize = icon == null ? 0 : (UInt32)icon.Length;

			try
			{
				if ((icon != null) && (iconSize > 0))
				{
					dir.head.AES.IV = dir._IconIV;
					dir.head.AES.Padding = PaddingMode.ISO10126;
					lock (_addFileLocker)
					{
						UInt64 iconStartPos = GetStartPosAndSaveChange(GetMod16(iconSize), (UInt64)dataFileStream.Length); //Вибираємо місце куди писати іконку
						dir = new Element(headersFileStream, dataFileStream, Name, GenID(), iconStartPos, iconSize, GetPHash(Icon), head.AES);
						dataFileStream.WriteEncrypt((long)iconStartPos, icon, dir.head.AES);
					}
				}
				else
				{
					dir = new Element(headersFileStream, dataFileStream, Name, GenID(), GenID(), 0, GetPHash(Icon), head.AES);
				}
			}
			catch
			{
				throw new Exception("Іконка папки не записалась.");
			}

			dir.Parent = this;

			return dir;
		}

		public Element CreateDir(string Name)
		{
			return CreateDir(Name, null);
		}

		public void SaveTo(Stream stream, SafeStreamAccess.ProgressCallback Progress = null)
		{
			if ((_Type != ElementType.File) || (_FileSize == 0))
			{
				return;
			}

			if (_FileIV == null)
			{
				if (_IconIV == null)
				{
					_IconIV = Crypto.GetMD5(head.IV);
				}

				_FileIV = Crypto.GetMD5(_IconIV);
			}

			AesCryptoServiceProvider AES = new AesCryptoServiceProvider();
			AES.KeySize = head.AES.KeySize;
			AES.BlockSize = head.AES.BlockSize;
			AES.Key = head.AES.Key;
			AES.Mode = head.AES.Mode;
			AES.IV = _FileIV;
			AES.Padding = PaddingMode.ISO10126;

			//dataFileStream.ReadDecrypt((long)_FileStartPos, stream, (long)_FileSize, AES, Progress);
			dataFileStream.MultithreadDecrypt((long)_FileStartPos, stream, (long)_FileSize, AES, Progress);
			AES.Dispose();
		}

		public void SaveTo(string PathToSave, SafeStreamAccess.ProgressCallback Progress = null)
		{
			if (_Type == ElementType.File)
			{
				Directory.CreateDirectory(PathToSave);
				using (FileStream stream = new FileStream(PathToSave + '\\' + Name, FileMode.Create, FileAccess.Write, FileShare.Read))
				{
					SaveTo(stream, Progress);
				}
			}
			else
			{
				ExportDir(PathToSave, Progress: Progress);
			}
		}

		public void SaveAs(string FullName, SafeStreamAccess.ProgressCallback Progress = null, Func<string, string> GetFileName = null)
		{
			if (_Type == ElementType.File)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FullName));
				using (FileStream stream = new FileStream(FullName, FileMode.Create, FileAccess.Write, FileShare.Read))
				{
					SaveTo(stream, Progress);
				}
			}
			else
			{
				ExportDir(Path.GetDirectoryName(FullName), Path.GetFileName(FullName), Progress: Progress, GetFileName: GetFileName);
			}
		}

		private void ExportDir(string destPath, string Name = "", SafeStreamAccess.ProgressCallback Progress = null, Func<string, string> GetFileName = null)
		{
			bool randomNames = (GetFileName != null);
			
			string tempName = Name == "" ? _Name : Name;
			Directory.CreateDirectory(destPath + '\\' + tempName);
			Element[] elementList;

			lock (_changeElementsLocker)
			{
				elementList = new Element[_Elements.Count];
				_Elements.CopyTo(elementList);
			}

			foreach (var element in elementList)
			{
				if (element._Type == ElementType.Dir)
				{
					if (randomNames)
					{
						element.ExportDir(destPath + '\\' + tempName, GetFileName(Path.GetExtension(element.Name)), Progress: Progress);
					}
					else
					{
						element.ExportDir(destPath + '\\' + tempName, Progress: Progress);
					}
				}
				else
				{
					if (randomNames)
					{
						element.SaveAs(destPath + '\\' + tempName + "\\" + GetFileName(Path.GetExtension(element.Name)), Progress);
					}
					else
					{
						element.SaveTo(destPath + '\\' + tempName, Progress);
					}
				}
			}

			elementList = null;
		}

		//Шукає в сортованому по Name списку
		private Element FindByName(List<Element> elements, string Name)
		{
			int index = elements.BinarySearch(new Element(Name), new NameComparer());
			Element result = index >= 0 ? elements[index] : null;

			return result;
		}

		//Шукає в сортованому по Name списку
		private Element FindByName(List<Element> elements, string Name, out int index)
		{
			index = elements.BinarySearch(new Element(Name), new NameComparer());
			Element result = index >= 0 ? elements[index] : null;
			index = index < 0 ? Math.Abs(index) - 1 : index;

			return result;
		}

		private void ChangeParent(Element NewParent, bool WithWrite)
		{
			if ((NewParent == null) || (NewParent._Type != ElementType.Dir) || (NewParent == this))
			{
				return;
			}

			lock (_changeElementsLocker)
			{

				bool writeToFile = ((_Parent != NewParent) && (NewParent.ID != _ParentID));

				if ((writeToFile) && (FindByName(NewParent._Elements, _Name) != null))
				{
					return;
				}

				if ((writeToFile) && (FindSubDirByID(NewParent.ID) != null))
				{
					return;
				}

				int index;
				if (_Parent != null)
				{
					if (FindByName(_Parent._Elements, _Name, out index) != null)
					{
						_Parent._Elements.RemoveAt(index);
					}
				}

				_Parent = NewParent;
				_ParentID = _Parent.ID;

				if (FindByName(_Parent._Elements, _Name, out index) == null)
				{
					_Parent._Elements.Insert(index, this);
				}
				else
				{
					throw new Exception("Елемент з такою назвою в цьому списку вже є!");
				}

				if (writeToFile && WithWrite)
				{
					head.AES.IV = head.IV;
					head.AES.Padding = PaddingMode.None;

					if ((long)head.StartPos == headersFileStream.Length) //Роздуплитись чи точно так //вроді так
					{
						byte[] buf = head.ToBufferFirstBlock(); //Зробити щоб записувало не все, а тільки потрібне
						headersFileStream.Write((long)(head.StartPos), buf, 0, buf.Length);
						headersFileStream.WriteEncrypt((long)(head.StartPos) + buf.Length, head.ToBufferEncryptBlock(), head.AES);
					}

					headersFileStream.WriteEncrypt((long)(head.StartPos + Head.Length), ToBufferInf(), head.AES);
				}
			}
		}

		private void Rename(string NewName)
		{
			if ((_Parent == null) || (String.IsNullOrEmpty(NewName)) || (NewName == this.Name))
			{
				return;
			}

			lock (_changeElementsLocker)
			{
				Element duplicate = FindByName(_Parent._Elements, NewName);
				if ((duplicate != null) && (duplicate != this))
				{
					return;
				}

				head.AES.IV = head.IV;
				head.AES.Padding = PaddingMode.None;

				UInt16 newInfSize = (UInt16)((_Type == ElementType.Dir ? DirInfLength : FileInfLength) + Encoding.UTF8.GetByteCount(NewName));
				//Якщо нове ім’я довше ніж попереднє і не влазить на його місце, то видаляємо старий файл і записуємо його в кінець з новим ім’ям
				if (head.InfSize < newInfSize)
				{
					head.DeleteAndWrite(headersFileStream);
					head.Exists = true;
					head.StartPos = (UInt64)headersFileStream.Length;
					head.InfSize = newInfSize; //виправити це так, щоб InfSize був приватним

					byte[] buf = head.ToBufferFirstBlock();
					headersFileStream.Write((long)(head.StartPos), buf, 0, buf.Length);
					headersFileStream.WriteEncrypt((long)(head.StartPos) + buf.Length, head.ToBufferEncryptBlock(), head.AES);
				}

				_Name = NewName;
				_Parent._Elements.Sort(new NameComparer()); //ускорити це!

				byte[] buf1 = ToBufferInf();
				headersFileStream.WriteEncrypt((long)(head.StartPos + Head.Length), buf1, head.AES);
			}

			NotifyPropertyChanged("Name");
		}

		private string _GetPath()
		{
			string result = Parent?.GetPath + (Parent == null ? "" : "\\") + Parent?.Name;
			return result;
		}

		private UInt64 GetSize(UInt64 size)
		{
			UInt64 result = size;
			if (_Type == ElementType.File)
			{
				return _FileSize + result;
			}
			else
			{
				lock (_changeElementsLocker)
				{
					foreach (var element in _Elements)
					{
						result = element.GetSize(result);
					}
				}
			}

			return result;
		}

		// Пошук в папці по імені файла
		public bool FileExists(string Name)
		{
			if (_Type != ElementType.Dir)
			{
				return false;
			}

			lock (_changeElementsLocker)
			{
				Element temp = new Element(Name);
				return _Elements.BinarySearch(temp, new NameComparer()) >= 0;
			}
		}

		//Пошук підпапки по ID
		private Element FindSubDirByID(UInt64 ID)
		{
			Element result;

			//lock (_changeElementsLocker)
			{
				foreach (var element in _Elements.Where(x => (x.Type == ElementType.Dir)))
				{
					if (element.ID == ID)
					{
						return element;
					}

					result = element.FindSubDirByID(ID);
					if (result != null)
					{
						return result;
					}
				}
			}

			return null;
		}

		public List<Element> FindByName(string Name, bool FindAsTags = false, bool AllTagsRequired = false, bool FindInSubDirectories = true)
		{
			if (_Type == ElementType.File)
			{
				return null;
			}

			List<Element> result = new List<Element>();

			if (FindAsTags)
			{
				string[] tags = Name.Split(' ');
				_FindAsTags(result, tags, AllTagsRequired, FindInSubDirectories);
			}
			else
			{
				_Find(result, Name, FindInSubDirectories);
			}

			return result;
		}

		private void _FindAsTags(List<Element> resultList, string[] tags, bool allTagsRequired, bool FindInSubDirectories)
		{
			lock (_changeElementsLocker)
			{
				foreach (var element in _Elements)
				{
					int coincidencesNumber = 0;

					foreach (string tag in tags)
					{
						if (element.Name.IndexOf(tag, StringComparison.CurrentCultureIgnoreCase) >= 0)
						{
							coincidencesNumber++;

							if (allTagsRequired)
							{
								if (coincidencesNumber == tags.Length)
								{
									resultList.Add(element);
								}
							}
							else
							{
								resultList.Add(element);
								break;
							}
						}
					}

					if ((element.Type == ElementType.Dir) && (FindInSubDirectories))
					{
						element._FindAsTags(resultList, tags, allTagsRequired, FindInSubDirectories);
					}
				}
			}
		}

		private void _Find(List<Element> resultList, string Name, bool FindInSubDirectories)
		{
			lock (_changeElementsLocker)
			{
				foreach (var element in _Elements)
				{
					if (element.Name.IndexOf(Name, StringComparison.CurrentCultureIgnoreCase) >= 0)
					{
						resultList.Add(element);
					}

					if ((element.Type == ElementType.Dir) && (FindInSubDirectories))
					{
						element._Find(resultList, Name, FindInSubDirectories);
					}
				}
			}
		}

		public List<Element> FindAllByIcon(Bitmap image, byte sensative = 0, bool FindInSubDirectories = true)
		{
			if (_Type == ElementType.File)
			{
				return null;
			}

			byte[] pHash = GetPHash(image);
			List<Element> result = new List<Element>();
			_FindAllByIcon(result, pHash, sensative, FindInSubDirectories);

			return result;
		}

		private void _FindAllByIcon(List<Element> resultList, byte[] pHash, byte sensative, bool FindInSubDirectories)
		{
			lock (_changeElementsLocker)
			{
				foreach (var element in _Elements)
				{
					if (element._IconSize > 0)
					{
						if (ComparePHashes(element.PHash, pHash, sensative))
						{
							resultList.Add(element);
						}
					}

					if ((element.Type == ElementType.Dir) && (FindInSubDirectories))
					{
						element._FindAllByIcon(resultList, pHash, sensative, FindInSubDirectories);
					}
				}
			}
		}

		public List<Element> FindByHash(byte[] Hash, bool FindInSubDirectories = true)
		{
			if (_Type == ElementType.File)
			{
				return null;
			}

			List<Element> result = new List<Element>();
			_FindByHash(result, Hash, FindInSubDirectories);

			return result;
		}

		private void _FindByHash(List<Element> resultList, byte[] Hash, bool FindInSubDirectories)
		{
			lock (_changeElementsLocker)
			{
				foreach (var element in _Elements)
				{
					if ((element.Type == ElementType.File) && (Crypto.CompareHash(element.Hash, Hash)))
					{
						resultList.Add(element);
					}

					if ((element.Type == ElementType.Dir) && (FindInSubDirectories))
					{
						element._FindByHash(resultList, Hash, FindInSubDirectories);
					}
				}
			}
		}

		private Bitmap GetIcon()
		{
			if (IconSize == 0)
			{
				return null;
			}

			if (_IconIV == null)
			{
				_IconIV = Crypto.GetMD5(head.IV);
			}

			MemoryStream stream = new MemoryStream();

			AesCryptoServiceProvider AES = new AesCryptoServiceProvider();
			AES.KeySize = head.AES.KeySize;
			AES.BlockSize = head.AES.BlockSize;
			AES.Key = head.AES.Key;
			AES.Mode = head.AES.Mode;
			AES.IV = _IconIV;
			AES.Padding = PaddingMode.ISO10126;

			dataFileStream.ReadDecrypt((long)_IconStartPos, stream, _IconSize, AES, null);
			AES.Dispose();

			try
			{
				stream.Position = 0;
				Bitmap result = new Bitmap(stream);
				stream.Dispose();
				return result;
			}
			catch
			{
				return null;
			}
		}

		private void SetIcon(Bitmap icon)
		{
			byte[] buf;
			UInt32 oldIconSize = _IconSize;
			//Якщо стара іконка видаляється, то зробити пошук в FreeSpaceMap і додавати туди вільне місце
			_IconSize = 0;
			_IconStartPos = GenID();
			_PHash = GetPHash(Icon);

			if (icon != null)
			{
				buf = GetIconBytes(icon);

				_IconSize = (UInt32)buf.Length;
				if (_IconSize == 0) //Вибираємо місце куди писати іконку
				{
					_IconStartPos = GenID();
				}
				else
				{
					lock (_addFileLocker)
					{
						_IconStartPos = GetStartPosAndSaveChange(GetMod16(_IconSize), (UInt64)dataFileStream.Length);

						if (_IconIV == null)
						{
							_IconIV = Crypto.GetMD5(head.IV);
						}

						//dataFileStream.Position = (long)_IconStartPos;
						head.AES.IV = _IconIV;
						head.AES.Padding = PaddingMode.ISO10126;
						dataFileStream.WriteEncrypt((long)_IconStartPos, buf, head.AES);
						//Crypto.AES_Encrypt(buf, dataFileStream, head.AES);
					}
				}
			}

			head.AES.IV = head.IV;
			head.AES.Padding = PaddingMode.None;
			headersFileStream.WriteEncrypt((long)(head.StartPos + Head.Length), ToBufferInf(), head.AES);
			NotifyPropertyChanged("Icon");

			buf = null;
		}

		private byte[] GetIconBytes(Bitmap Icon)
		{
			try
			{
				using (MemoryStream ms = new MemoryStream())
				{
					if (Icon.PixelFormat == PixelFormat.Format32bppArgb)
					{
						Icon.Save(ms, ImageFormat.Png);
					}
					else
					{
						Icon.Save(ms, ImageFormat.Jpeg);
					}
					ms.Position = 0;

					return ms.ToArray();
				}
			}
			catch
			{
				return null;
			}
		}

		private byte[] GetPHash(Bitmap Icon)
		{
			if (Icon == null)
			{
				byte[] phash = new byte[8];
				CryptoRandom.GetBytes(phash);
				return phash;
			}

			try
			{
				return _GetPHash(Icon);
			}
			catch
			{
				return GetPHash(null);
			}
		}

		private static byte[] _GetPHash(Bitmap bmp)
		{
			Bitmap bm = new Bitmap(bmp, new Size(8, 8));
			BitmapData data = bm.LockBits(new Rectangle(0, 0, bm.Width, bm.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
			var result = new byte[8];
			BitArray bitArr = new BitArray(64);
			byte[] arr = new byte[64];
			ushort mid = 0;
			unsafe
			{
				int i = 0;
				byte* ptr = (byte*)data.Scan0;
				for (int y = 0; y < bm.Height; y++)
				{
					byte* ptr2 = ptr;
					for (int x = 0; x < bm.Width; x++)
					{
						arr[i++] = (byte)((*(ptr2++) * 0.11) + (*(ptr2++) * 0.59) + (*(ptr2++) * 0.3));
						mid += arr[i - 1];
					}
					ptr += data.Stride;
				}

				mid = (UInt16)(mid / 64);
				for (i = 0; i < arr.Length; i++)
				{
					bitArr[i] = arr[i] >= mid;
				}
			}
			bm.UnlockBits(data);
			bm.Dispose();

			bitArr.CopyTo(result, 0);
			return result;
		}

		private static byte GetHammingDistance(UInt64 PHash1, UInt64 PHash2)
		{
			byte dist = 0;
			UInt64 val = PHash1 ^ PHash2;

			while (val > 0)
			{
				++dist;
				val &= val - 1;
			}

			return dist;
		}

		private bool ComparePHashes(byte[] PHash1, byte[] PHash2, byte sensative)
		{
			byte dist = GetHammingDistance(BitConverter.ToUInt64(PHash1, 0), BitConverter.ToUInt64(PHash2, 0));
			return (dist <= sensative) || (dist >= (64 - sensative));
		}

		public bool Delete()
		{
			lock (_changeElementsLocker)
			{
				if (_Delete())
				{
					int index;
					if (_Parent != null)
					{
						if (FindByName(_Parent._Elements, _Name, out index) != null)
						{
							_Parent._Elements.RemoveAt(index);
						}
					}

					return true;
				}

				return false;
			}
		}

		private bool _Delete()
		{
			try
			{
				if (_Parent != null)
				{
					head.DeleteAndWrite(headersFileStream);
					//Зробити пошук в FreeSpaceMap і додавати туди вільне місце
				}

				if (_Type == ElementType.Dir)
				{
					foreach (var element in _Elements)
					{
						element._Delete();
					}

					_Elements.Clear();
				}
			}
			catch
			{
				return false;
			}

			return true;
		}
	}
}