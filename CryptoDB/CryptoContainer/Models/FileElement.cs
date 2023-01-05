using CryptoDataBase.CryptoContainer.Exceptions;
using CryptoDataBase.CryptoContainer.Helpers;
using CryptoDataBase.CryptoContainer.Repositories;
using CryptoDataBase.CryptoContainer.Services;
using CryptoDataBase.CryptoContainer.Types;
using System;
using System.Drawing;
using System.IO;
using System.Text;

namespace CryptoDataBase.CryptoContainer.Models
{
    public class FileElement : Element
    {
        public const int RawInfLength = 63;
        public override ElementType Type => ElementType.File;
        public override ulong Size => GetSize();
        public override ulong FullSize => GetFullSize();
        public override ulong FullEncryptSize => GetFullEncryptSize();
        public byte[] Hash => _hash;
        public bool IsCompressed => _isCompressed;
        public ulong FileStartPos => _fileStartPos;
        public override DirectoryElement Parent { get { return ParentElement; } set { ChangeParent(value); } }

        private byte[] _fileIV { get { return _innerFileIV == null ? (_innerFileIV = HashHelper.GetMD5(IconIV)) : _innerFileIV; } set { _innerFileIV = value; } }
        private byte[] _innerFileIV;
        private ulong _fileSize;
        private byte[] _hash;
        private bool _isCompressed;
        private ulong _fileStartPos;

        //Створення файлу при читані з файлу
        public FileElement(Header header, DataRepository dataRepository, object addElementLocker, object changeElementsLocker) : base(header, dataRepository, addElementLocker, changeElementsLocker)
        {
            ReadElementParamsFromBuffer(header.GetInfoBuf());
        }

        //Створення файлу вручну
        public FileElement(DirectoryElement parent, Header parentHeader, DataRepository dataRepository, string name, Stream fileStream, bool isCompressed,
            object addElementLocker, object changeElementsLocker, Bitmap icon = null, MultithreadingStreamService.ProgressCallback progress = null) : base(addElementLocker, changeElementsLocker)
        {
            lock (AddElementLocker)
            {
                ulong fileSize = (ulong)fileStream.Length;

                byte[] iconBytes = ImageHelper.GetBytesFromBitmap(icon);
                uint iconSize = iconBytes == null ? 0 : (uint)iconBytes.Length;

                ulong fileStartPos = GenID();
                ulong iconStartPos = GenID();

                Header = new Header(parentHeader.Repository, ElementType.File);

                if (fileSize > 0)
                {
                    fileStartPos = dataRepository.WriteEncrypt(fileStream, _fileIV, out _hash, progress).Start;
                }
                else
                {
                    _hash = new byte[16];
                    RandomHelper.GetBytes(_hash);
                }

                if (iconSize > 0)
                {
                    iconStartPos = dataRepository.WriteEncrypt(iconBytes, IconIV).Start;
                }

                lock (dataRepository.WriteLock)
                {
                    DataRepository = dataRepository;

                    ElementName = name;
                    ParentElementId = parent.Id;
                    _fileStartPos = fileStartPos;
                    _fileSize = fileSize;
                    IconStartPos = iconStartPos;
                    IconSizeInner = iconSize;
                    _isCompressed = isCompressed;
                    PHash = ImageHelper.GetBitmapPHash(icon);
                    Exists = true;

                    SaveInf();
                }

                //Закидаємо файл в потрібну папку і записуємо зміни
                ChangeParent(parent, true);
            }
        }

        //for read from file
        private void ReadElementParamsFromBuffer(byte[] buf)
        {
            _hash = new byte[16];
            PHash = new byte[8];

            _fileStartPos = BitConverter.ToUInt64(buf, 0);
            IconStartPos = BitConverter.ToUInt64(buf, 8);
            _fileSize = BitConverter.ToUInt64(buf, 16);
            IconSizeInner = BitConverter.ToUInt32(buf, 24);
            _isCompressed = buf[28] < 128;
            Buffer.BlockCopy(buf, 29, _hash, 0, 16);
            Buffer.BlockCopy(buf, 45, PHash, 0, 8);
            ParentElementId = BitConverter.ToUInt64(buf, 53);
            int lengthName = BitConverter.ToUInt16(buf, 61);
            ElementName = Encoding.UTF8.GetString(buf, 63, lengthName);
        }

        public override ushort GetRawInfoLength()
        {
            byte[] UTF8Name = Encoding.UTF8.GetBytes(ElementName);
            int realLength = RawInfLength + UTF8Name.Length;

            return Header.GetNewInfSizeByBufLength(realLength);
        }

