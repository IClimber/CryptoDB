using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices.ComTypes;
using System.IO;
using System.Runtime.InteropServices;

namespace CryptoDataBase
{
	class VirtualFileStream : IStream
	{
		private const int ChunkSize = 4096;
		private string _EncryptedVideoFilePath;
		private Stream _EncryptedVideoFile;
		private int[] _EncryptedChunkLength;
		private long[] _EncryptedChunkPosition;
		private int[] _SourceChunkLength;
		private int _ChunkCount;
		private byte[] _CurrentChunk = new byte[ChunkSize];
		private long _CurrentChunkIndex = -1;
		private long _Position = 0;
		private long _Length;
		private Object _Lock = new Object();

		public VirtualFileStream(string EncryptedVideoFilePath)
		{
			_EncryptedVideoFilePath = EncryptedVideoFilePath;
			_EncryptedVideoFile = File.Open(EncryptedVideoFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

			// Read chunk data
			using (Stream EncryptedVideoFileStream = File.Open(EncryptedVideoFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using (BinaryReader Reader = new BinaryReader(EncryptedVideoFileStream))
				{
					_Length = 0;

					EncryptedVideoFileStream.Position = _EncryptedVideoFile.Length - sizeof(int);
					_ChunkCount = Reader.ReadInt32();

					EncryptedVideoFileStream.Position = _EncryptedVideoFile.Length - sizeof(int) - _ChunkCount * sizeof(int) - _ChunkCount * sizeof(int);

					_EncryptedChunkLength = new int[_ChunkCount];
					_SourceChunkLength = new int[_ChunkCount];
					_EncryptedChunkPosition = new long[_ChunkCount];

					for (int i = 0; i < _ChunkCount; i++)
					{
						_SourceChunkLength[i] = Reader.ReadInt32();
						_Length += _SourceChunkLength[i];
					}

					long Offset = 0;

					for (int i = 0; i < _ChunkCount; i++)
					{
						_EncryptedChunkLength[i] = Reader.ReadInt32();
						_EncryptedChunkPosition[i] = Offset;

						Offset += _EncryptedChunkLength[i];
					}
				}
			}
		}

		public void Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition)
		{
			SeekOrigin Origin = (SeekOrigin)dwOrigin;

			// Let's protect _Position: _Position might be changed by Read()
			lock (_Lock)
			{
				switch (Origin)
				{
					case SeekOrigin.Begin:
						{
							_Position = dlibMove;
							break;
						}
					case SeekOrigin.Current:
						{
							_Position += dlibMove;
							break;
						}
					case SeekOrigin.End:
						{
							_Position = _Length + dlibMove;
							break;
						}
				}
			}

			if (IntPtr.Zero != plibNewPosition)
				Marshal.WriteInt64(plibNewPosition, _Position);
		}

		public void Read(byte[] pv, int cb, IntPtr pcbRead)
		{
			int ReadBytes;

			if (_Position < 0 || _Position > _Length)
			{
				ReadBytes = 0;
			}
			else
			{
				// Let's protect _Position: _Position might be changed by another Read() or Seek()
				lock (_Lock)
				{
					int TotalReadBytes = 0;
					int RestBytesToCopy = cb;

					int OffsetInOutput = 0;

					// Let's move chunk by chunk until all requested data is read or end of file is reached
					while (RestBytesToCopy > 0 && _Position < _Length)
					{
						// Original data is split into chunks, so let's find the chunk number that corresponds
						// with current position
						long RequiredChunkIndex = _Position / ChunkSize;

						// We do cache decrypted data, so let's update the cache if it's not initialized
						// or the cached chunk has another index
						if (-1 == _CurrentChunkIndex || _CurrentChunkIndex != RequiredChunkIndex)
						{
							_CurrentChunkIndex = RequiredChunkIndex;

							_EncryptedVideoFile.Position = _EncryptedChunkPosition[_CurrentChunkIndex];

							byte[] data = new byte[_EncryptedChunkLength[_CurrentChunkIndex]];
							_EncryptedVideoFile.Read(data, 0, data.Length);

							//********************************************************************* Реалізувати
							//_CurrentChunk = Program.Decrypt(data, data.Length);
						}

						// So far, we have the decrypted data available, now let's get the starting point within the chunk
						// and find out how many bytes we can read from the chunk (chunks may have different lengths)
						int OffsetInChunk = (int)(_Position - (_CurrentChunkIndex * ChunkSize));
						int RestInChunk = (int)(_SourceChunkLength[_CurrentChunkIndex] - OffsetInChunk);

						int BytesToCopy;
						if (RestInChunk < RestBytesToCopy)
							BytesToCopy = RestInChunk;
						else
							BytesToCopy = RestBytesToCopy;

						// Copy the data...
						Array.Copy(_CurrentChunk, OffsetInChunk, pv, OffsetInOutput, BytesToCopy);

						// ...and move forward
						RestBytesToCopy -= BytesToCopy;
						TotalReadBytes += BytesToCopy;
						OffsetInOutput += BytesToCopy;
						_Position += BytesToCopy;
					}

					ReadBytes = TotalReadBytes;
				}
			}

			if (IntPtr.Zero != pcbRead)
				Marshal.WriteIntPtr(pcbRead, new IntPtr(ReadBytes));
		}

		public void Write(byte[] pv, int cb, IntPtr pcbWritten)
		{
			throw new NotImplementedException();
		}

		public void SetSize(long libNewSize)
		{
			throw new NotImplementedException();
		}

		public void CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten)
		{
			throw new NotImplementedException();
		}

		public void Commit(int grfCommitFlags)
		{
			throw new NotImplementedException();
		}

		public void Revert()
		{
			throw new NotImplementedException();
		}

		public void LockRegion(long libOffset, long cb, int dwLockType)
		{
			throw new NotImplementedException();
		}

		public void UnlockRegion(long libOffset, long cb, int dwLockType)
		{
			throw new NotImplementedException();
		}

		public void Stat(out System.Runtime.InteropServices.ComTypes.STATSTG pstatstg, int grfStatFlag)
		{
			throw new NotImplementedException();
		}

		public void Clone(out IStream ppstm)
		{
			throw new NotImplementedException();
		}
	}
}
