using CryptoDataBase.CryptoContainer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataBase.CryptoContainer.Repositories
{
    public class HeaderStreamRepositoryV4 : HeaderRepository
    {
        public const byte Version = 4;
        private readonly int _threadsCount = Environment.ProcessorCount;
        private const uint BlockSize = 1048576;

        public HeaderStreamRepositoryV4(Stream stream, string password, byte[] aesKey = null) : base(stream)
        {
            DekAes = new AesCryptoServiceProvider
            {
                KeySize = 256,
                BlockSize = 128,
                Key = aesKey ?? GetAesKeyByPassword(password),
                Mode = CipherMode.CBC,
                Padding = PaddingMode.None
            };
        }

        private byte[] GetAesKeyByPassword(string password)
        {
            SHA256 hash = SHA256.Create();
            byte[] salt = hash.ComputeHash(Encoding.UTF8.GetBytes(password));
            for (int i = 0; i < 50000; i++)
            {
                salt = hash.ComputeHash(salt);
            }

            var key = new Rfc2898DeriveBytes(password, salt, 100000);

            return key.GetBytes(32);
        }

        public override ulong GetStartPosBySize(ulong position, ushort size)
        {
            return position / BlockSize < (position + size) / BlockSize
                ? (ulong)Math.Ceiling(position / (double)BlockSize) * BlockSize
                : position;
        }

        public override void ExportStructToFile(IList<Element> elements)
        {
            BaseStream.WriteByte(Version);

            foreach (Element element in elements)
            {
                ushort rawSize = (ushort)(element.GetRawInfoLength() + Header.RawLength);
                ulong position = GetStartPosBySize((ulong)BaseStream.Position, rawSize);
                element.ExportInfTo(this, position);
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
                headerStream.Position = i == 0 ? 1 : 0;
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