        protected override byte[] GetRawInfo()
        {
            byte[] utf8Name = Encoding.UTF8.GetBytes(ElementName);
            int realLength = RawInfLength + utf8Name.Length;
            ushort newInfSize = Header.GetNewInfSizeByBufLength(realLength);

            byte[] buf = new byte[newInfSize];
            Buffer.BlockCopy(BitConverter.GetBytes(_fileStartPos), 0, buf, 0, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(IconStartPos), 0, buf, 8, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(_fileSize), 0, buf, 16, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(IconSizeInner), 0, buf, 24, 4);
            byte isCompressed = (byte)(Convert.ToByte(!_isCompressed) * 128 + (int)RandomHelper.Random(128));
            Buffer.BlockCopy(BitConverter.GetBytes(isCompressed), 0, buf, 28, 1);
            Buffer.BlockCopy(_hash, 0, buf, 29, 16);
            Buffer.BlockCopy(PHash, 0, buf, 45, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(ParentElementId), 0, buf, 53, 8);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)utf8Name.Length), 0, buf, 61, 2);
            Buffer.BlockCopy(utf8Name, 0, buf, 63, utf8Name.Length);

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

        private ulong GetSize()
        {
            return _fileSize;
        }

        private ulong GetFullSize()
        {
            return _fileSize + IconSize;
        }

        private ulong GetFullEncryptSize()
        {
            return MathHelper.GetMod16(_fileSize) + MathHelper.GetMod16(IconSize);
        }

        public void SaveTo(Stream stream, MultithreadingStreamService.ProgressCallback progress = null)
        {
            if (_fileSize == 0)
            {
                return;
            }

            DataRepository.MultithreadDecrypt((long)_fileStartPos, stream, (long)_fileSize, _fileIV, progress);
        }

        public override void SaveTo(string pathToSave, MultithreadingStreamService.ProgressCallback progress = null)
        {
            Directory.CreateDirectory(pathToSave);
            using (FileStream stream = new FileStream(pathToSave + '\\' + ElementName, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                SaveTo(stream, progress);
            }

        }

        public override void SaveAs(string fullName, MultithreadingStreamService.ProgressCallback progress = null, Func<string, string> getFileName = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullName));
            using (FileStream stream = new FileStream(fullName, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                SaveTo(stream, progress);
            }
        }

        protected override void Rename(string newName)
        {
            if (ParentElement == null || string.IsNullOrEmpty(newName) || newName == Name)
            {
                return;
            }

            if ((ParentElement as DirectoryElement).FileExists(newName))
            {
                throw new DuplicatesFileNameException();
            }

            lock (DataRepository.WriteLock)
            {
                lock (ChangeElementsLocker)
                {
                    ElementName = newName;
                    (ParentElement as DirectoryElement).RefreshChildOrders();
                    SaveInf();
                }

                base.Rename(newName);
            }
        }

        public void ChangeContent(Stream newData, MultithreadingStreamService.ProgressCallback progress = null)
        {
            lock (AddElementLocker)
            {
                ulong fileSize = (ulong)newData.Length;

                byte[] newDataHash;

                try
                {
                    DataRepository.AddFreeSpace(_fileStartPos, _fileSize);

                    if (fileSize > 0)
                    {
                        _fileStartPos = DataRepository.WriteEncrypt(newData, _fileIV, out newDataHash, progress).Start;
                    }
                    else
                    {
                        newDataHash = new byte[16];
                        RandomHelper.GetBytes(newDataHash);
                    }
                }
                catch
                {
                    throw new DataWasNotWrittenException();
                }

                try
                {
                    _fileSize = fileSize;
                    _hash = newDataHash;
                    SaveInf();
                }
                catch
                {
                    throw new HeaderWasNotWrittenException();
                }
            }
        }

        public override bool SetVirtualParent(DirectoryElement newParent)
        {
            if (newParent == null)
            {
                return false;
            }

            lock (ChangeElementsLocker)
            {
                if (newParent.FileExists(ElementName))
                {
                    return false;
                }

                if (ParentElement != null)
                {
                    ParentElement.RemoveElementFromElementsList(this);
                }

                ParentElement = newParent;
                ParentElementId = newParent.Id;

                ParentElement.InsertElementToElementsList(this);
            }

            return true;
        }

        private void ChangeParent(DirectoryElement newParent, bool withWrite = false)
        {
            if (newParent == null)
            {
                return;
            }

            lock (ChangeElementsLocker)
            {
                if (newParent.FileExists(ElementName))
                {
                    return;
                }

                if (ParentElement != null)
                {
                    (ParentElement as DirectoryElement).RemoveElementFromElementsList(this);
                }

                bool writeToFile = ParentElementId != newParent.Id || withWrite;
                ParentElement = newParent;
                ParentElementId = newParent.Id;

                (ParentElement as DirectoryElement).InsertElementToElementsList(this);

                if (writeToFile)
                {
                    SaveInf();
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

        public bool InnerDelete()
        {
            try
            {
                Header.Delete();
                DataRepository.AddFreeSpace(_fileStartPos, MathHelper.GetMod16(_fileSize));
                DataRepository.AddFreeSpace(IconStartPos, MathHelper.GetMod16(IconSizeInner));
            }
            catch
            {
                return false;
            }

            return true;
        }

        public override bool Restore()
        {
            if (Exists)
            {
                return true;
            }

            if ((_fileSize == 0 || DataRepository.IsFreeSpace(_fileStartPos, _fileSize)) && (IconSizeInner == 0 || DataRepository.IsFreeSpace(IconStartPos, IconSizeInner)))
            {
                try
                {
                    Parent = ParentElement;
                    Exists = true;
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