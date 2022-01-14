using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataBase.CDB.Repositories
{
	class HeaderStreamRepositoryV5 : HeaderRepository
	{
		const uint BLOCK_SIZE = 1048576;
		public const byte CURRENT_VERSION = 5;
		private static int threadsCount = Environment.ProcessorCount;
		private const long DATA_START_POS = 321;

		public HeaderStreamRepositoryV5(Stream stream, string password, byte[] aesKey = null) : base(stream)
		{
			byte[] key;
			AesCryptoServiceProvider kek = CreateKEK(password);

			if (IsNew(stream))
			{
				stream.Position = 0;
				stream.WriteByte(CURRENT_VERSION);
				key = WriteDEK(stream, kek, aesKey);
			}
			else
			{
				key = ReadDEK(stream, kek);
			}

			kek.Dispose();

			_dekAes = new AesCryptoServiceProvider();
			_dekAes.KeySize = 256;
			_dekAes.BlockSize = 128;
			_dekAes.Key = key;
			_dekAes.Mode = CipherMode.CBC;
			_dekAes.Padding = PaddingMode.None;
		}

		private AesCryptoServiceProvider CreateKEK(string password)
		{
			AesCryptoServiceProvider KEK = new AesCryptoServiceProvider();
			KEK.KeySize = 256;
			KEK.BlockSize = 128;
			KEK.Key = GetAesKeyByPassword(password);
			KEK.Mode = CipherMode.CBC;
			KEK.Padding = PaddingMode.None;

			return KEK;
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
			byte[] IV = new byte[16];
			stream.Position = 1;
			stream.Read(buf, 0, buf.Length);
			Buffer.BlockCopy(buf, 0, IV, 0, IV.Length);
			Buffer.BlockCopy(buf, 16, encryptedData, 0, encryptedData.Length);
			using (ICryptoTransform transform = kek.CreateDecryptor(kek.Key, IV))
			{
				decryptedData = Crypto.AesConvertBuf(encryptedData, encryptedData.Length, transform);
			}
			Buffer.BlockCopy(decryptedData, decryptedData[0] + 1, key, 0, key.Length);

			return key;
		}

		private byte[] WriteDEK(Stream stream, AesCryptoServiceProvider kek, byte[] defaultDek = null)
		{
			byte[] key = new byte[32];
			byte[] buf = new byte[320];
			byte[] decryptedData = new byte[304];
			byte[] IV = new byte[16];

			CryptoRandom.GetBytes(buf);
			Buffer.BlockCopy(buf, 0, IV, 0, IV.Length);
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
			using (ICryptoTransform transform = kek.CreateEncryptor(kek.Key, IV))
			{
				byte[] encryptedData = Crypto.AesConvertBuf(decryptedData, decryptedData.Length, transform);
				stream.Position = 1;
				stream.Write(IV, 0, IV.Length);
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
			if (position / BLOCK_SIZE < (position + size) / BLOCK_SIZE)
			{
				return (ulong)Math.Ceiling(position / (double)BLOCK_SIZE) * BLOCK_SIZE;
			}

			return position;
		}

		public override void ExportStructToFile(IList<Element> elements)
		{
			foreach (Element element in elements)
			{
				ushort rawSize = (ushort)(element.GetRawInfoLength() + Header.RAW_LENGTH);
				ulong position = GetStartPosBySize((ulong)_stream.Position, rawSize);
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
				WriteDEK(_stream, kek, _dekAes.Key);
			}
		}

		public override List<Header> ReadFileStruct(ProgressCallback Progress)
		{
			List<Header> headers = new List<Header>();
			double percent = 0;
			int lastProgress = 0;
			long length = _stream.Length;
			object locker = new object();
			object addLocker = new object();

			int blockCount = (int)Math.Ceiling(_stream.Length / (double)BLOCK_SIZE);
			Parallel.For(0, blockCount, new ParallelOptions { MaxDegreeOfParallelism = threadsCount }, i =>
			{
				MemoryStream headerStream = new MemoryStream();
				byte[] buf = new byte[BLOCK_SIZE];
				int count;
				lock (locker)
				{
					_stream.Position = i * BLOCK_SIZE;
					count = _stream.Read(buf, 0, buf.Length);
				}

				headerStream.Write(buf, 0, count);
				headerStream.Position = i == 0 ? DATA_START_POS : 0;
				while (headerStream.Position < headerStream.Length)
				{
					long lastPos = headerStream.Position;
					Header header = GetNextElementFromStream(headerStream, (ulong)(i * BLOCK_SIZE));
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
					if ((Progress != null) && (lastProgress != (int)percent1))
					{
						Progress(percent1, "Reading elements from file");
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
				if (header.InfSize + (ulong)Header.RAW_LENGTH + position > (ulong)stream.Length)
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
