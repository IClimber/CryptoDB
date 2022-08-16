using CryptoDataBase.CryptoContainer.Helpers;
using CryptoDataBase.CryptoContainer.Repositories;
using CryptoDataBase.CryptoContainer.Services;
using CryptoDataBase.CryptoContainer.Types;
using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
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
                Bitmap result = new Bitmap(stream);
                stream.Dispose();
                return result;
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
                //Якщо стара іконка видаляється, то зробити пошук в FreeSpaceMap і додавати туди вільне місце
                IconSizeInner = 0;
                IconStartPos = GenID();
                PHash = GetIconPHash(icon);

                if (icon != null)
                {
                    buf = GetIconBytes(icon);

                    IconSizeInner = (uint)buf.Length;
                    if (IconSizeInner == 0) //Вибираємо місце куди писати іконку
                    {
                        IconStartPos = GenID();
                    }
                    else
                    {
                        IconStartPos = DataRepository.GetFreeSpaceStartPos(MathHelper.GetMod16(IconSizeInner));
                        DataRepository.WriteEncrypt((long)IconStartPos, buf, IconIV);
                    }
                }

                SaveInf();
                NotifyPropertyChanged("Icon");

                buf = null;
            }
        }

        protected byte[] GetIconBytes(Bitmap icon)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    if (icon?.PixelFormat == PixelFormat.Format32bppArgb)
                    {
                        icon.Save(ms, ImageFormat.Png);
                    }
                    else
                    {
                        icon?.Save(ms, ImageFormat.Jpeg);
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

        protected static byte[] GetIconPHash(Bitmap icon)
        {
            if (icon == null)
            {
                byte[] phash = new byte[8];
                RandomHelper.GetBytes(phash);
                return phash;
            }

            try
            {
                return GetPHash(icon);
            }
            catch
            {
                return GetIconPHash(null);
            }
        }

        private static byte[] GetPHash(Bitmap bmp)
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
                        arr[i++] = (byte)(*ptr2++ * 0.11 + *ptr2++ * 0.59 + *ptr2++ * 0.3);
                        mid += arr[i - 1];
                    }
                    ptr += data.Stride;
                }

                mid = (ushort)(mid / 64);
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

        public static byte GetHammingDistance(ulong pHash1, ulong pHash2)
        {
            byte dist = 0;
            ulong val = pHash1 ^ pHash2;

            while (val > 0)
            {
                ++dist;
                val &= val - 1;
            }

            return dist;
        }

        public static bool ComparePHashes(byte[] pHash1, byte[] pHash2, byte sensative)
        {
            byte dist = GetHammingDistance(BitConverter.ToUInt64(pHash1, 0), BitConverter.ToUInt64(pHash2, 0));

            return dist <= sensative || dist >= 64 - sensative;
        }
    }
}