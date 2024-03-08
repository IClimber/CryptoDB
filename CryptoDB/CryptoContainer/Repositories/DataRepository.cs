using CryptoDataBase.CryptoContainer.Services;
using CryptoDataBase.CryptoContainer.Types;
using System;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CryptoContainer.Repositories
{
    public class DataRepository : IDisposable
    {
        public readonly object WriteLock;

        protected MultithreadingStreamService StreamService;
        protected Stream BaseStream;
        protected AesCryptoServiceProvider DekAes;

        public DataRepository(Stream stream, AesCryptoServiceProvider aes)
        {
            BaseStream = stream;
            StreamService = new MultithreadingStreamService(stream);
            WriteLock = new object();

            DekAes = new AesCryptoServiceProvider
            {
                KeySize = aes.KeySize,
                BlockSize = aes.BlockSize,
                Key = aes.Key,
                Mode = aes.Mode,
                Padding = PaddingMode.ISO10126
            };
        }

        protected AesCryptoServiceProvider GetAesCryptoProvider(byte[] iv = null)
        {
            return new AesCryptoServiceProvider
            {
                KeySize = DekAes.KeySize,
                BlockSize = DekAes.BlockSize,
                Key = DekAes.Key,
                Mode = DekAes.Mode,
                Padding = DekAes.Padding,
                IV = iv
            };
        }

        public void MultithreadDecrypt(long streamOffset, Stream outputStream, long dataSize, byte[] iv, MultithreadingStreamService.ProgressCallback progress)
        {
            using (AesCryptoServiceProvider aes = GetAesCryptoProvider(iv))
            {
                StreamService.MultithreadDecrypt(streamOffset, outputStream, dataSize, aes, progress);
            }
        }

        public SPoint WriteEncrypt(Stream inputStream, byte[] iv, out byte[] hash, MultithreadingStreamService.ProgressCallback progress)
        {
            SPoint result;

            using (AesCryptoServiceProvider aes = GetAesCryptoProvider(iv))
            {
                result = StreamService.WriteEncrypt(inputStream, aes, out hash, progress);
            }

            return result;
        }

        public SPoint WriteEncrypt(byte[] inputData, byte[] iv)
        {
            SPoint result;

            using (AesCryptoServiceProvider aes = GetAesCryptoProvider(iv))
            {
                result = StreamService.WriteEncrypt(inputData, aes);
            }

            return result;
        }

        public void AddFreeSpace(ulong start, ulong length)
        {
            StreamService.AddFreeSpace(start, length);
        }

        public void RemoveFreeSpace(ulong start, ulong length)
        {
            StreamService.RemoveFreeSpace(start, length);
        }

        public bool IsFreeSpace(ulong start, ulong size)
        {
            return StreamService.IsFreeSpace(start, size);
        }

        public void Dispose()
        {
            BaseStream.Close();
            StreamService.Close();
            DekAes.Dispose();
        }
    }
}