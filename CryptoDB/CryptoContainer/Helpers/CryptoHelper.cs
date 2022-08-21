using System.Security.Cryptography;

namespace CryptoDataBase.CryptoContainer.Helpers
{
    internal static class CryptoHelper
    {
        public static byte[] AesConvertBuf(byte[] inputData, int size, ICryptoTransform transform)
        {
            return transform.TransformFinalBlock(inputData, 0, size);
        }
    }
}