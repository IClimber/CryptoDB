﻿using System;

namespace CryptoDataBase.CryptoContainer.Exceptions
{
    class InvalidFileStartPositionException : Exception
    {
        public InvalidFileStartPositionException() : base()
        {

        }

        public InvalidFileStartPositionException(string message) : base(message)
        {

        }

        public InvalidFileStartPositionException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}