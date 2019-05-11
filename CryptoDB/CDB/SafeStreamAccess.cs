using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CryptoDataBase.CDB
{
	public class SafeStreamAccess
	{
		public delegate void ProgressCallback(double percent);
		public readonly Object writeLock = new Object();
		private Object _ReadWriteLock = new Object();
		private Object _WriteLock = new Object();
		private Stream _stream;
		public Stream BaseStream { get { return _stream; } }
		public long Length { get { return Math.Max(_stream.Length, _Length); } }
		private long _Length;
		private FreeSpaceMap freeSpaceMap;
		//private byte[] ecnryptBuffer = new byte[1048576];
		//private byte[] decryptBuffer = new byte[1048576];

		public SafeStreamAccess(Stream stream)
		{
			_stream = stream;
			freeSpaceMap = new FreeSpaceMap(stream.Length, false);
		}

		//Кодує файли, перед викликом не забути присвоїти потрібний IV
		public void WriteEncrypt(long streamOffset, Stream inputStream, AesCryptoServiceProvider AES, out byte[] Hash, ProgressCallback Progress)
		{
			lock (_WriteLock)
			{
				if (streamOffset > _stream.Length)
				{
					throw new Exception("");
				}

				if (AES.Padding == PaddingMode.None)
				{
					_Length = streamOffset + inputStream.Length - inputStream.Position; //Якщо файл кодується з доповненням, то тут може бути менше значення чим потрібно.
				}
				else
				{
					_Length = streamOffset + (long)Crypto.GetMod16((UInt64)(inputStream.Length - inputStream.Position));
				}

				byte[] buffer = new byte[1048576];
				CryptoStream cs = new CryptoStream(_stream, AES.CreateEncryptor(), CryptoStreamMode.Write);
				MD5 md5 = MD5.Create();
				long position = streamOffset;

				while (inputStream.Position < inputStream.Length)
				{
					int count = inputStream.Read(buffer, 0, buffer.Length);
					lock (_ReadWriteLock)
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

					Progress?.Invoke(inputStream.Position / (double)inputStream.Length * 100.0);
				}

				lock (_ReadWriteLock)
				{
					_stream.Position = position;
					cs.FlushFinalBlock();
				}

				Hash = md5.Hash;
			}
		}

		//Кодує і записує масив байт, перед викликом не забути присвоїти потрібний IV
		public void WriteEncrypt(long streamOffset, byte[] inputData, AesCryptoServiceProvider AES)
		{
			lock (_WriteLock)
			{
				if (AES.Padding == PaddingMode.None)
				{
					_Length = streamOffset + inputData.Length;
				}
				else
				{
					_Length = streamOffset + (long)Crypto.GetMod16((UInt64)inputData.Length);
				}

				lock (_ReadWriteLock)
				{
					CryptoStream cs = new CryptoStream(_stream, AES.CreateEncryptor(), CryptoStreamMode.Write);
					_stream.Position = streamOffset;
					cs.Write(inputData, 0, inputData.Length);
					cs.FlushFinalBlock();
				}
			}
		}

		//Записує нешифровані дані напряму в потік
		public void Write(long streamOffset, byte[] buffer, int offset, int count)
		{
			lock (_WriteLock)
			{
				_Length = streamOffset + count;

				lock (_ReadWriteLock)
				{
					_stream.Position = streamOffset;
					_stream.Write(buffer, offset, count);
				}
			}
		}

		//Записує нешифровані дані напряму в потік
		public void WriteByte(long streamOffset, byte value)
		{
			lock (_WriteLock)
			{
				_Length = streamOffset + 1;

				lock (_ReadWriteLock)
				{
					_stream.Position = streamOffset;
					_stream.WriteByte(value);
				}
			}
		}

		//Розкодувує файли, перед викликом не забути присвоїти потрібний IV
		//streamOffset	- _stream.Position
		//outputStream	- куди зберігати розшифровані дані
		//dataSize		- read count
		public void ReadDecrypt(long streamOffset, Stream outputStream, long dataSize, AesCryptoServiceProvider AES, ProgressCallback Progress)
		{
			byte[] buffer = new byte[1048576];
			CryptoStream cs = new CryptoStream(_stream, AES.CreateDecryptor(), CryptoStreamMode.Read);
			long max = dataSize;
			long position = streamOffset;
			while (max > 0)
			{
				lock (_ReadWriteLock)
				{
					_stream.Position = position;
					int count = cs.Read(buffer, 0, (int)Math.Min(buffer.Length, max));
					outputStream.Write(buffer, 0, count);
					position = _stream.Position;
					max -= count;
				}

				Progress?.Invoke((dataSize - max) / (double)dataSize * 100.0);
			}
		}

		//Розкодувує файли, перед викликом не забути присвоїти потрібний IV
		//streamOffset	- _stream.Position
		//outputData	- куди зберігати розшифровані дані
		//dataSize		- read count
		public void ReadDecrypt(long streamOffset, byte[] outputData, int dataSize, AesCryptoServiceProvider AES)
		{
			lock (_ReadWriteLock)
			{
				using (CryptoStream cs = new CryptoStream(_stream, AES.CreateDecryptor(), CryptoStreamMode.Read))
				{
					_stream.Position = streamOffset;
					cs.Read(outputData, 0, dataSize);
				}
			}
		}

		public void Read(long streamOffset, byte[] buffer, int offset, int count)
		{
			lock (_ReadWriteLock)
			{
				_stream.Position = streamOffset;
				_stream.Read(buffer, offset, count);
			}
		}

		public void Close()
		{
			_stream.Close();
		}

		public void MultithreadDecrypt(long streamOffset, Stream outputStream, long dataSize, AesCryptoServiceProvider AES, ProgressCallback Progress)
		{
			byte[] buffer = new byte[1048576];
			byte[] outputBuffer = new byte[buffer.Length];
			byte[] IV = AES.IV;
			long max = (long)Crypto.GetMod16((ulong)dataSize);
			long position = streamOffset;

			while (max > 0)
			{
				lock (_ReadWriteLock)
				{
					_stream.Position = position;
					bool lastBlock = (max - buffer.Length) <= 0;
					int length = _stream.Read(buffer, 0, (int)Math.Min(buffer.Length, max));
					MultithreadDecryptBufer(buffer, ref outputBuffer, length, AES, lastBlock, ref IV);
					outputStream.Write(outputBuffer, 0, outputBuffer.Length);
					position = _stream.Position;
					max -= length;
				}

				Progress?.Invoke((dataSize - max) / (double)dataSize * 100.0);
			}
		}

		private void MultithreadDecryptBufer(byte[] inputBuffer, ref byte[] outputBuffer, int lenght, AesCryptoServiceProvider AES, bool thisLastBlock, ref byte[] IV)
		{
			int blockSize = 65536;
			byte[] outBuffer = outputBuffer;
			int count = (int)Math.Ceiling(lenght / (double)blockSize);
			byte[] lastIV = IV;
			byte[] result = null;
			Object AESLock = new Object();

			Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => {
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

				if (thisLastBlock && (i == (count - 1)))
				{
					lock (AESLock)
					{
						AES.Padding = PaddingMode.ISO10126;
						transform = AES.CreateDecryptor(AES.Key, myIV);
					}
					int inputCount = Math.Min(blockSize, lenght - i * blockSize);
					byte[] decryptedData = transform.TransformFinalBlock(inputBuffer, i * blockSize, inputCount);
					result = new byte[(count - 1) * blockSize + decryptedData.Length];
					Buffer.BlockCopy(decryptedData, 0, result, (count - 1) * blockSize, decryptedData.Length);
				}
				else
				{
					lock (AESLock)
					{
						AES.Padding = PaddingMode.None;
						transform = AES.CreateDecryptor(AES.Key, myIV);
					}
					transform.TransformBlock(inputBuffer, i * blockSize, blockSize, outBuffer, i * blockSize);
				}
			});


			if (result != null)
			{
				Buffer.BlockCopy(outBuffer, 0, result, 0, (count - 1) * blockSize);
				outputBuffer = result;
			}

			Buffer.BlockCopy(inputBuffer, lenght - 16, lastIV, 0, 16);
		}

		public UInt64 GetFreeSpaceStartPos(UInt64 size, bool withWrite = true)
		{
			return freeSpaceMap.GetFreeSpacePos(size, Length, withWrite);
		}

		public void RemoveFreeSpace(ulong start, ulong length)
		{
			freeSpaceMap.RemoveFreeSpace(start, length);
		}

		public void AddFreeSpace(ulong start, ulong length)
		{
			freeSpaceMap.AddFreeSpace(start, length);
		}

		public void FreeSpaceAnalyse()
		{
			freeSpaceMap.FreeSpaceAnalyse((UInt64)Length);
		}

		public bool IsFreeSpace(ulong Start, ulong Size)
		{
			return freeSpaceMap.IsFreeSpace(Start, Size);
		}
	}
}
