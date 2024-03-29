﻿using System;

namespace CryptoDataBase.CryptoContainer.Exceptions
{
    class DuplicatesFileNameException : Exception
    {
        public DuplicatesFileNameException() : base()
        {

        }

        public DuplicatesFileNameException(string message) : base(message)
        {

        }

        public DuplicatesFileNameException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}