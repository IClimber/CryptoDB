using CryptoDataBase.CryptoContainer.Models;
using System.Collections.Generic;

namespace CryptoDataBase.CryptoContainer.Comparers
{
    public class SizeComparer : IComparer<Element>
    {
        int IComparer<Element>.Compare(Element x, Element y)
        {
            return x.Size == y.Size ? 0 : x.Size < y.Size ? -1 : 1;
        }
    }
}