using CryptoDataBase.CDB.Repositories;
using System;
using System.IO;
using System.Security.Cryptography;

namespace CryptoDataBase.CDB
{
	//Працює тільки з файлом заголовківю
	//При шифрувані доповнення не використовується
	public class Header
	{
		public const byte RAW_LENGTH = 33; //Розмір заголовку в байтах
		private const PaddingMode GetPaddingMode = PaddingMode.None;
		private const byte _ReservLength = 16;

		public HeaderRepository repository { get { return _repository; } }
		private HeaderRepository _repository;
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
		private bool wasWroteInFile = false;
		private byte[] infDdata;

		public Header(HeaderRepository repository, AesCryptoServiceProvider AES, ElementType blockType)
		{
			_IV = new byte[16];
			CryptoRandom.GetBytes(_IV);

			_AES = AES;
			_StartPos = repository.GetEndPosition();
			_Exists = true;
			_ElType = blockType;
			_repository = repository;
			_InfSize = 0;
		}

		//Створення при зчитувані з файлу
		public Header(Stream memoryStream, ulong startPos, HeaderRepository repository, AesCryptoServiceProvider AES)
		{
			//Зчитуємо незакодовані дані, IV (16 байт) і Exists (1 байт)
			byte[] buf = new byte[17];
			_StartPos = startPos;
			memoryStream.Read(buf, 0, buf.Length);

			//Записуємо зчитані дані в відповідні параметри
			_IV = new byte[16];
			Buffer.BlockCopy(buf, 0, _IV, 0, 16);
			_Exists = buf[16] < 128;

			_AES = AES;
			SetAESValue();
			ICryptoTransform transform = AES.CreateDecryptor(AES.Key, _IV);
			memoryStream.Read(buf, 0, 16);
			buf = Crypto.AES_Decrypt_Buf(buf, 16, transform);

			_InfSize = BitConverter.ToUInt16(buf, 13);
			_ElType = (ElementType)(buf[15] / 128);

			wasWroteInFile = true;

			_repository = repository;

			if (_Exists)
			{
				infDdata = new byte[_InfSize];
				memoryStream.Read(infDdata, 0, infDdata.Length);
				infDdata = Crypto.AES_Decrypt_Buf(infDdata, infDdata.Length, transform);
			}
			transform.Dispose();
		}

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

		public bool Save(HeaderRepository repository)
		{
			lock (repository.writeLock)
			{
				SetAESValue();

				byte[] buf = ToBufferFirstBlock();
				repository.Write((long)_StartPos, buf, 0, buf.Length);
				repository.WriteEncrypt((long)_StartPos + buf.Length, ToBufferEncryptBlock(), AES);
			}

			wasWroteInFile = true;

			return true;
		}

		public bool SaveInfo(byte[] info)
		{
			lock (_repository.writeLock)
			{
				SetAESValue();
				if (info.Length > _InfSize)
				{
					Delete();
					_Exists = true;
					_InfSize = (ushort)info.Length;
					_StartPos = _repository.GetStartPosBySize(_repository.GetEndPosition(), (ushort)(RAW_LENGTH + _InfSize));

					byte[] nonEncryptedFirstBlock = ToBufferFirstBlock();
					repository.Write((long)_StartPos, nonEncryptedFirstBlock, 0, nonEncryptedFirstBlock.Length);
					repository.WriteEncrypt((long)_StartPos + nonEncryptedFirstBlock.Length, ToBufferEncryptBlock(), AES);
					wasWroteInFile = true;
				}

				_repository.WriteEncrypt((long)(_StartPos + RAW_LENGTH), info, AES);
			}

			return true;
		}

		public bool ExportInfoTo(HeaderRepository repository, ulong startPos, byte[] info)
		{
			lock (repository.writeLock)
			{
				_StartPos = startPos;
				_InfSize = (ushort)info.Length;

				Save(repository);

				SetAESValue();
				repository.WriteEncrypt((long)(_StartPos + RAW_LENGTH), info, AES);
			}

			return true;
		}

		public static ushort GetNewInfSizeByBufLength(int length)
		{
			return (UInt16)(Math.Ceiling(length / 16.0) * 16 + _ReservLength);
		}

		public byte[] GetInfoBuf()
		{
			return infDdata;
		}

		public void Delete()
		{
			_Exists = false;

			if (wasWroteInFile)
			{
				_repository.WriteByte((long)_StartPos + 16, (byte)(Convert.ToByte(!_Exists) * 128 + (int)CryptoRandom.Random(128)));
			}
		}

		public void Restore()
		{
			_Exists = true;

			if (wasWroteInFile)
			{
				_repository.WriteByte((long)_StartPos + 16, (byte)(Convert.ToByte(!_Exists) * 128 + (int)CryptoRandom.Random(128)));
			}
		}
	}
}
