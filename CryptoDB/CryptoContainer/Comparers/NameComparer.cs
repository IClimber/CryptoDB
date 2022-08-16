using CryptoDataBase.CryptoContainer.Models;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CryptoDataBase.CryptoContainer.Comparers
{
    public class NameComparer : IComparer<Element>
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int StrCmpLogicalW(string s1, string s2);

        int IComparer<Element>.Compare(Element x, Element y)
        {
            return StrCmpLogicalW(x.Name, y.Name);//String.Compare(x.Name, y.Name, true);
        }
    }
}