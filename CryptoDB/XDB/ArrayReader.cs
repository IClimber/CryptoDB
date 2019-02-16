using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataBase
{
	public class ArrayReader
	{
		public int Position { get { return _Position; } set { _Position = value < _Length ? value : _Length; } }
		public int Length { get { return _Length; } }
		private int _Position;
		private int _Length;
		private byte[] buffer;

		public ArrayReader(byte[] buffer)
		{
			this.buffer = buffer;
			_Position = 0;
			_Length = buffer.Length;
		}

		public void Read(byte[] buf, int offset, int count)
		{
			Buffer.BlockCopy(buffer, _Position, buf, offset, count);
			_Position += count;
		}

		public byte ReadByte()
		{
			byte[] buf = new byte[1];
			Buffer.BlockCopy(buffer, _Position, buf, 0, buf.Length);
			_Position += buf.Length;

			return buf[1];
		}
	}
}
