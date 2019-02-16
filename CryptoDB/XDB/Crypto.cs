using System.Security.Cryptography;
using System.IO;
using System;

namespace CryptoDataBase
{
	public class Crypto
	{
		public static byte[] GetMD5(byte[] Data)
		{
			using (MD5 md5 = MD5.Create())
			{
				return md5.ComputeHash(Data);
			}
		}

		public static byte[] GetMD5(Stream stream)
		{
			using (MD5 md5 = MD5.Create())
			{
				return md5.ComputeHash(stream);
			}
		}

		public static byte[] GetFileSHA256(string FileName)
		{
			try
			{
				using (FileStream fs = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				{
					using (SHA256 sha256 = SHA256.Create())
					{
						return sha256.ComputeHash(fs);
					}
				}
			}
			catch (Exception e)
			{
				return null;
			}
		}

		public static bool CompareHash(byte[] Hash1, byte[] Hash2)
		{
			for (int i = 0; i < Hash1.Length; i++)
			{
				if (Hash1[i] != Hash2[i])
				{
					return false;
				}
			}

			return true;
		}

		public static void AES_Decrypt(Stream inputStream, byte[] outputData, int DataSize, AesCryptoServiceProvider AES) //Розкодувує файли, перед викликом не забути присвоїти потрібний IV
		{
			CryptoStream cs = new CryptoStream(inputStream, AES.CreateDecryptor(), CryptoStreamMode.Read);
			cs.Read(outputData, 0, DataSize);
		}

		public static UInt16 GetMod16(UInt16 length)
		{
			return (UInt16)GetMod16((UInt64)length);
		}

		public static UInt64 GetMod16(UInt64 length)
		{
			return length == 0 ? 0 : length % 16 == 0 ? length + 16 : (UInt64)(Math.Ceiling(length / 16.0) * 16);
		}
	}
}