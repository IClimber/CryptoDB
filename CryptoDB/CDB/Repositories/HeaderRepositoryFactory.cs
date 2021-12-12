using System;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CDB.Repositories
{
	public class HeaderRepositoryFactory
	{
		private const byte VERSION_3 = 3;
		private const byte VERSION_4 = 4;

		public static HeaderRepository GetRepositoryByVersion(byte version, Stream stream, AesCryptoServiceProvider aes)
		{
			switch (version)
			{
				case VERSION_3:
					return new HeaderStreamRepositoryV3(stream, aes);
				case VERSION_4:
					return new HeaderStreamRepositoryV4(stream, aes);
			}

			throw new Exception();
		}
	}
}
