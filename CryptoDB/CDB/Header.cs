using System;
using System.Security.Cryptography;
using System.IO;

namespace CryptoDataBase.CDB
{
	//Працює тільки з файлом заголовківю
	//При шифрувані доповнення не використовується
    public class Header
    {
		public const byte Length = 33; //Розмір заголовку в байтах
		private const PaddingMode GetPaddingMode = PaddingMode.None;
		private const byte _ReservLength = 16;

		public SafeStreamAccess headersFileStream { get { return _headersFileStream; } }
		private SafeStreamAccess _headersFileStream;
		public UInt64 StartPos { get { return _StartPos; } }
		private UInt64 _StartPos { get; set; } //Стартова позиція заголовку в файлі
		public AesCryptoServiceProvider AES { get { return _AES; } }
		private AesCryptoServiceProvider _AES;
		public byte[] IV { get { return _IV; } } //byte[16]
		private byte[] _IV; //byte[16]
		public bool Exists { get { return _Exists; } } //BlockStatus
		private bool _Exists = true;
		public ElementType ElType { get { return _ElType; } }
		private ElementType _ElType;
		public UInt16 InfSize { get { return _InfSize; } }
		private UInt16 _InfSize;
		private bool writedInFile = false;
		private byte[] infDdata;
		private Stream memoryData;

		public Header(SafeStreamAccess headersStream, AesCryptoServiceProvider AES, ElementType blockType)
		{
			_IV = new byte[16];
			CryptoRandom.GetBytes(_IV);

			_AES = AES;
			_StartPos = (UInt64)headersStream.Length;
			_Exists = true;
			_ElType = blockType;
			_headersFileStream = headersStream;
			_InfSize = 0;
		}

		//Створення при зчитувані з файлу
		public Header(Stream memoryStream, UInt64 startPos, SafeStreamAccess headersStream, AesCryptoServiceProvider AES)
		{
			memoryData = memoryStream;
			//Зчитуємо незакодовані дані, IV (16 байт) і Exists (1 байт)
			byte[] buf = new byte[17];
			_StartPos = startPos;
			memoryStream.Position = (int)startPos;
			memoryStream.Read(buf, 0, buf.Length);

			//Записуємо зчитані дані в відповідні параметри
			_IV = new byte[16];
			Buffer.BlockCopy(buf, 0, _IV, 0, 16);
			_Exists = buf[16] < 128;

			_AES = AES;
			SetAESValue();
			Crypto.AES_Decrypt(memoryStream, buf, 16, this.AES);

			_InfSize = BitConverter.ToUInt16(buf, 13);
			_ElType = (ElementType)(buf[15] / 128);

			writedInFile = true;

			_headersFileStream = headersStream;

			if (_Exists)
			{
				infDdata = new byte[_InfSize];

				SetAESValue();

				//memoryData.Position = (int)_StartPos + Length;
				Crypto.AES_Decrypt(memoryData, infDdata, infDdata.Length, AES);
			}
		}

		//public byte[] ToBuffer()
		//{
		//	byte[] result = new byte[Length]; //не забути про доповнення кратне 16
		//	byte[] trash = new byte[13];
		//	CryptoRandom.GetBytes(trash);
		//	Buffer.BlockCopy(_IV, 0, result, 0, _IV.Length);
		//	byte BlockStatus = (byte)(Convert.ToByte(!Exists) * 128 + (int)CryptoRandom.Random(128));
		//	Buffer.BlockCopy(BitConverter.GetBytes(BlockStatus), 0, result, 16, 1);
		//	Buffer.BlockCopy(trash, 0, result, 17, trash.Length);
		//	Buffer.BlockCopy(BitConverter.GetBytes(_InfSize), 0, result, 30, 2);
		//	Buffer.BlockCopy(BitConverter.GetBytes((byte)ElType * 128 + (int)CryptoRandom.Random(128)), 0, result, 32, 1);

		//	return result;
		//}

		//Перші 17 байт (нешифровані)
		public byte[] ToBufferFirstBlock()
		{
			byte[] result = new byte[17];
			Buffer.BlockCopy(_IV, 0, result, 0, _IV.Length);
			result[16] = (byte)(Convert.ToByte(!Exists) * 128 + (int)CryptoRandom.Random(128));

			return result;
		}

		public byte[] ToBufferEncryptBlock()
		{
			byte[] result = new byte[16];
			CryptoRandom.GetBytes(result);
			Buffer.BlockCopy(BitConverter.GetBytes(_InfSize), 0, result, 13, 2);
			Buffer.BlockCopy(BitConverter.GetBytes((byte)ElType * 128 + (int)CryptoRandom.Random(128)), 0, result, 15, 1);

			return result;
		}

		private void SetAESValue()
		{
			AES.IV = _IV;
			AES.Padding = GetPaddingMode;
		}

		public bool Save()
		{
			lock (_headersFileStream.writeLock)
			{
				SetAESValue();

				byte[] buf = ToBufferFirstBlock();
				_headersFileStream.Write((long)(_StartPos), buf, 0, buf.Length);
				_headersFileStream.WriteEncrypt((long)(_StartPos) + buf.Length, ToBufferEncryptBlock(), AES);
			}

			writedInFile = true;

			return true;
		}

		public bool SaveInfo(byte[] info, int realLength)
		{
			CryptoRandom.GetBytes(info, realLength, info.Length - realLength);

			lock (_headersFileStream.writeLock)
			{
				if (info.Length > _InfSize)
				{
					Delete();
					_Exists = true;
					_StartPos = (UInt64)_headersFileStream.Length;
					_InfSize = (ushort)info.Length;

					Save();
				}

				SetAESValue();

				_headersFileStream.WriteEncrypt((long)(_StartPos + Length), info, AES);
			}

			return true;
		}

		public static ushort GetNewInfSizeByBufLength(int length)
		{
			return (UInt16)(Math.Ceiling(length / 16.0) * 16 + _ReservLength);
		}

		public byte[] GetInfoBuf()
		{
			//byte[] buf = new byte[InfSize];

			//SetAESValue();

			//memoryData.Position = (int)_StartPos + Length;
			//Crypto.AES_Decrypt(memoryData, buf, buf.Length, AES);

			return infDdata;
		}

		public void Delete()
		{
			_Exists = false;

			if (writedInFile)
			{
				_headersFileStream.WriteByte((long)_StartPos + 16, (byte)(Convert.ToByte(!_Exists) * 128 + (int)CryptoRandom.Random(128)));
			}
		}

		public void Restore()
		{
			_Exists = true;

			if (writedInFile)
			{
				_headersFileStream.WriteByte((long)_StartPos + 16, (byte)(Convert.ToByte(!_Exists) * 128 + (int)CryptoRandom.Random(128)));
			}
		}
	}
}
