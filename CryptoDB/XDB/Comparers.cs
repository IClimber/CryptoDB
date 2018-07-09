using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CryptoDataBase
{
	public class IDComparer : IComparer<Element>
	{
		int IComparer<Element>.Compare(Element x, Element y)
		{
			return x.ID == y.ID ? 0 : x.ID < y.ID ? -1 : 1;
		}
	}

	public class NameComparer : IComparer<Element>
	{
		int IComparer<Element>.Compare(Element x, Element y)
		{
			return String.Compare(x.Name, y.Name, true);
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
}