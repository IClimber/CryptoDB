using System;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CDB
{
	public class Crypto
	{
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

		public static byte[] AesConvertBuf(byte[] inputData, int size, ICryptoTransform transform)
		{
			return transform.TransformFinalBlock(inputData, 0, size);
		}

		public static ushort GetMod16(ushort length)
		{
			return GetMod16(length);
		}

		public static ulong GetMod16(ulong length)
		{
			return length == 0 ? 0 : length % 16 == 0 ? length + 16 : (ulong)(Math.Ceiling(length / 16.0) * 16);
		}
	}
}