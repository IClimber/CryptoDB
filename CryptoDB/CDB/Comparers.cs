using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace CryptoDataBase.CDB
{
	public class IDComparer : IComparer<DirElement>
	{
		int IComparer<DirElement>.Compare(DirElement x, DirElement y)
		{
			return x.ID == y.ID ? 0 : x.ID < y.ID ? -1 : 1;
		}
	}

	public class NameComparer : IComparer<Element>
	{
		[DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
		static extern int StrCmpLogicalW(string s1, string s2);

		int IComparer<Element>.Compare(Element x, Element y)
		{
			return StrCmpLogicalW(x.Name, y.Name);//String.Compare(x.Name, y.Name, true);
		}
	}

	public class ExtComparer : IComparer<Element>
	{
		int IComparer<Element>.Compare(Element x, Element y)
		{
			return String.Compare(Path.GetExtension(x.Name), Path.GetExtension(y.Name), true);
		}
	}

	public class SizeComparer : IComparer<Element>
	{
		int IComparer<Element>.Compare(Element x, Element y)
		{
			return x.Size == y.Size ? 0 : x.Size < y.Size ? -1 : 1;
		}
	}

	public class TimeComparer : IComparer<Element>
	{
		int IComparer<Element>.Compare(Element x, Element y)
		{
			return x.TimeIndex == y.TimeIndex ? 0 : x.TimeIndex < y.TimeIndex ? -1 : 1;
		}
	}

	public class PosComparer : IComparer<SPoint>
	{
		int IComparer<SPoint>.Compare(SPoint x, SPoint y)
		{
			return x.Start < y.Start ? -1 : x.Start > y.Start ? 1 : 0;
		}
	}

	public class PSizeComparer : IComparer<SPoint>
	{
		int IComparer<SPoint>.Compare(SPoint x, SPoint y)
		{
			return x.Size < y.Size ? -1 : x.Size > y.Size ? 1 : 0;
		}
	}

	public class PHashComparer : IComparer<Element>
	{
		private byte sensative = 0;

		public PHashComparer(byte sensative = 0)
		{
			this.sensative = sensative;
		}

		int IComparer<Element>.Compare(Element x, Element y)
		{
			byte dist1 = Element.GetHammingDistance(BitConverter.ToUInt64(x.PHash, 0), 0);
			byte dist2 = Element.GetHammingDistance(BitConverter.ToUInt64(y.PHash, 0), 0);
			return dist1 < dist2 ? -1 : dist1 > dist2 ? 1 : 0;
		}
	}
}