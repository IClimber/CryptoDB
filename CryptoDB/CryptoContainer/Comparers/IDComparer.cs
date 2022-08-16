using CryptoDataBase.CryptoContainer.Models;
using System.Collections.Generic;

namespace CryptoDataBase.CryptoContainer.Comparers
{
    public class IDComparer : IComparer<DirElement>
    {
        int IComparer<DirElement>.Compare(DirElement x, DirElement y)
        {
            return x.Id == y.Id ? 0 : x.Id < y.Id ? -1 : 1;
        }
    }
}