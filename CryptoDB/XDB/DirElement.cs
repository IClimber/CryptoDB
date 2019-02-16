using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CryptoDataBase
{
	public class DirElement : Element
	{
		public override ElementType Type { get { return ElementType.Dir; } }
		public IList<Element> Elements { get { return _Elements.AsReadOnly(); } }
		public override UInt64 Size { get { return GetSize(0); } }
		public UInt64 ID { get { return _ID; } }
		private List<Element> _Elements;
		private const int DirInfLength = 38;	
		protected UInt64 _ID;
		public override DirElement Parent { get { return _Parent; } set { ChangeParent(value); } }

		protected DirElement()
		{
			_Elements = new List<Element>();
		}

		protected DirElement(Object addElementLocker, Object changeElementsLocker) : base(addElementLocker, changeElementsLocker)
		{
			_Elements = new List<Element>();
		}

		private DirElement(string Name)
		{
			_Name = Name;
		}

		public DirElement(UInt64 ID)
		{
			_ID = ID;
		}

		//Створення папки при читані з файлу
		public DirElement(Header header, SafeStreamAccess dataFileStream, Object addElementLocker, Object changeElementsLocker) : base(header, dataFileStream, addElementLocker, changeElementsLocker)
		{
			_Elements = new List<Element>();
			byte[] buf = header.GetInfoBuf();

			ReadElementParamsFromBuffer(buf);

			buf = null;
		}

		//Створення папки вручну
		protected DirElement(DirElement parent, SafeStreamAccess dataFileStream, string Name, Object addElementLocker, Object changeElementsLocker, Bitmap Icon = null,
			SafeStreamAccess.ProgressCallback Progress = null) : this(addElementLocker, changeElementsLocker)
		{
			lock (_addElementLocker)
			{
				header = new Header(parent.header.headersFileStream, parent.header.AES, ElementType.Dir);
				this.dataFileStream = dataFileStream;

				byte[] icon = GetIconBytes(Icon);
				UInt32 iconSize = icon == null ? 0 : (UInt32)icon.Length;
				UInt64 iconStartPos = dataFileStream.GetFreeSpaceStartPos(Crypto.GetMod16(iconSize)); //Вибираємо місце куди писати іконку

				AesCryptoServiceProvider AES = GetFileAES(_IconIV);

				if ((icon != null) && (iconSize > 0))
				{
					dataFileStream.WriteEncrypt((long)iconStartPos, icon, AES);
				}

				_Name = Name;
				_ID = GenID();
				_ParentID = parent.ID;
				_IconStartPos = iconStartPos;
				_IconSize = iconSize;
				_PHash = GetPHash(Icon);
				Parent = parent;

				SaveInf();
			}
		}

		private UInt64 GetSize(UInt64 size)
		{
			UInt64 result = size;

			lock (_changeElementsLocker)
			{
				foreach (var element in _Elements)
				{
					result += element.Size;
				}
			}
			
			return result;
		}

		private void ReadElementParamsFromBuffer(byte[] buf)
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

		protected override void SaveInf()
		{
			byte[] UTF8Name = Encoding.UTF8.GetBytes(_Name);
			int realLength = DirInfLength + UTF8Name.Length;
			ushort newInfSize = Header.GetNewInfSizeByBufLength(realLength);

			byte[] buf = new byte[newInfSize];
			Buffer.BlockCopy(BitConverter.GetBytes(_IconStartPos), 0, buf, 0, 8);
			Buffer.BlockCopy(BitConverter.GetBytes(_IconSize), 0, buf, 8, 4);
			Buffer.BlockCopy(_PHash, 0, buf, 12, 8);
			Buffer.BlockCopy(BitConverter.GetBytes(_ParentID), 0, buf, 20, 8);
			Buffer.BlockCopy(BitConverter.GetBytes(_ID), 0, buf, 28, 8);
			Buffer.BlockCopy(BitConverter.GetBytes((UInt16)UTF8Name.Length), 0, buf, 36, 2);
			Buffer.BlockCopy(UTF8Name, 0, buf, 38, UTF8Name.Length);

			header.SaveInfo(buf, realLength);
		}

		public override void SaveTo(string PathToSave, SafeStreamAccess.ProgressCallback Progress = null)
		{
			ExportDir(PathToSave, Progress: Progress);
		}

		public override void SaveAs(string FullName, SafeStreamAccess.ProgressCallback Progress = null, Func<string, string> GetFileName = null)
		{
			ExportDir(Path.GetDirectoryName(FullName), Path.GetFileName(FullName), Progress: Progress, GetFileName: GetFileName);
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
				if (element is DirElement)
				{
					if (randomNames)
					{
						(element as DirElement).ExportDir(destPath + '\\' + tempName, GetFileName(Path.GetExtension(element.Name)), Progress: Progress, GetFileName: GetFileName);
					}
					else
					{
						(element as DirElement).ExportDir(destPath + '\\' + tempName, Progress: Progress);
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

		private FileElement AddFile(Stream stream, string destFileName, bool compressFile = false, Bitmap Icon = null, SafeStreamAccess.ProgressCallback Progress = null, bool isPrivate = true)
		{
			lock (_changeElementsLocker)
			{
				if (FindByName(_Elements, destFileName) != null)
				{
					throw new Exception("Файл або папка з таким ім’ям вже є.");
				}
			}

			FileElement file;

			try
			{
				file = new FileElement(this, header, dataFileStream, destFileName, stream, compressFile, _addElementLocker, _changeElementsLocker, Icon, Progress);
			}
			catch
			{
				throw new Exception("Файл або іконка не записались.");
			}

			return file;
		}

		public FileElement AddFile(Stream stream, string destFileName, bool compressFile = false, Bitmap Icon = null, SafeStreamAccess.ProgressCallback Progress = null)
		{
			return AddFile(stream, destFileName, compressFile, Icon, Progress, true);
		}

		public Element AddFile(string sourceFileName, string destFileName, bool compressFile = false, Bitmap Icon = null, SafeStreamAccess.ProgressCallback Progress = null)
		{
			try
			{
				using (FileStream f = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					return AddFile(f, destFileName, compressFile, Icon, Progress, true);
				}
			}
			catch
			{
				return null;
			}
		}

		protected override void ChangeParent(DirElement NewParent)
		{
			if ((NewParent == null) || (NewParent == this))
			{
				return;
			}

			lock (_changeElementsLocker)
			{
				//bool writeToFile = ((_Parent != NewParent) && (NewParent.ID != _ParentID));

				if (FindByName(NewParent._Elements, _Name) != null)
				{
					return;
				}

				if (FindSubDirByID(NewParent.ID) != null)
				{
					throw new Exception("Невірна вкладеність папок");
				}

				int index;
				if (_Parent != null)
				{
					if (FindByName((_Parent as DirElement)._Elements, _Name, out index) != null)
					{
						(_Parent as DirElement)._Elements.RemoveAt(index);
					}
				}

				bool writeToFile = _ParentID != NewParent.ID;
				_Parent = NewParent;
				_ParentID = NewParent.ID;

				if (FindByName((_Parent as DirElement)._Elements, _Name, out index) == null)
				{
					(_Parent as DirElement)._Elements.Insert(index, this);
				}
				else
				{
					throw new Exception("Елемент з такою назвою в цьому списку вже є!");
				}

				if (writeToFile)
				{
					SaveInf();
				}
			}
		}

		public void RefreshChildOrders()
		{
			lock (_changeElementsLocker)
			{
				_Elements.Sort(new NameComparer());
			}
		}

		protected override void Rename(string newName)
		{
			if ((_Parent == null) || (String.IsNullOrEmpty(newName)) || (newName == Name))
			{
				return;
			}

			lock (_changeElementsLocker)
			{
				Element duplicate = FindByName((_Parent as DirElement)._Elements, newName);
				if ((duplicate != null) && (duplicate != this))
				{
					return;
				}

				lock (_addElementLocker)
				{
					_Name = newName;
					(_Parent as DirElement).RefreshChildOrders(); //ускорити це!
					SaveInf();

					base.Rename(newName);
				}
			}
		}

		public bool RemoveElementFromElementsList(Element element)
		{
			lock (_changeElementsLocker)
			{
				int index;

				try
				{
					if (FindByName(_Elements, element.Name, out index) != null)
					{
						_Elements.RemoveAt(index);
					}
				}
				catch
				{
					return false;
				}
			}

			return true;
		}

		public bool InsertElementToElementsList(Element element)
		{
			lock (_changeElementsLocker)
			{
				int index;

				if (FindByName(_Elements, element.Name, out index) == null)
				{
					_Elements.Insert(index, element);
				}
				else
				{
					throw new Exception("Елемент з такою назвою в цьому списку вже є!");
				}
			}


			return true;
		}

		// Пошук в папці по імені файла
		public bool FileExists(string Name)
		{
			lock (_changeElementsLocker)
			{
				return _Elements.BinarySearch(new DirElement(Name), new NameComparer()) >= 0;
			}
		}

		//Пошук підпапки по ID
		private DirElement FindSubDirByID(UInt64 ID)
		{
			DirElement result;

			lock (_changeElementsLocker)
			{
				foreach (DirElement element in _Elements.Where(x => (x.Type == ElementType.Dir)))
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

		//Шукає в сортованому по Name списку
		private Element FindByName(List<Element> elements, string Name)
		{
			lock (_changeElementsLocker)
			{
				int index = elements.BinarySearch(new DirElement(Name), new NameComparer());
				Element result = index >= 0 ? elements[index] : null;

				return result;
			}
		}

		//Шукає в сортованому по Name списку
		private Element FindByName(List<Element> elements, string Name, out int index)
		{
			lock (_changeElementsLocker)
			{
				index = elements.BinarySearch(new DirElement(Name), new NameComparer());
				Element result = index >= 0 ? elements[index] : null;
				index = index < 0 ? Math.Abs(index) - 1 : index;

				return result;
			}
		}

		public List<Element> FindByName(string Name, bool FindAsTags = false, bool AllTagsRequired = false, bool FindInSubDirectories = true)
		{
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

					if ((element is DirElement) && (FindInSubDirectories))
					{
						(element as DirElement)._FindAsTags(resultList, tags, allTagsRequired, FindInSubDirectories);
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

					if ((element is DirElement) && (FindInSubDirectories))
					{
						(element as DirElement)._Find(resultList, Name, FindInSubDirectories);
					}
				}
			}
		}

		public DirElement CreateDir(string Name, Bitmap Icon)
		{
			int index;
			
			lock (_changeElementsLocker)
			{
				if (FindByName(_Elements, Name, out index) != null)
				{
					throw new Exception("Не можна створити папку. Файл з таким ім’ям вже є.");
				}
			}

			DirElement dir;

			try
			{
				dir = new DirElement(this, dataFileStream, Name, _addElementLocker, _changeElementsLocker, Icon);
			}
			catch
			{
				throw new Exception("Іконка папки не записалась.");
			}

			return dir;
		}

		public DirElement CreateDir(string Name)
		{
			return CreateDir(Name, null);
		}

		public List<Element> FindAllByIcon(Bitmap image, byte sensative = 0, bool FindInSubDirectories = true)
		{
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
					if (element.IconSize > 0)
					{
						if (ComparePHashes(element.PHash, pHash, sensative))
						{
							resultList.Add(element);
						}
					}

					if ((element is DirElement) && (FindInSubDirectories))
					{
						(element as DirElement)._FindAllByIcon(resultList, pHash, sensative, FindInSubDirectories);
					}
				}
			}
		}

		public List<Element> FindByHash(byte[] Hash, bool FindInSubDirectories = true)
		{
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
					if ((element is FileElement) && (Crypto.CompareHash((element as FileElement).Hash, Hash)))
					{
						resultList.Add(element);
					}

					if ((element is DirElement) && (FindInSubDirectories))
					{
						(element as DirElement)._FindByHash(resultList, Hash, FindInSubDirectories);
					}
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

		private bool _Delete()
		{
			try
			{
				header.DeleteAndWrite();
				dataFileStream.AddFreeSpace(_IconStartPos, Crypto.GetMod16(_IconSize));

				foreach (var element in _Elements)
				{
					if (element is FileElement)
					{
						(element as FileElement)._Delete();
					}
					else if (element is DirElement)
					{
						(element as DirElement)._Delete();
					}
				}

				_Elements.Clear();
			}
			catch
			{
				return false;
			}

			return true;
		}
	}
}
