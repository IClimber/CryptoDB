using CryptoDataBase.CryptoContainer.Helpers;
using CryptoDataBase.CryptoContainer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataBase.CryptoContainer.Repositories
{
    public class HeaderStreamRepositoryV5 : HeaderRepository
    {
        public const byte Version = 5;
        private readonly int _threadsCount = Environment.ProcessorCount;
        private const uint BlockSize = 1048576;
        private const long DataStartPos = 321;

        public HeaderStreamRepositoryV5(Stream stream, string password, byte[] aesKey = null) : base(stream)
        {
            byte[] key;
            //key encription key
            using (AesCryptoServiceProvider kek = CreateKEK(password))
            {
                if (IsNew(stream))
                {
                    stream.Position = 0;
                    stream.WriteByte(Version);
                    key = WriteDEK(stream, kek, aesKey);
                }
                else
                {
                    key = ReadDEK(stream, kek);
                }
            }

            DekAes = new AesCryptoServiceProvider
            {
                KeySize = 256,
                BlockSize = 128,
                Key = key,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.None
            };
        }

        private AesCryptoServiceProvider CreateKEK(string password)
        {
            return new AesCryptoServiceProvider
            {
                KeySize = 256,
                BlockSize = 128,
                Key = GetAesKeyByPassword(password),
                Mode = CipherMode.CBC,
                Padding = PaddingMode.None
            };
        }

        private byte[] GetAesKeyByPassword(string password)
        {
            SHA512 hash = SHA512.Create();
            byte[] salt = hash.ComputeHash(Encoding.UTF8.GetBytes(password));
            for (int i = 0; i < 500000; i++)
            {
                salt = hash.ComputeHash(salt);
            }

            var key = new Rfc2898DeriveBytes(password, salt, 300000);

            return key.GetBytes(32);
        }

        private byte[] ReadDEK(Stream stream, AesCryptoServiceProvider kek)
        {
            byte[] key = new byte[32];
            byte[] buf = new byte[320];
            byte[] encryptedData = new byte[304];
            byte[] decryptedData;
            byte[] iv = new byte[16];
            stream.Position = 1;
            stream.Read(buf, 0, buf.Length);
            Buffer.BlockCopy(buf, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(buf, 16, encryptedData, 0, encryptedData.Length);
            using (ICryptoTransform transform = kek.CreateDecryptor(kek.Key, iv))
            {
                decryptedData = CryptoHelper.AesConvertBuf(encryptedData, encryptedData.Length, transform);
            }
            Buffer.BlockCopy(decryptedData, decryptedData[0] + 1, key, 0, key.Length);

            return key;
        }

        private byte[] WriteDEK(Stream stream, AesCryptoServiceProvider kek, byte[] defaultDek = null)
        {
            byte[] key = new byte[32];
            byte[] buf = new byte[320];
            byte[] decryptedData = new byte[304];
            byte[] iv = new byte[16];

            RandomHelper.GetBytes(buf);
            Buffer.BlockCopy(buf, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(buf, 16, decryptedData, 0, decryptedData.Length);
            if (defaultDek == null)
            {
                Buffer.BlockCopy(decryptedData, decryptedData[0] + 1, key, 0, key.Length);
            }
            else
            {
                key = defaultDek;
                Buffer.BlockCopy(defaultDek, 0, decryptedData, decryptedData[0] + 1, key.Length);
            }
            using (ICryptoTransform transform = kek.CreateEncryptor(kek.Key, iv))
            {
                byte[] encryptedData = CryptoHelper.AesConvertBuf(decryptedData, decryptedData.Length, transform);
                stream.Position = 1;
                stream.Write(iv, 0, iv.Length);
                stream.Write(encryptedData, 0, encryptedData.Length);
            }

            return key;
        }

        private bool IsNew(Stream stream)
        {
            return stream.Length <= 1;
        }

        public override ulong GetStartPosBySize(ulong position, ushort size)
        {
            return position / BlockSize < (position + size) / BlockSize
                ? (ulong)Math.Ceiling(position / (double)BlockSize) * BlockSize
                : position;
        }

        public override void ExportStructToFile(IList<Element> elements)
        {
            foreach (Element element in elements)
            {
                ushort rawSize = (ushort)(element.GetRawInfoLength() + Header.RawLength);
                ulong position = GetStartPosBySize((ulong)BaseStream.Position, rawSize);
                element.ExportInfTo(this, position);
            }
        }

        public override bool CanChangePassword()
        {
            return true;
        }

        public override void ChangePassword(string newPassword)
        {
            using (var kek = CreateKEK(newPassword))
            {
                WriteDEK(BaseStream, kek, DekAes.Key);
            }
        }

        public override List<Header> ReadFileStruct(ProgressCallback progress)
        {
            List<Header> headers = new List<Header>();
            double percent = 0;
            int lastProgress = 0;
            long length = BaseStream.Length;
            object locker = new object();
            object addLocker = new object();

            int blockCount = (int)Math.Ceiling(BaseStream.Length / (double)BlockSize);
            Parallel.For(0, blockCount, new ParallelOptions { MaxDegreeOfParallelism = _threadsCount }, i =>
            {
                MemoryStream headerStream = new MemoryStream();
                byte[] buf = new byte[BlockSize];
                int count;
                lock (locker)
                {
                    BaseStream.Position = i * BlockSize;
                    count = BaseStream.Read(buf, 0, buf.Length);
                }

                headerStream.Write(buf, 0, count);
                headerStream.Position = i == 0 ? DataStartPos : 0;
                while (headerStream.Position < headerStream.Length)
                {
                    long lastPos = headerStream.Position;
                    Header header = GetNextElementFromStream(headerStream, (ulong)(i * BlockSize));
                    if (header != null)
                    {
                        lock (addLocker)
                        {
                            headers.Add(header);
                        }
                    }

                    //percents
                    percent += headerStream.Position - lastPos;
                    double percent1 = percent / (double)length * 100.0;
                    if ((progress != null) && (lastProgress != (int)percent1))
                    {
                        progress(percent1, "Reading elements from file");
                        lastProgress = (int)percent1;
                    }
                }

                headerStream.Dispose();
            });

            return headers;
        }

        private Header GetNextElementFromStream(Stream stream, ulong offset)
        {
            Header header = null;
            ulong position = (ulong)stream.Position;

            try
            {
                header = ReadHeader(stream, position + offset);
                if (header.InfSize + (ulong)Header.RawLength + position > (ulong)stream.Length)
                {
                    return null;
                }
            }
            catch (Exception)
            { }

            if (header != null)
            {
                if (header.Exists)
                {
                    return header;
                }
                else
                {
                    stream.Position += header.InfSize;
                }
            }

            return null;
        }
    }
}