using CryptoDataBase.CryptoContainer.Models;
using System.Collections.Generic;

namespace CryptoDataBase.CryptoContainer.Comparers
{
    public class IDComparer : IComparer<DirectoryElement>
    {
        int IComparer<DirectoryElement>.Compare(DirectoryElement x, DirectoryElement y)
        {
            return x.Id == y.Id ? 0 : x.Id < y.Id ? -1 : 1;
        }
    }
}