using CryptoDataBase.CDB.Exceptions;
using System.IO;

namespace CryptoDataBase.CDB.Repositories
{
	public class HeaderRepositoryFactory
	{
		private const byte VERSION_3 = 3;
		private const byte VERSION_4 = 4;
		private const byte VERSION_5 = 5;

		public static HeaderRepository GetRepositoryByVersion(byte version, Stream stream, string password, byte[] aesKey = null)
		{
			switch (version)
			{
				case VERSION_3:
					return new HeaderStreamRepositoryV3(stream, password, aesKey);
				case VERSION_4:
					return new HeaderStreamRepositoryV4(stream, password, aesKey);
				case VERSION_5:
					return new HeaderStreamRepositoryV5(stream, password, aesKey);
			}

			throw new UnsupportedVersionException();
		}
	}
}
