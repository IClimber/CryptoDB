using CryptoDataBase.CryptoContainer.Types;
using System.Collections.Generic;

namespace CryptoDataBase.CryptoContainer.Comparers
{
    public class SPointSizeComparer : IComparer<SPoint>
    {
        int IComparer<SPoint>.Compare(SPoint x, SPoint y)
        {
            var res = x.Size.CompareTo(y.Size);

            return res == 0 ? x.Start.CompareTo(y.Start) : res;
        }
    }
}