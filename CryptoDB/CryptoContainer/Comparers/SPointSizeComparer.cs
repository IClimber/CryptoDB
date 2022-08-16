using CryptoDataBase.CryptoContainer.Types;
using System.Collections.Generic;

namespace CryptoDataBase.CryptoContainer.Comparers
{
    public class SPointSizeComparer : IComparer<SPoint>
    {
        int IComparer<SPoint>.Compare(SPoint x, SPoint y)
        {
            return x.Size < y.Size ? -1 : x.Size > y.Size ? 1 : 0;
        }
    }
}