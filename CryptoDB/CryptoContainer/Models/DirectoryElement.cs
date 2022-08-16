using CryptoDataBase.CryptoContainer.Comparers;
using CryptoDataBase.CryptoContainer.Exceptions;
using CryptoDataBase.CryptoContainer.Helpers;
using CryptoDataBase.CryptoContainer.Repositories;
using CryptoDataBase.CryptoContainer.Services;
using CryptoDataBase.CryptoContainer.Types;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace CryptoDataBase.CryptoContainer.Models
{
    public class DirectoryElement : Element
    {
        public override ElementType Type => ElementType.Directory;
        public IList<Element> Elements => _elements.AsReadOnly();
        public override ulong Size => GetSize();
        public override ulong FullSize => GetFullSize();
        public override ulong FullEncryptSize => GetFullEncryptSize();
        public ulong Id => _id;
        public override DirectoryElement Parent { get { return ParentElement; } set { ChangeParent(value); } }
        private const int RawInfLength = 38;
        private List<Element> _elements;
        private ulong _id;

        protected DirectoryElement()
        {
            _elements = new List<Element>();
        }

        protected DirectoryElement(object addElementLocker, object changeElementsLocker) : base(addElementLocker, changeElementsLocker)
        {
            _elements = new List<Element>();
        }

        private DirectoryElement(string Name)
        {
            ElementName = Name;
        }

        public DirectoryElement(ulong ID)
        {
            _id = ID;
        }

        //Створення папки при читані з файлу
        public DirectoryElement(Header header, DataRepository datarRepository, object addElementLocker, object changeElementsLocker) : base(header, datarRepository, addElementLocker, changeElementsLocker)
        {
            _elements = new List<Element>();
            byte[] buf = header.GetInfoBuf();

            ReadElementParamsFromBuffer(buf);
        }

        //Створення папки вручну
        protected DirectoryElement(DirectoryElement parent, DataRepository dataRepository, string name, object addElementLocker, object changeElementsLocker, Bitmap icon = null) : this(addElementLocker, changeElementsLocker)
        {
            lock (AddElementLocker)
            {
                lock (dataRepository.WriteLock)
                {
                    Header = new Header(parent.Header.Repository, ElementType.Directory);
                    DataRepository = dataRepository;

                    byte[] iconBytes = GetIconBytes(icon);
                    uint iconSize = iconBytes == null ? 0 : (uint)iconBytes.Length;
                    //Вибираємо місце куди писати іконку
                    ulong iconStartPos = dataRepository.GetFreeSpaceStartPos(MathHelper.GetMod16(iconSize));

                    if (iconBytes != null && iconSize > 0)
                    {
                        dataRepository.WriteEncrypt((long)iconStartPos, iconBytes, IconIV);
                    }

                    ElementName = name;
                    _id = GenID();
                    ParentElementId = parent.Id;
                    IconStartPos = iconStartPos;
                    IconSizeInner = iconSize;
                    PHash = GetIconPHash(icon);
                    Parent = parent;

                    SaveInf();
                }
            }
        }

        private ulong GetSize()
        {
            ulong result = 0;

            lock (ChangeElementsLocker)
            {
                foreach (var element in _elements)
                {
                    result += element.Size;
                }
            }

            return result;
        }

        private ulong GetFullSize()
        {
            ulong result = IconSizeInner;

            lock (ChangeElementsLocker)
            {
                foreach (var element in _elements)
                {
                    result += element.FullSize;
                }
            }

            return result;
        }

        private ulong GetFullEncryptSize()
        {
            ulong result = MathHelper.GetMod16(IconSizeInner);

            lock (ChangeElementsLocker)
            {
                foreach (var element in _elements)
                {
                    result += element.FullEncryptSize;
                }
            }

            return result;
        }

        private void ReadElementParamsFromBuffer(byte[] buf)
        {
            PHash = new byte[8];

            IconStartPos = BitConverter.ToUInt64(buf, 0);
            IconSizeInner = BitConverter.ToUInt32(buf, 8);
            Buffer.BlockCopy(buf, 12, PHash, 0, 8);
            ParentElementId = BitConverter.ToUInt64(buf, 20);
            _id = BitConverter.ToUInt64(buf, 28);
            int lengthName = BitConverter.ToUInt16(buf, 36);
            ElementName = Encoding.UTF8.GetString(buf, 38, lengthName);
        }

        public override ushort GetRawInfoLength()
        {
            byte[] utf8Name = Encoding.UTF8.GetBytes(ElementName);
            int realLength = RawInfLength + utf8Name.Length;

            return Header.GetNewInfSizeByBufLength(realLength);
        }

        protected override byte[] GetRawInfo()
        {
            byte[] utf8Name = Encoding.UTF8.GetBytes(ElementName);
            int realLength = RawInfLength + utf8Name.Length;
            ushort newInfSize = Header.GetNewInfSizeByBufLength(realLength);

            byte[] buf = new byte[newInfSize];
            Buffer.BlockCopy(BitConverter.GetBytes(IconStartPos), 0, buf, 0, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(IconSizeInner), 0, buf, 8, 4);
            Buffer.BlockCopy(PHash, 0, buf, 12, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(ParentElementId), 0, buf, 20, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(_id), 0, buf, 28, 8);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)utf8Name.Length), 0, buf, 36, 2);
            Buffer.BlockCopy(utf8Name, 0, buf, 38, utf8Name.Length);

            RandomHelper.GetBytes(buf, realLength, buf.Length - realLength);

            return buf;
        }

        protected override void SaveInf()
        {
            Header.SaveInfo(GetRawInfo());
        }

        public override void ExportInfTo(HeaderRepository repository, ulong position)
        {
            Header.ExportInfoTo(repository, position, GetRawInfo());
        }

        public override void SaveTo(string pathToSave, MultithreadingStreamService.ProgressCallback progress = null)
        {
            ExportDirectory(pathToSave, progress: progress);
        }

        public override void SaveAs(string fullName, MultithreadingStreamService.ProgressCallback progress = null, Func<string, string> getFileName = null)
        {
            ExportDirectory(Path.GetDirectoryName(fullName), Path.GetFileName(fullName), progress: progress, getFileName: getFileName);
        }

        private void ExportDirectory(string destPath, string name = "", MultithreadingStreamService.ProgressCallback progress = null, Func<string, string> getFileName = null)
        {
            bool randomNames = getFileName != null;

            string tempName = name == "" ? ElementName : name;
            Directory.CreateDirectory(destPath + '\\' + tempName);
            Element[] elementList;

            lock (ChangeElementsLocker)
            {
                elementList = new Element[_elements.Count];
                _elements.CopyTo(elementList);
            }

            foreach (var element in elementList)
            {
                if (element is DirectoryElement)
                {
                    if (randomNames)
                    {
                        (element as DirectoryElement).ExportDirectory(destPath + '\\' + tempName, getFileName(Path.GetExtension(element.Name)), progress: progress, getFileName: getFileName);
                    }
                    else
                    {
                        (element as DirectoryElement).ExportDirectory(destPath + '\\' + tempName, progress: progress);
                    }
                }
                else
                {
                    if (randomNames)
                    {
                        element.SaveAs(destPath + '\\' + tempName + "\\" + getFileName(Path.GetExtension(element.Name)), progress);
                    }
                    else
                    {
                        element.SaveTo(destPath + '\\' + tempName, progress);
                    }
                }
            }
        }

        private FileElement AddFile(Stream stream, string destFileName, bool compressFile = false, Bitmap icon = null, MultithreadingStreamService.ProgressCallback progress = null, bool isPrivate = true)
        {
            lock (ChangeElementsLocker)
            {
                if (FindByName(_elements, destFileName) != null)
                {
                    throw new DuplicatesFileNameException();
                }
            }

            try
            {
                return new FileElement(this, Header, DataRepository, destFileName, stream, compressFile, AddElementLocker, ChangeElementsLocker, icon, progress);
            }
            catch
            {
                throw new DataWasNotWrittenException();
            }
        }

        public FileElement AddFile(Stream stream, string destFileName, bool compressFile = false, Bitmap icon = null, MultithreadingStreamService.ProgressCallback progress = null)
        {
            return AddFile(stream, destFileName, compressFile, icon, progress, true);
        }

        public Element AddFile(string sourceFileName, string destFileName, bool compressFile = false, Bitmap icon = null, MultithreadingStreamService.ProgressCallback progress = null)
        {
            using (FileStream f = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return AddFile(f, destFileName, compressFile, icon, progress, true);
            }
        }

        public override bool SetVirtualParent(DirectoryElement newParent)
        {
            if (newParent == null || newParent == this)
            {
                return false;
            }

            lock (ChangeElementsLocker)
            {
                if (FindByName(newParent._elements, ElementName) != null)
                {
                    return false;
                }

                if (FindSubDirectoryByID(newParent.Id) != null)
                {
                    throw new RecursiveFolderAttachmentException();
                }

                int index;
                if (ParentElement != null)
                {
                    if (FindByName(ParentElement._elements, ElementName, out index) != null)
                    {
                        ParentElement._elements.RemoveAt(index);
                    }
                }

                ParentElement = newParent;
                ParentElementId = newParent.Id;

                if (FindByName(ParentElement._elements, ElementName, out index) == null)
                {
                    ParentElement._elements.Insert(index, this);
                }
                else
                {
                    throw new DuplicatesFileNameException();
                }
            }

            return true;
        }

        private void ChangeParent(DirectoryElement newParent)
        {
            if (newParent == null || newParent == this)
            {
                return;
            }

            lock (ChangeElementsLocker)
            {
                if (FindByName(newParent._elements, ElementName) != null)
                {
                    return;
                }

                if (FindSubDirectoryByID(newParent.Id) != null)
                {
                    throw new RecursiveFolderAttachmentException();
                }

                int index;
                if (ParentElement != null)
                {
                    if (FindByName((ParentElement as DirectoryElement)._elements, ElementName, out index) != null)
                    {
                        (ParentElement as DirectoryElement)._elements.RemoveAt(index);
                    }
                }

                bool writeToFile = ParentElementId != newParent.Id;
                ParentElement = newParent;
                ParentElementId = newParent.Id;

                if (FindByName((ParentElement as DirectoryElement)._elements, ElementName, out index) == null)
                {
                    (ParentElement as DirectoryElement)._elements.Insert(index, this);
                }
                else
                {
                    throw new DuplicatesFileNameException();
                }

                if (writeToFile)
                {
                    SaveInf();
                }
            }
        }

        public void RefreshChildOrders()
        {
            lock (ChangeElementsLocker)
            {
                _elements.Sort(new NameComparer());
            }
        }

        protected override void Rename(string newName)
        {
            if (ParentElement == null || string.IsNullOrEmpty(newName) || newName == Name)
            {
                return;
            }

            lock (ChangeElementsLocker)
            {
                Element duplicate = FindByName((ParentElement as DirectoryElement)._elements, newName);
                if (duplicate != null && duplicate != this)
                {
                    return;
                }

                lock (DataRepository.WriteLock)
                {
                    ElementName = newName;
                    (ParentElement as DirectoryElement).RefreshChildOrders();
                    SaveInf();

                    base.Rename(newName);
                }
            }
        }

        public bool RemoveElementFromElementsList(Element element)
        {
            lock (ChangeElementsLocker)
            {
                try
                {
                    if (FindByName(_elements, element.Name, out int index) != null)
                    {
                        _elements.RemoveAt(index);
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
            lock (ChangeElementsLocker)
            {
                if (FindByName(_elements, element.Name, out int index) == null)
                {
                    _elements.Insert(index, element);
                }
                else
                {
                    throw new DuplicatesFileNameException();
                }
            }

            return true;
        }

        // Пошук в папці по імені файла
        public bool FileExists(string name)
        {
            lock (ChangeElementsLocker)
            {
                return _elements.BinarySearch(new DirectoryElement(name), new NameComparer()) >= 0;
            }
        }

        //Пошук підпапки по ID
        private DirectoryElement FindSubDirectoryByID(ulong id)
        {
            lock (ChangeElementsLocker)
            {
                foreach (DirectoryElement element in _elements.Where(x => x.Type == ElementType.Directory))
                {
                    if (element.Id == id)
                    {
                        return element;
                    }

                    DirectoryElement directory = element.FindSubDirectoryByID(id);
                    if (directory != null)
                    {
                        return directory;
                    }
                }
            }

            return null;
        }

        //Шукає в сортованому по Name списку
        public Element FindByName(string name)
        {
            lock (ChangeElementsLocker)
            {
                int index = _elements.BinarySearch(new DirectoryElement(name), new NameComparer());

                return index >= 0 ? _elements[index] : null;
            }
        }

        //Шукає в сортованому по Name списку
        private Element FindByName(List<Element> elements, string name)
        {
            lock (ChangeElementsLocker)
            {
                int index = elements.BinarySearch(new DirectoryElement(name), new NameComparer());

                return index >= 0 ? elements[index] : null;
            }
        }

        //Шукає в сортованому по Name списку
        private Element FindByName(List<Element> elements, string name, out int index)
        {
            lock (ChangeElementsLocker)
            {
                index = elements.BinarySearch(new DirectoryElement(name), new NameComparer());
                Element result = index >= 0 ? elements[index] : null;
                index = index < 0 ? Math.Abs(index) - 1 : index;

                return result;
            }
        }

        public List<Element> FindByName(string name, bool findAsTags = false, bool allTagsRequired = false, bool findInSubDirectories = true)
        {
            List<Element> result = new List<Element>();

            if (findAsTags)
            {
                string[] tags = name.Split(' ');
                FindAsTags(result, tags, allTagsRequired, findInSubDirectories);
            }
            else
            {
                Find(result, name, findInSubDirectories);
            }

            return result;
        }

        private void FindAsTags(List<Element> resultList, string[] tags, bool allTagsRequired, bool findInSubDirectories)
        {
            lock (ChangeElementsLocker)
            {
                foreach (var element in _elements)
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

                    if (element is DirectoryElement && findInSubDirectories)
                    {
                        (element as DirectoryElement).FindAsTags(resultList, tags, allTagsRequired, findInSubDirectories);
                    }
                }
            }
        }

        private void Find(List<Element> resultList, string name, bool findInSubDirectories)
        {
            lock (ChangeElementsLocker)
            {
                foreach (var element in _elements)
                {
                    if (element.Name.IndexOf(name, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    {
                        resultList.Add(element);
                    }

                    if (element is DirectoryElement && findInSubDirectories)
                    {
                        (element as DirectoryElement).Find(resultList, name, findInSubDirectories);
                    }
                }
            }
        }

        public DirectoryElement CreateDirectory(string name, Bitmap icon)
        {
            lock (ChangeElementsLocker)
            {
                if (FindByName(_elements, name, out int index) != null)
                {
                    throw new DuplicatesFileNameException();
                }
            }

            DirectoryElement directory;

            try
            {
                directory = new DirectoryElement(this, DataRepository, name, AddElementLocker, ChangeElementsLocker, icon);
            }
            catch
            {
                throw new DataWasNotWrittenException();
            }

            return directory;
        }

        public DirectoryElement CreateDirectory(string name)
        {
            return CreateDirectory(name, null);
        }

        public List<Element> FindAllByIcon(Bitmap image, byte sensative = 0, bool findInSubDirectories = true)
        {
            byte[] pHash = GetIconPHash(image);
            List<Element> result = new List<Element>();
            InnerFindAllByPHash(result, pHash, sensative, findInSubDirectories);

            return result;
        }

        public List<Element> FindAllByPHash(byte[] pHash, byte sensative = 0, bool findInSubDirectories = true)
        {
            List<Element> result = new List<Element>();
            InnerFindAllByPHash(result, pHash, sensative, findInSubDirectories);

            return result;
        }

        private void InnerFindAllByPHash(List<Element> resultList, byte[] pHash, byte sensative, bool findInSubDirectories)
        {
            lock (ChangeElementsLocker)
            {
                foreach (var element in _elements)
                {
                    if (element.IconSize > 0)
                    {
                        if (ComparePHashes(element.IconPHash, pHash, sensative))
                        {
                            resultList.Add(element);
                        }
                    }

                    if (element is DirectoryElement && findInSubDirectories)
                    {
                        (element as DirectoryElement).InnerFindAllByPHash(resultList, pHash, sensative, findInSubDirectories);
                    }
                }
            }
        }

        public List<Element> FindByHash(byte[] hash, bool findInSubDirectories = true)
        {
            List<Element> result = new List<Element>();
            InnerFindByHash(result, hash, findInSubDirectories);

            return result;
        }

        private void InnerFindByHash(List<Element> resultList, byte[] hash, bool findInSubDirectories)
        {
            lock (ChangeElementsLocker)
            {
                foreach (var element in _elements)
                {
                    if (element is FileElement && HashHelper.CompareHash((element as FileElement).Hash, hash))
                    {
                        resultList.Add(element);
                    }

                    if (element is DirectoryElement && findInSubDirectories)
                    {
                        (element as DirectoryElement).InnerFindByHash(resultList, hash, findInSubDirectories);
                    }
                }
            }
        }

        public override bool Delete()
        {
            lock (ChangeElementsLocker)
            {
                if (InnerDelete())
                {
                    if (ParentElement != null)
                    {
                        Parent.RemoveElementFromElementsList(this);
                    }

                    return true;
                }

                return false;
            }
        }

        private bool InnerDelete()
        {
            try
            {
                Header.Delete();
                DataRepository.AddFreeSpace(IconStartPos, MathHelper.GetMod16(IconSizeInner));

                foreach (var element in _elements)
                {
                    if (element is FileElement)
                    {
                        (element as FileElement).InnerDelete();
                    }
                    else if (element is DirectoryElement)
                    {
                        (element as DirectoryElement).InnerDelete();
                    }
                }

                _elements.Clear();
            }
            catch
            {
                return false;
            }

            return true;
        }

        public override bool Restore()
        {
            throw new NotImplementedException();
        }
    }
}