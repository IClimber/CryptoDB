using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace CryptoDataBase
{
	static class CryptoRandom
	{
		static public void GetBytes(byte[] buffer)
		{
			RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();
			rngCsp.GetBytes(buffer);
		}

		static public UInt64 Random(UInt64 max)
		{
			RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();
			byte[] result = new byte[8];
			rngCsp.GetBytes(result);

			return BitConverter.ToUInt64(result, 0) % max;
		}
	}
}
