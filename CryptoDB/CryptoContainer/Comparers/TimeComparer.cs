using CryptoDataBase.CryptoContainer.Models;
using System.Collections.Generic;

namespace CryptoDataBase.CryptoContainer.Comparers
{
    public class TimeComparer : IComparer<Element>
    {
        int IComparer<Element>.Compare(Element x, Element y)
        {
            return x.TimeIndex == y.TimeIndex ? 0 : x.TimeIndex < y.TimeIndex ? -1 : 1;
        }
    }
}