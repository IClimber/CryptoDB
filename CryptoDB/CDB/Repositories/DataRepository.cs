using System;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CDB.Repositories
{
	public class DataRepository : IDisposable
	{
		public readonly object writeLock;

		protected SafeStreamAccess _safeStream;
		protected Stream _stream;
		protected AesCryptoServiceProvider _aes;

		public DataRepository(Stream stream, AesCryptoServiceProvider aes)
		{
			_stream = stream;
			_safeStream = new SafeStreamAccess(stream);
			writeLock = _safeStream.writeLock;

			_aes = new AesCryptoServiceProvider();
			_aes.KeySize = aes.KeySize;
			_aes.BlockSize = aes.BlockSize;
			_aes.Key = aes.Key;
			_aes.Mode = aes.Mode;
			_aes.Padding = PaddingMode.ISO10126;
		}

		protected AesCryptoServiceProvider GetAesCryptoProvider(byte[] IV = null)
		{
			AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
			aes.KeySize = _aes.KeySize;
			aes.BlockSize = _aes.BlockSize;
			aes.Key = _aes.Key;
			aes.Mode = _aes.Mode;
			aes.Padding = _aes.Padding;
			aes.IV = IV;

			return aes;
		}

		public void MultithreadDecrypt(long streamOffset, Stream outputStream, long dataSize, byte[] IV, SafeStreamAccess.ProgressCallback Progress)
		{
			using (var aes = GetAesCryptoProvider(IV))
			{
				_safeStream.MultithreadDecrypt(streamOffset, outputStream, dataSize, aes, Progress);
			}
		}

		public void WriteEncrypt(long streamOffset, Stream inputStream, byte[] IV, out byte[] Hash, SafeStreamAccess.ProgressCallback Progress)
		{
			using (var aes = GetAesCryptoProvider(IV))
			{
				_safeStream.WriteEncrypt(streamOffset, inputStream, aes, out Hash, Progress);
			}
		}

		public void WriteEncrypt(long streamOffset, byte[] inputData, byte[] IV)
		{
			using (var aes = GetAesCryptoProvider(IV))
			{
				_safeStream.WriteEncrypt(streamOffset, inputData, aes);
			}
		}

		public void FreeSpaceAnalyse()
		{
			_safeStream.FreeSpaceAnalyse();
		}

		public UInt64 GetFreeSpaceStartPos(UInt64 size, bool withWrite = true)
		{
			return _safeStream.GetFreeSpaceStartPos(size, withWrite);
		}

		public void AddFreeSpace(ulong start, ulong length)
		{
			_safeStream.AddFreeSpace(start, length);
		}

		public void RemoveFreeSpace(ulong start, ulong length)
		{
			_safeStream.RemoveFreeSpace(start, length);
		}

		public bool IsFreeSpace(ulong Start, ulong Size)
		{
			return _safeStream.IsFreeSpace(Start, Size);
		}

		public void Dispose()
		{
			_stream.Close();
			_safeStream.Close();
			_aes.Dispose();
		}
	}
}
