using System;

namespace CryptoDataBase.CryptoContainer.Exceptions
{
    class DataWasNotWrittenException : Exception
    {
        public DataWasNotWrittenException() : base()
        {

        }

        public DataWasNotWrittenException(string message) : base(message)
        {

        }

        public DataWasNotWrittenException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}