using CryptoDataBase.CryptoContainer.Types;
using System.Collections.Generic;

namespace CryptoDataBase.CryptoContainer.Comparers
{
    public class SegmentSizeComparer : IComparer<Segment>
    {
        int IComparer<Segment>.Compare(Segment x, Segment y)
        {
            var res = x.Size.CompareTo(y.Size);

            return res == 0 ? x.Start.CompareTo(y.Start) : res;
        }
    }
}