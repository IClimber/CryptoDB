using System;
using System.Security.Cryptography;

namespace CryptoDataBase.CDB
{
	public static class CryptoRandom
	{
		public static void GetBytes(byte[] buffer)
		{
			using (RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider())
			{
				rngCsp.GetBytes(buffer);
			}
		}

		public static void GetBytes(byte[] buffer, int offset, int count)
		{
			using (RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider())
			{
				rngCsp.GetBytes(buffer, offset, count);
			}
		}

		public static ulong Random(ulong max)
		{
			RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();
			byte[] result = new byte[8];
			rngCsp.GetBytes(result);

			return BitConverter.ToUInt64(result, 0) % max;
		}
	}
}
