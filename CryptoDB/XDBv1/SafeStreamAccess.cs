using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDataBase
{
	public class SafeStreamAccess
	{
		public delegate void ProgressCallback(double percent);
		private Object _ReadWriteLock = new Object();
		private Object _WriteLock = new Object();
		private Stream _stream;
		public Stream BaseStream { get { return _stream; } }
		public long Length { get { return Math.Max(_stream.Length, _Length); } }
		private long _Length;
		private Object _freeSpaceMapLocker = new Object();
		protected List<SPoint> FreeSpaceMap = new List<SPoint>();
		//private byte[] ecnryptBuffer = new byte[1048576];
		//private byte[] decryptBuffer = new byte[1048576];

		public SafeStreamAccess(Stream stream)
		{
			_stream = stream;
		}

		//Кодує файли, перед викликом не забути присвоїти потрібний IV
		public void WriteEncrypt(long streamOffset, Stream inputStream, AesCryptoServiceProvider AES, out byte[] Hash, ProgressCallback Progress)
		{
			lock (_WriteLock)
			{
				_Length = streamOffset + inputStream.Length - inputStream.Position; //Якщо файл кодується з доповненням, то тут може бути менше значення чим потрібно.

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

		//Кодує файли, перед викликом не забути присвоїти потрібний IV
		public void WriteEncrypt(long streamOffset, byte[] inputData, AesCryptoServiceProvider AES)
		{
			lock (_WriteLock)
			{
				_Length = streamOffset + inputData.Length;

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
			long max = (long)Element.GetMod16((ulong)dataSize);
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

		private int GetStartPos(UInt64 Size)
		{
			int index = -1;
			int min = 0,
				max = FreeSpaceMap.Count - 1;

			while (min <= max)
			{
				int mid = (min + max) / 2;
				if (Size == FreeSpaceMap[mid].Size)
				{
					return mid;// ++mid;
				}
				else if (Size < FreeSpaceMap[mid].Size)
				{
					max = mid - 1;
					index = mid;
				}
				else
				{
					min = mid + 1;
				}
			}
			return index;
		}

		public UInt64 GetStartPosAndSaveChange(UInt64 Size)
		{
			lock (_freeSpaceMapLocker)
			{
				//Вибираємо місце куди писати файл
				//Якщо розмір = 0, то генеримо любі цифри, так, як файл все одно не буде записуватись
				if (Size == 0)
				{
					return CryptoRandom.Random(UInt64.MaxValue - 2) + 2;
				}

				UInt64 result = (ulong)Length;
				int pos = GetStartPos(Size);
				if (pos >= 0)
				{
					result = FreeSpaceMap[pos].Start;
					FreeSpaceMap[pos] = new SPoint(FreeSpaceMap[pos].Start + Size, FreeSpaceMap[pos].Size - Size);
				}

				return result;
			}
		}
	}
}
