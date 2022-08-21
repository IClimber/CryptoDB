using System;
using System.Collections;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace CryptoDataBase.CryptoContainer.Helpers
{
    public static class ImageHelper
    {

        public static byte[] GetBitmapPHash(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                byte[] phash = new byte[8];
                RandomHelper.GetBytes(phash);
                return phash;
            }

            try
            {
                return GetPHash(bitmap);
            }
            catch
            {
                return GetBitmapPHash(null);
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

        public static byte[] GetBytesFromBitmap(Bitmap bitmap)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    if (bitmap?.PixelFormat == PixelFormat.Format32bppArgb)
                    {
                        bitmap.Save(ms, ImageFormat.Png);
                    }
                    else
                    {
                        bitmap?.Save(ms, ImageFormat.Jpeg);
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

        public static Bitmap GetBitmapFromStream(Stream stream)
        {
            return new Bitmap(stream);
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