﻿using System;

namespace CryptoDataBase.CDB.Exceptions
{
	class UnsupportedMethodException : Exception
	{
		public UnsupportedMethodException() : base()
		{

		}

		public UnsupportedMethodException(string message) : base(message)
		{

		}

		public UnsupportedMethodException(string message, Exception innerException) : base(message, innerException)
		{

		}
	}
}
