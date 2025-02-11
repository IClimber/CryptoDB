using System.Collections.Generic;
using System.Linq;

namespace CryptoDataBase.CryptoContainer.Helpers
{
    internal static class StringHelper
    {
        private static readonly HashSet<char> _invalidChars = new HashSet<char> { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

        public static string SanitizeFileName(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return filename;
            }

            return new string(filename
                .Select(c => _invalidChars.Contains(c) ? '_' : c)
                .ToArray()
            );
        }
    }
}
