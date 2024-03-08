namespace CryptoDataBase.CryptoContainer.Types
{
    public class SPoint
    {
        public ulong Start;
        public ulong Size;
        public ulong End => Start + Size;

        public SPoint(ulong start, ulong size)
        {
            Start = start;
            Size = size;
        }

        public SPoint Clone()
        {
            return new SPoint(Start, Size);
        }

        public bool EaqualsValue(SPoint sPoint)
        {
            return Start == sPoint.Start && Size == sPoint.Size;
        }
    }
}