using System;

namespace CryptoDataBase.CryptoContainer.Exceptions
{
    class FileIsTooBigException : Exception
    {
        public FileIsTooBigException() : base()
        {

        }

        public FileIsTooBigException(string message) : base(message)
        {

        }

        public FileIsTooBigException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}