using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CryptoDataBase.CDB.Repositories
{
	public class HeaderStreamRepositoryV3: HeaderRepository
	{
		public const byte CURRENT_VERSION = 3;
		public HeaderStreamRepositoryV3(Stream stream, string password, byte[] aesKey = null) : base(stream)
		{
			_dekAes = new AesCryptoServiceProvider();
			_dekAes.KeySize = 256;
			_dekAes.BlockSize = 128;
			_dekAes.Key = aesKey ?? GetAesKeyByPassword(password);
			_dekAes.Mode = CipherMode.CBC;
			_dekAes.Padding = PaddingMode.None;
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
			_stream.WriteByte(CURRENT_VERSION);

			foreach (Element element in elements)
			{
				ushort rawSize = (ushort)(element.GetRawInfoLength() + Header.RAW_LENGTH);
				ulong position = (ulong)_stream.Position;
				element.ExportInfTo(this, position);
			}
		}

		public override List<Header> ReadFileStruct(ProgressCallback Progress)
		{
			List<Header> headers = new List<Header>();
			_stream.Position = 0;
			byte[] buf = new byte[1048576];
			double percent = 0;

			//Читаємо список файлів з диску в пам’ять
			MemoryStream headersStream = new MemoryStream();
			while (_stream.Position < _stream.Length)
			{
				int count = _stream.Read(buf, 0, buf.Length);
				headersStream.Write(buf, 0, count);

				Progress?.Invoke(_stream.Position / (double)_stream.Length * 100.0, "Reading file list to memory");
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
				if ((Progress != null) && (lastProgress != (int)percent))
				{
					Progress(percent, "Reading elements from file");
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
