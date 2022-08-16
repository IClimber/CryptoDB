using System;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CryptoContainer.Helpers
{
    internal static class HashHelper
    {
        public static bool CompareHash(byte[] hash1, byte[] hash2)
        {
            for (int i = 0; i < hash1.Length; i++)
            {
                if (hash1[i] != hash2[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static byte[] GetFileSHA256(string fileName)
        {
            try
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        return sha256.ComputeHash(fs);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
        public static byte[] GetMD5(byte[] data)
        {
            using (MD5 md5 = MD5.Create())
            {
                return md5.ComputeHash(data);
            }
        }

        public static byte[] GetMD5(Stream stream)
        {
            using (MD5 md5 = MD5.Create())
            {
                return md5.ComputeHash(stream);
            }
        }
    }
}