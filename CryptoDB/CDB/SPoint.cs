namespace CryptoDataBase.CDB
{
	public class SPoint
	{
		public ulong Start;
		public ulong Size;

		public SPoint(ulong start, ulong size)
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
