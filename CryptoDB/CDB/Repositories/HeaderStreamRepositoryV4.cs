using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CDB.Repositories
{
	class HeaderStreamRepositoryV4 : HeaderRepository
	{
		const uint BLOCK_SIZE = 1048576;
		public const byte CURRENT_VERSION = 4;

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
				ulong position = (ulong)_stream.Position;
				position = (position % BLOCK_SIZE + rawSize) <= BLOCK_SIZE ? position : (ulong)Math.Ceiling((double)position / BLOCK_SIZE) * BLOCK_SIZE;
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
			Header header = null;
			ulong position = (ulong)stream.Position;
			try
			{
				header = new Header(stream, position, this, _aes);
			}
			catch (Exception)
			{ }

			ulong newPos = (ulong)stream.Position;
			if (position / BLOCK_SIZE < newPos / BLOCK_SIZE)
			{
				position = (ulong)Math.Ceiling(position / (double)BLOCK_SIZE) * BLOCK_SIZE;
				header = new Header(stream, position, this, _aes);
			}

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
