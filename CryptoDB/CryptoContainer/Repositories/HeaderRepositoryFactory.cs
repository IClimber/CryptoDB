using CryptoDataBase.CryptoContainer.Exceptions;
using System.IO;

namespace CryptoDataBase.CryptoContainer.Repositories
{
    public class HeaderRepositoryFactory
    {
        private const byte Version3 = 3;
        private const byte Version4 = 4;
        private const byte Version5 = 5;

        public static HeaderRepository GetRepositoryByVersion(byte version, Stream stream, string password, byte[] aesKey = null)
        {
            switch (version)
            {
                case Version3:
                    return new HeaderStreamRepositoryV3(stream, password, aesKey);
                case Version4:
                    return new HeaderStreamRepositoryV4(stream, password, aesKey);
                case Version5:
                    return new HeaderStreamRepositoryV5(stream, password, aesKey);
            }

            throw new UnsupportedVersionException();
        }
    }
}