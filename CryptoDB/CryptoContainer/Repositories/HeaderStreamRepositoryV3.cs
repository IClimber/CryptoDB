using CryptoDataBase.CryptoContainer.Models;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoDataBase.CryptoContainer.Repositories
{
    public class HeaderStreamRepositoryV3 : HeaderRepository
    {
        public const byte Version = 3;

        public HeaderStreamRepositoryV3(Stream stream, string password, byte[] aesKey = null) : base(stream)
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
            return position;
        }

        public override void ExportStructToFile(IList<Element> elements)
        {
            BaseStream.WriteByte(Version);

            foreach (Element element in elements)
            {
                element.ExportInfTo(this, (ulong)BaseStream.Position);
            }
        }

        public override List<Header> ReadFileStruct(ProgressCallback progress)
        {
            List<Header> headers = new List<Header>();
            byte[] buf = new byte[1048576];
            double percent = 0;

            //Читаємо список файлів з диску в пам’ять
            MemoryStream headersStream = new MemoryStream();
            BaseStream.Position = 0;
            while (BaseStream.Position < BaseStream.Length)
            {
                int count = BaseStream.Read(buf, 0, buf.Length);
                headersStream.Write(buf, 0, count);

                progress?.Invoke(BaseStream.Position / (double)BaseStream.Length * 100.0, "Reading file list to memory");
            }

            headersStream.Position = 1;
            //Читаємо список файлів з пам’яті
            int lastProgress = 0;
            while (headersStream.Position < headersStream.Length)
            {
                Header header = GetNextElementFromStream(headersStream);
                if (header != null)
                {
                    headers.Add(header);
                }

                percent = headersStream.Position / (double)headersStream.Length * 100.0;
                if ((progress != null) && (lastProgress != (int)percent))
                {
                    progress(percent, "Reading elements from file");
                    lastProgress = (int)percent;
                }
            }

            headersStream.Dispose();

            return headers;
        }

        private Header GetNextElementFromStream(Stream stream)
        {
            Header header = ReadHeader(stream, (ulong)stream.Position);

            if (header.Exists)
            {
                return header;
            }
            else
            {
                stream.Position += header.InfSize;
            }

            return null;
        }
    }
}