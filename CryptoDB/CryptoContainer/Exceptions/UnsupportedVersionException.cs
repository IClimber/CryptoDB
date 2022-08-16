using System;

namespace CryptoDataBase.CryptoContainer.Exceptions
{
    class UnsupportedVersionException : Exception
    {
        public UnsupportedVersionException() : base()
        {

        }

        public UnsupportedVersionException(string message) : base(message)
        {

        }

        public UnsupportedVersionException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}