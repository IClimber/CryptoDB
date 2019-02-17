using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CDB
{
	public abstract class Element : INotifyPropertyChanged
	{
		protected Object _addElementLocker;
		protected Object _changeElementsLocker;
		public event PropertyChangedEventHandler PropertyChanged;
		protected Header header;
		public abstract ElementType Type { get; }
		public abstract UInt64 Size { get; }
		public bool Exists { get { return _Exists; } }
		protected bool _Exists { get { return header.Exists; } set { if (value) header.Restore(); else header.Delete(); } }
		public Bitmap Icon { get { return GetIcon(); } set { SetIcon(value); } }
		public UInt64 IconStartPos { get { return _IconStartPos; } }
		protected UInt64 _IconStartPos;
		public UInt32 IconSize { get { return _IconSize; } }
		protected UInt32 _IconSize;
		public byte[] PHash { get { return _PHash; } }
		protected byte[] _PHash;
		protected byte[] _IconIV { get { return __IconIV == null ? (__IconIV = Crypto.GetMD5(header.IV)) : __IconIV; } set { __IconIV = value; } }
		private byte[] __IconIV;
		public UInt64 ParentID { get { return _ParentID; } }
		protected UInt64 _ParentID;
		public abstract DirElement Parent { get; set; }
		protected DirElement _Parent;

		public string Name { get { return _Name; } set { Rename(value); } }
		protected string _Name;
		public long TimeIndex { get { return (long)header.StartPos; } }
		public string FullPath { get { return _GetPath(); } }
		protected SafeStreamAccess dataFileStream;

		private void NotifyPropertyChanged(String propertyName = "")
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		protected Element()
		{

		}

		protected Element(Header header, SafeStreamAccess dataFileStream, Object addElementLocker, Object changeElementsLocker)
		{
			this.header = header;
			this.dataFileStream = dataFileStream;
			_addElementLocker = addElementLocker;
			_changeElementsLocker = changeElementsLocker;
		}

		protected Element(Object addElementLocker, Object changeElementsLocker)
		{
			_addElementLocker = addElementLocker;
			_changeElementsLocker = changeElementsLocker;
		}

		protected abstract void SaveInf();

		protected UInt64 GenID()
		{
			return CryptoRandom.Random(UInt64.MaxValue - 2) + 2;
		}

		protected AesCryptoServiceProvider GetFileAES(byte[] IV)
		{
			AesCryptoServiceProvider AES = new AesCryptoServiceProvider();
			AES.KeySize = header.AES.KeySize;
			AES.BlockSize = header.AES.BlockSize;
			AES.Key = header.AES.Key;
			AES.Mode = header.AES.Mode;
			AES.IV = IV;
			AES.Padding = PaddingMode.ISO10126;

			return AES;
		}

		protected virtual void Rename(string NewName)
		{
			NotifyPropertyChanged("Name");
		}

		private string _GetPath()
		{
			string result = _Parent?.FullPath + (_Parent == null ? "" : "\\") + _Parent?.Name;
			return result;
		}

		//protected abstract void ChangeParent(DirElement NewParent);

		public abstract void SaveTo(string PathToSave, SafeStreamAccess.ProgressCallback Progress = null);

		public abstract void SaveAs(string FullName, SafeStreamAccess.ProgressCallback Progress = null, Func<string, string> GetFileName = null);

		private Bitmap GetIcon()
		{
			if (IconSize == 0)
			{
				return null;
			}

			if (_IconIV == null)
			{
				_IconIV = Crypto.GetMD5(header.IV);
			}

			MemoryStream stream = new MemoryStream();

			AesCryptoServiceProvider AES = new AesCryptoServiceProvider();
			AES.KeySize = header.AES.KeySize;
			AES.BlockSize = header.AES.BlockSize;
			AES.Key = header.AES.Key;
			AES.Mode = header.AES.Mode;
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
			lock (_addElementLocker)
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

						_IconStartPos = dataFileStream.GetFreeSpaceStartPos(Crypto.GetMod16(_IconSize));

						AesCryptoServiceProvider AES = GetFileAES(_IconIV);
						dataFileStream.WriteEncrypt((long)_IconStartPos, buf, AES);
					}
				}

				SaveInf();
				NotifyPropertyChanged("Icon");

				buf = null;
			}
		}

		protected byte[] GetIconBytes(Bitmap Icon)
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

		protected static byte[] GetPHash(Bitmap Icon)
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

		protected static bool ComparePHashes(byte[] PHash1, byte[] PHash2, byte sensative)
		{
			byte dist = GetHammingDistance(BitConverter.ToUInt64(PHash1, 0), BitConverter.ToUInt64(PHash2, 0));
			return (dist <= sensative) || (dist >= (64 - sensative));
		}

		public abstract bool Delete();
	}
}