using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CryptoDataBase.CDB.Repositories
{
	class HeaderStreamRepositoryV4 : HeaderRepository
	{
		const uint BLOCK_SIZE = 1048576;
		public const byte CURRENT_VERSION = 4;
		private static int threadsCount = Environment.ProcessorCount;

		public HeaderStreamRepositoryV4(Stream stream, AesCryptoServiceProvider aes) : base(stream, aes)
		{

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
			_stream.WriteByte(CURRENT_VERSION);

			WriteToStream(elements);
		}

		private void WriteToStream(IList<Element> elements)
		{
			foreach (Element element in elements)
			{
				ushort rawSize = (ushort)(element.GetRawInfoLength() + Header.RAW_LENGTH);
				ulong position = GetStartPosBySize((ulong)_stream.Position, rawSize);
				element.ExportInfTo(this, position);

				if (element is DirElement)
				{
					WriteToStream(((DirElement)element).Elements);
				}
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
				headerStream.Position = i == 0 ? 1 : 0;
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
				header = new Header(stream, position + offset, this, _aes);
				if (header.InfSize + (ulong)Header.RAW_LENGTH + position > (ulong)stream.Length)
				{
					return null;
				}
			} catch (Exception)
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
