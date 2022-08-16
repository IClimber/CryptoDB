using CryptoDataBase.CryptoContainer.Models;
using System.Collections.Generic;
using System.IO;

namespace CryptoDataBase.CryptoContainer.Comparers
{
    public class ExtComparer : IComparer<Element>
    {
        int IComparer<Element>.Compare(Element x, Element y)
        {
            return string.Compare(Path.GetExtension(x.Name), Path.GetExtension(y.Name), true);
        }
    }
}