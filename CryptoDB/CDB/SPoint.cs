using System;

namespace CryptoDataBase.CDB
{
	public class SPoint
	{
		public UInt64 Start;
		public UInt64 Size;

		public SPoint(UInt64 start, UInt64 size)
		{
			Start = start;
			Size = size;
		}

		public SPoint Clone()
		{
			return new SPoint(Start, Size);
		}
	}
}
