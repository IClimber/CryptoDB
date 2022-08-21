using CryptoDataBase.CryptoContainer.Helpers;
using CryptoDataBase.CryptoContainer.Repositories;
using CryptoDataBase.CryptoContainer.Services;
using CryptoDataBase.CryptoContainer.Types;
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;

namespace CryptoDataBase.CryptoContainer.Models
{
    public abstract class Element : INotifyPropertyChanged
    {
        protected object AddElementLocker;
        protected object ChangeElementsLocker;
        public event PropertyChangedEventHandler PropertyChanged;
        protected Header Header;
        public abstract ElementType Type { get; }
        public abstract ulong Size { get; }
        public abstract ulong FullSize { get; }
        public abstract ulong FullEncryptSize { get; }
        public bool IsExists => Exists;
        protected bool Exists
        {
            get
            {
                return Header.Exists;
            }
            set
            {
                if (value) Header.Restore(); else Header.Delete();
            }
        }
        public Bitmap Icon { get { return GetIcon(); } set { SetIcon(value); } }
        public ulong IconStartPosition => IconStartPos;
        protected ulong IconStartPos;
        public uint IconSize => IconSizeInner;
        protected uint IconSizeInner;
        public byte[] IconPHash => PHash;
        protected byte[] PHash;
        protected byte[] IconIV
        {
            get
            {
                return _iconIV ?? (_iconIV = HashHelper.GetMD5(Header.IV));
            }
            set
            {
                _iconIV = value;
            }
        }
        private byte[] _iconIV;
        public ulong ParentId => ParentElementId;
        protected ulong ParentElementId;
        public abstract DirectoryElement Parent { get; set; }
        protected DirectoryElement ParentElement;
        public string Name { get { return ElementName; } set { Rename(value); } }
        protected string ElementName;
        public long TimeIndex { get { return (long)Header.StartPos; } }
        public string FullPath => GetPath();
        protected DataRepository DataRepository;

        protected Element()
        {

        }

        protected Element(Header header, DataRepository dataRepository, object addElementLocker, object changeElementsLocker)
        {
            Header = header;
            DataRepository = dataRepository;
            AddElementLocker = addElementLocker;
            ChangeElementsLocker = changeElementsLocker;
        }

        protected Element(object addElementLocker, object changeElementsLocker)
        {
            AddElementLocker = addElementLocker;
            ChangeElementsLocker = changeElementsLocker;
        }

        public abstract ushort GetRawInfoLength();

        public abstract void ExportInfTo(HeaderRepository stream, ulong position);

        public abstract void SaveTo(string pathToSave, MultithreadingStreamService.ProgressCallback progress = null);

        public abstract void SaveAs(string fullName, MultithreadingStreamService.ProgressCallback progress = null, Func<string, string> getFileName = null);

        public abstract bool SetVirtualParent(DirectoryElement newParent);

        public abstract bool Delete();

        public abstract bool Restore();

        protected abstract byte[] GetRawInfo();

        protected abstract void SaveInf();

        protected ulong GenID()
        {
            return RandomHelper.Random(ulong.MaxValue - 2) + 2;
        }

        private void NotifyPropertyChanged(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual void Rename(string newName)
        {
            NotifyPropertyChanged("Name");
        }

        private string GetPath()
        {
            string result = ParentElement?.FullPath + (ParentElement == null ? "" : "\\") + ParentElement?.Name;
            return result;
        }

        public Element GetRootDirectory()
        {
            return ParentElement == null ? this : ParentElement.GetRootDirectory();
        }

        private Bitmap GetIcon()
        {
            if (IconSize == 0)
            {
                return null;
            }

            if (IconIV == null)
            {
                IconIV = HashHelper.GetMD5(Header.IV);
            }

            MemoryStream stream = new MemoryStream();
            DataRepository.MultithreadDecrypt((long)IconStartPos, stream, IconSizeInner, IconIV, null);

            try
            {
                stream.Position = 0;
                var bitmap = ImageHelper.GetBitmapFromStream(stream);
                stream.Dispose();

                return bitmap;
            }
            catch
            {
                stream.Dispose();
                return null;
            }
        }

        private void SetIcon(Bitmap icon)
        {
            lock (AddElementLocker)
            {
                byte[] buf;
                uint oldIconSize = IconSizeInner;

                DataRepository.AddFreeSpace(IconStartPos, IconSizeInner);

                IconSizeInner = 0;
                IconStartPos = GenID();
                PHash = ImageHelper.GetBitmapPHash(icon);

                if (icon != null)
                {
                    buf = ImageHelper.GetBytesFromBitmap(icon);

                    IconSizeInner = buf == null ? 0 : (uint)buf.Length;
                    if (IconSizeInner > 0)
                    {
                        IconStartPos = DataRepository.WriteEncrypt(buf, IconIV).Start;
                    }
                }

                SaveInf();
                NotifyPropertyChanged("Icon");

                buf = null;
            }
        }
    }
}