using CryptoDataBase.CryptoContainer.Services;
using System;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CryptoContainer.Repositories
{
    public class DataRepository : IDisposable
    {
        public readonly object WriteLock;

        protected MultithreadingStreamService SafeStream;
        protected Stream BaseStream;
        protected AesCryptoServiceProvider DekAes;

        public DataRepository(Stream stream, AesCryptoServiceProvider aes)
        {
            BaseStream = stream;
            SafeStream = new MultithreadingStreamService(stream);
            WriteLock = SafeStream.WriteLock;

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
                SafeStream.MultithreadDecrypt(streamOffset, outputStream, dataSize, aes, progress);
            }
        }

        public void WriteEncrypt(long streamOffset, Stream inputStream, byte[] iv, out byte[] hash, MultithreadingStreamService.ProgressCallback progress)
        {
            using (AesCryptoServiceProvider aes = GetAesCryptoProvider(iv))
            {
                SafeStream.WriteEncrypt(streamOffset, inputStream, aes, out hash, progress);
            }
        }

        public void WriteEncrypt(long streamOffset, byte[] inputData, byte[] iv)
        {
            using (AesCryptoServiceProvider aes = GetAesCryptoProvider(iv))
            {
                SafeStream.WriteEncrypt(streamOffset, inputData, aes);
            }
        }

        public void FreeSpaceAnalyse()
        {
            SafeStream.FreeSpaceAnalyse();
        }

        public ulong GetFreeSpaceStartPos(ulong size, bool withWrite = true)
        {
            return SafeStream.GetFreeSpaceStartPos(size, withWrite);
        }

        public void AddFreeSpace(ulong start, ulong length)
        {
            SafeStream.AddFreeSpace(start, length);
        }

        public void RemoveFreeSpace(ulong start, ulong length)
        {
            SafeStream.RemoveFreeSpace(start, length);
        }

        public bool IsFreeSpace(ulong start, ulong size)
        {
            return SafeStream.IsFreeSpace(start, size);
        }

        public void Dispose()
        {
            BaseStream.Close();
            SafeStream.Close();
            DekAes.Dispose();
        }
    }
}