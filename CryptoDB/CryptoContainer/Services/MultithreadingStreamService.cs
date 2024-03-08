using CryptoDataBase.CryptoContainer.Helpers;
using CryptoDataBase.CryptoContainer.Types;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CryptoDataBase.CryptoContainer.Services
{
    public class MultithreadingStreamService
    {
        public delegate void ProgressCallback(double percent);
        public long Length => Math.Max(_stream.Length, _length);

        private object _readWriteLock = new object();
        private object _writeLock = new object();
        private object _freeSpaceMapLocker = new object();
        private readonly Stream _stream;
        private long _length;
        private FreeSpaceMapService _freeSpaceMap;
        private int s_threadsCount = Environment.ProcessorCount;

        public MultithreadingStreamService(Stream stream)
        {
            _stream = stream;
            _freeSpaceMap = new FreeSpaceMapService((ulong)stream.Length);
        }

        //Encrypt and write data from stream
        public SPoint WriteEncrypt(Stream inputStream, AesCryptoServiceProvider aes, out byte[] hash, ProgressCallback progress)
        {
            lock (_writeLock)
            {
                ulong dataRealSize = (ulong)(inputStream.Length - inputStream.Position);
                long startPosition;

                if (aes.Padding != PaddingMode.None)
                {
                    dataRealSize = MathHelper.GetMod16(dataRealSize);
                }

                lock (_freeSpaceMapLocker)
                {
                    startPosition = (long)_freeSpaceMap.GetFreeSpacePos(dataRealSize, (ulong)Length);
                    _length = startPosition + (long)dataRealSize;
                }

                if (startPosition > _stream.Length)
                {
                    throw new Exception();
                }

                byte[] buffer = new byte[1048576];
                CryptoStream cs = new CryptoStream(_stream, aes.CreateEncryptor(), CryptoStreamMode.Write);
                MD5 md5 = MD5.Create();
                long position = startPosition;

                while (inputStream.Position < inputStream.Length)
                {
                    int count = inputStream.Read(buffer, 0, buffer.Length);
                    lock (_readWriteLock)
                    {
                        _stream.Position = position;
                        cs.Write(buffer, 0, count);
                        position = _stream.Position;
                    }

                    if (inputStream.Position < inputStream.Length)
                    {
                        md5.TransformBlock(buffer, 0, count, buffer, 0);
                    }
                    else
                    {
                        md5.TransformFinalBlock(buffer, 0, count);
                    }

                    progress?.Invoke(inputStream.Position / (double)inputStream.Length * 100.0);
                }

                lock (_readWriteLock)
                {
                    _stream.Position = position;
                    cs.FlushFinalBlock();
                }

                hash = md5.Hash;

                return new SPoint((ulong)startPosition, dataRealSize);
            }
        }

        //Encrypt and write data from array
        public SPoint WriteEncrypt(byte[] inputData, AesCryptoServiceProvider aes)
        {
            lock (_writeLock)
            {
                ulong dataRealSize = (ulong)inputData.Length;
                long startPosition;

                if (aes.Padding != PaddingMode.None)
                {
                    dataRealSize = MathHelper.GetMod16(dataRealSize);
                }

                lock (_freeSpaceMapLocker)
                {
                    startPosition = (long)_freeSpaceMap.GetFreeSpacePos(dataRealSize, (ulong)Length);
                    _length = startPosition + (long)dataRealSize;
                }

                lock (_readWriteLock)
                {
                    CryptoStream cs = new CryptoStream(_stream, aes.CreateEncryptor(), CryptoStreamMode.Write);
                    _stream.Position = startPosition;
                    cs.Write(inputData, 0, inputData.Length);
                    cs.FlushFinalBlock();
                }

                return new SPoint((ulong)startPosition, dataRealSize);
            }
        }

        //Записує нешифровані дані напряму в потік
        public void Write(long streamOffset, byte[] buffer, int offset, int count)
        {
            lock (_writeLock)
            {
                _length = streamOffset + count;

                lock (_readWriteLock)
                {
                    _stream.Position = streamOffset;
                    _stream.Write(buffer, offset, count);
                }
            }
        }

        //Записує нешифровані дані напряму в потік
        public void WriteByte(long streamOffset, byte value)
        {
            lock (_writeLock)
            {
                _length = streamOffset + 1;

                lock (_readWriteLock)
                {
                    _stream.Position = streamOffset;
                    _stream.WriteByte(value);
                }
            }
        }

        public void MultithreadDecrypt(long streamOffset, Stream outputStream, long dataSize, AesCryptoServiceProvider aes, ProgressCallback progress)
        {
            byte[] buffer = new byte[1048576];
            byte[] outputBuffer = new byte[buffer.Length];
            byte[] iv = aes.IV;
            long max = (long)MathHelper.GetMod16((ulong)dataSize);
            long position = streamOffset;

            while (max > 0)
            {
                lock (_readWriteLock)
                {
                    _stream.Position = position;
                    bool lastBlock = max - buffer.Length <= 0;
                    int length = _stream.Read(buffer, 0, (int)Math.Min(buffer.Length, max));
                    MultithreadDecryptBufer(buffer, ref outputBuffer, length, aes, lastBlock, ref iv);
                    outputStream.Write(outputBuffer, 0, outputBuffer.Length);
                    position = _stream.Position;
                    max -= length;
                }

                progress?.Invoke((dataSize - max) / (double)dataSize * 100.0);
            }
        }

        public void Close()
        {
            _stream.Close();
        }

        public void RemoveFreeSpace(ulong start, ulong length)
        {
            lock (_freeSpaceMapLocker)
            {
                _freeSpaceMap.RemoveFreeSpace(start, length);
            }
        }

        public void AddFreeSpace(ulong start, ulong length)
        {
            lock (_freeSpaceMapLocker)
            {
                _freeSpaceMap.AddFreeSpace(start, length);
            }
        }

        public bool IsFreeSpace(ulong start, ulong size)
        {
            return _freeSpaceMap.IsFreeSpace(start, size);
        }

        private void MultithreadDecryptBufer(byte[] inputBuffer, ref byte[] outputBuffer, int lenght, AesCryptoServiceProvider aes, bool thisLastBlock, ref byte[] iv)
        {
            int blockSize = 65536;
            byte[] outBuffer = outputBuffer;
            int count = (int)Math.Ceiling(lenght / (double)blockSize);
            byte[] lastIV = iv;
            byte[] result = null;
            object aesLock = new object();

            Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = s_threadsCount }, i =>
            {
                byte[] myIV = new byte[lastIV.Length];
                ICryptoTransform transform;

                if (i == 0)
                {
                    Buffer.BlockCopy(lastIV, 0, myIV, 0, lastIV.Length);
                }
                else
                {
                    Buffer.BlockCopy(inputBuffer, i * blockSize - 16, myIV, 0, 16);
                }

                if (thisLastBlock && i == count - 1)
                {
                    lock (aesLock)
                    {
                        aes.Padding = PaddingMode.ISO10126;
                        transform = aes.CreateDecryptor(aes.Key, myIV);
                    }
                    int inputCount = Math.Min(blockSize, lenght - i * blockSize);
                    byte[] decryptedData = transform.TransformFinalBlock(inputBuffer, i * blockSize, inputCount);
                    result = new byte[(count - 1) * blockSize + decryptedData.Length];
                    Buffer.BlockCopy(decryptedData, 0, result, (count - 1) * blockSize, decryptedData.Length);
                }
                else
                {
                    lock (aesLock)
                    {
                        aes.Padding = PaddingMode.None;
                        transform = aes.CreateDecryptor(aes.Key, myIV);
                    }
                    transform.TransformBlock(inputBuffer, i * blockSize, blockSize, outBuffer, i * blockSize);
                }

                transform.Dispose();
            });


            if (result != null)
            {
                Buffer.BlockCopy(outBuffer, 0, result, 0, (count - 1) * blockSize);
                outputBuffer = result;
            }

            Buffer.BlockCopy(inputBuffer, lenght - 16, lastIV, 0, 16);
        }
    }
}