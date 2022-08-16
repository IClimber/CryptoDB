using CryptoDataBase.CryptoContainer.Exceptions;
using CryptoDataBase.CryptoContainer.Helpers;
using CryptoDataBase.CryptoContainer.Models;
using CryptoDataBase.CryptoContainer.Services;
using CryptoDataBase.CryptoContainer.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CryptoContainer.Repositories
{
    public abstract class HeaderRepository : IDisposable
    {
        public readonly object WriteLock = new object();
        public delegate void ProgressCallback(double percent, string message);
        public abstract ulong GetStartPosBySize(ulong position, ushort size);
        public abstract List<Header> ReadFileStruct(ProgressCallback progress);
        public abstract void ExportStructToFile(IList<Element> elements);

        protected Stream BaseStream;
        protected MultithreadingStreamService SafeStream;
        protected AesCryptoServiceProvider DekAes;

        public HeaderRepository(Stream stream)
        {
            BaseStream = stream;
            SafeStream = new MultithreadingStreamService(stream);
        }

        public AesCryptoServiceProvider GetDek()
        {
            return new AesCryptoServiceProvider
            {
                KeySize = DekAes.KeySize,
                BlockSize = DekAes.BlockSize,
                Key = DekAes.Key,
                Mode = DekAes.Mode,
                Padding = DekAes.Padding
            };
        }

        public virtual bool CanChangePassword()
        {
            return false;
        }

        public virtual void ChangePassword(string newPassword)
        {
            throw new UnsupportedMethodException();
        }

        protected Header ReadHeader(Stream memoryStream, ulong startPos)
        {
            //Зчитуємо незакодовані дані, IV (16 байт) і Exists (1 байт)
            byte[] buf = new byte[17];
            memoryStream.Read(buf, 0, buf.Length);

            //Записуємо зчитані дані в відповідні параметри
            byte[] iv = new byte[16];
            Buffer.BlockCopy(buf, 0, iv, 0, 16);
            bool exists = buf[16] < 128;

            ICryptoTransform transform = DekAes.CreateDecryptor(DekAes.Key, iv);
            memoryStream.Read(buf, 0, 16);
            buf = CryptoHelper.AesConvertBuf(buf, 16, transform);

            ushort infSize = BitConverter.ToUInt16(buf, 13);
            ElementType elementType = (ElementType)(buf[15] / 128);

            if (!exists)
            {
                transform.Dispose();

                return new Header(this, startPos, iv, exists, elementType, infSize);
            }

            byte[] infDdata = new byte[infSize];
            memoryStream.Read(infDdata, 0, infDdata.Length);
            infDdata = CryptoHelper.AesConvertBuf(infDdata, infDdata.Length, transform);
            transform.Dispose();

            return new Header(this, startPos, iv, exists, elementType, infSize, infDdata);
        }

        public ulong GetEndPosition()
        {
            return (ulong)SafeStream.Length;
        }

        public void Write(long streamOffset, byte[] buffer, int offset, int count)
        {
            SafeStream.Write(streamOffset, buffer, offset, count);
        }

        public void WriteByte(long streamOffset, byte value)
        {
            SafeStream.WriteByte(streamOffset, value);
        }

        public void WriteEncrypt(long streamOffset, byte[] inputData, byte[] iv)
        {
            using (ICryptoTransform transform = DekAes.CreateEncryptor(DekAes.Key, iv))
            {
                byte[] buf = CryptoHelper.AesConvertBuf(inputData, inputData.Length, transform);
                SafeStream.Write(streamOffset, buf, 0, buf.Length);
            }
        }

        public void Dispose()
        {
            BaseStream.Close();
            SafeStream.Close();
            DekAes.Dispose();
        }
    }
}