using CryptoDataBase.CDB.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CDB.Repositories
{
	public abstract class HeaderRepository: IDisposable
	{
		public readonly object writeLock = new object();

		protected Stream _stream;
		protected SafeStreamAccess _safeStream;
		protected AesCryptoServiceProvider _dekAes;
		public delegate void ProgressCallback(double percent, string message);

		public abstract ulong GetStartPosBySize(ulong position, ushort size);

		public abstract List<Header> ReadFileStruct(ProgressCallback Progress);

		public abstract void ExportStructToFile(IList<Element> elements);

		public HeaderRepository(Stream stream)
		{
			_stream = stream;
			_safeStream = new SafeStreamAccess(stream);
		}

		public AesCryptoServiceProvider GetDek()
		{
			var aes = new AesCryptoServiceProvider();
			aes.KeySize = _dekAes.KeySize;
			aes.BlockSize = _dekAes.BlockSize;
			aes.Key = _dekAes.Key;
			aes.Mode = _dekAes.Mode;
			aes.Padding = _dekAes.Padding;

			return aes;
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
			UInt64 StartPos;
			byte[] IV;
			bool Exists;
			ElementType ElType;
			UInt16 InfSize;
			byte[] infDdata;

			//Зчитуємо незакодовані дані, IV (16 байт) і Exists (1 байт)
			byte[] buf = new byte[17];
			StartPos = startPos;
			memoryStream.Read(buf, 0, buf.Length);

			//Записуємо зчитані дані в відповідні параметри
			IV = new byte[16];
			Buffer.BlockCopy(buf, 0, IV, 0, 16);
			Exists = buf[16] < 128;

			ICryptoTransform transform = _dekAes.CreateDecryptor(_dekAes.Key, IV);
			memoryStream.Read(buf, 0, 16);
			buf = Crypto.AesConvertBuf(buf, 16, transform);

			InfSize = BitConverter.ToUInt16(buf, 13);
			ElType = (ElementType)(buf[15] / 128);

			if (!Exists)
			{
				transform.Dispose();

				return new Header(this, StartPos, IV, Exists, ElType, InfSize);
			}

			infDdata = new byte[InfSize];
			memoryStream.Read(infDdata, 0, infDdata.Length);
			infDdata = Crypto.AesConvertBuf(infDdata, infDdata.Length, transform);
			transform.Dispose();

			return new Header(this, StartPos, IV, Exists, ElType, InfSize, infDdata);
		}

		public ulong GetEndPosition()
		{
			return (ulong)_safeStream.Length;
		}

		public void Write(long streamOffset, byte[] buffer, int offset, int count)
		{
			_safeStream.Write(streamOffset, buffer, offset, count);
		}

		public void WriteByte(long streamOffset, byte value)
		{
			_safeStream.WriteByte(streamOffset, value);
		}

		public void WriteEncrypt(long streamOffset, byte[] inputData, byte[] IV)
		{
			using (ICryptoTransform transform = _dekAes.CreateEncryptor(_dekAes.Key, IV))
			{
				byte[] buf = Crypto.AesConvertBuf(inputData, inputData.Length, transform);
				_safeStream.Write(streamOffset, buf, 0, buf.Length);
			}
		}

		public void Dispose()
		{
			_stream.Close();
			_safeStream.Close();
			_dekAes.Dispose();
		}
	}
}
