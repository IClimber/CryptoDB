using System;

namespace CryptoDataBase.CDB.Exceptions
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
