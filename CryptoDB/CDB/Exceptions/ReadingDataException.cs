using System;

namespace CryptoDataBase.CDB.Exceptions
{
	class ReadingDataException : Exception
	{
		public ReadingDataException() : base()
		{

		}

		public ReadingDataException(string message) : base(message)
		{

		}

		public ReadingDataException(string message, Exception innerException) : base(message, innerException)
		{

		}
	}
}
