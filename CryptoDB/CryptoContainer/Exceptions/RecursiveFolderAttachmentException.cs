﻿using System;

namespace CryptoDataBase.CryptoContainer.Exceptions
{
    class RecursiveFolderAttachmentException : Exception
    {
        public RecursiveFolderAttachmentException() : base()
        {

        }

        public RecursiveFolderAttachmentException(string message) : base(message)
        {

        }

        public RecursiveFolderAttachmentException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}