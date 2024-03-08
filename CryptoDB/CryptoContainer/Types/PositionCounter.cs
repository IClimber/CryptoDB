using System.Collections.Generic;

namespace CryptoDataBase.CryptoContainer.Types
{
    public class PositionCounter
    {
        public ulong Start;
        public uint Count;
        private static IEqualityComparer<ulong> comparer = EqualityComparer<ulong>.Default;

        public PositionCounter(ulong start, uint count = 1)
        {
            Start = start;
            Count = count;
        }

        public override bool Equals(object obj)
        {
            return (obj as PositionCounter).Start == Start;
        }

        public override int GetHashCode()
        {
            return comparer.GetHashCode(Start);
        }
    }
}