using System;

namespace CryptoDataBase.CDB.Exceptions
{
	class HeaderWasNotWrittenException : Exception
	{
		public HeaderWasNotWrittenException() : base()
		{

		}

		public HeaderWasNotWrittenException(string message) : base(message)
		{

		}

		public HeaderWasNotWrittenException(string message, Exception innerException) : base(message, innerException)
		{

		}
	}
}
