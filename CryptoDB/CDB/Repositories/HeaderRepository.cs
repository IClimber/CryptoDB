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
		protected AesCryptoServiceProvider _aes;
		public delegate void ProgressCallback(double percent, string message);

		public abstract ulong GetStartPosBySize(ulong position, ushort size);

		public abstract List<Header> ReadFileStruct(ProgressCallback Progress);

		public abstract void ExportStructToFile(IList<Element> elements);

		public HeaderRepository(Stream stream, AesCryptoServiceProvider aes)
		{
			_stream = stream;
			_safeStream = new SafeStreamAccess(stream);
			_aes = aes;
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

		public void WriteEncrypt(long streamOffset, byte[] inputData, AesCryptoServiceProvider AES)
		{
			_safeStream.WriteEncrypt(streamOffset, inputData, AES);
		}

		public void Dispose()
		{
			_safeStream.Close();
		}
	}
}
