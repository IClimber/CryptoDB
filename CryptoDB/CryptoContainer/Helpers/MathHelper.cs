using System;

namespace CryptoDataBase.CryptoContainer.Helpers
{
    internal static class MathHelper
    {
        public static ulong GetMod16(ulong length)
        {
            return length == 0 ? 0 : length % 16 == 0 ? length + 16 : (ulong)(Math.Ceiling(length / 16.0) * 16);
        }
    }
}