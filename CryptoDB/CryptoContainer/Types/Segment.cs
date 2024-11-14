namespace CryptoDataBase.CryptoContainer.Types
{
    public class Segment
    {
        public ulong Start;
        public ulong Size;
        public ulong End => Start + Size;

        public Segment(ulong start, ulong size)
        {
            Start = start;
            Size = size;
        }

        public Segment Clone()
        {
            return new Segment(Start, Size);
        }

        public bool EaqualsValue(Segment segment)
        {
            return Start == segment.Start && Size == segment.Size;
        }

        public bool IsFullIn(Segment segment)
        {
            return Start >= segment.Start && End <= segment.End;
        }

        public bool IsIntersects(Segment segment)
        {
            return (Start <= segment.Start && End >= segment.Start) || (Start <= segment.End && End >= segment.End) || IsFullIn(segment);
        }
    }
}