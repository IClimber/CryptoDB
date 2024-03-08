using CryptoDataBase.CryptoContainer.Types;
using System.Collections.Generic;

namespace CryptoDataBase.CryptoContainer.Comparers
{
    public class SPointPositionComparer : IComparer<SPoint>
    {
        int IComparer<SPoint>.Compare(SPoint x, SPoint y)
        {
            return x.Start.CompareTo(y.Start);
        }
    }
}