using CryptoDataBase.CryptoContainer.Types;
using System.Collections.Generic;

namespace CryptoDataBase.CryptoContainer.Comparers
{
    public class SegmentPositionComparer : IComparer<Segment>
    {
        int IComparer<Segment>.Compare(Segment x, Segment y)
        {
            return x.Start.CompareTo(y.Start);
        }
    }
}