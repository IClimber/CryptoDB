using System;

namespace CryptoDataBase.CryptoContainer.Exceptions
{
    class FreeSpaceMapWasNotCalculatedException : Exception
    {
        public FreeSpaceMapWasNotCalculatedException() : base()
        {

        }

        public FreeSpaceMapWasNotCalculatedException(string message) : base(message)
        {

        }

        public FreeSpaceMapWasNotCalculatedException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}