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

		public Header(HeaderRepository repository, ElementType blockType)
		{
			_IV = new byte[16];
			CryptoRandom.GetBytes(_IV);

			_Exists = true;
			_ElType = blockType;
			_repository = repository;
			_InfSize = 0;
		}

		public Header(HeaderRepository repository,
			UInt64 StartPos,
			byte[] IV,
			bool Exists,
			ElementType ElType,
			UInt16 InfSize = 0,
			byte[] InfDdata = null
			)
		{
			_repository = repository;
			_StartPos = StartPos;
			_IV = IV;
			_Exists = Exists;
			_ElType = ElType;
			_InfSize = InfSize;
			infDdata = InfDdata;

			wasWroteInFile = infDdata != null;
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

		public bool SaveInfo(byte[] info)
		{
			lock (_repository.writeLock)
			{
				if (info.Length > _InfSize)
				{
					Delete();
					_Exists = true;
					_InfSize = (ushort)info.Length;
					_StartPos = _repository.GetStartPosBySize(_repository.GetEndPosition(), (ushort)(RAW_LENGTH + _InfSize));

					byte[] nonEncryptedFirstBlock = ToBufferFirstBlock();
					_repository.Write((long)_StartPos, nonEncryptedFirstBlock, 0, nonEncryptedFirstBlock.Length);
					_repository.WriteEncrypt((long)_StartPos + nonEncryptedFirstBlock.Length, ToBufferEncryptBlock(), _IV);
					wasWroteInFile = true;
				}

				_repository.WriteEncrypt((long)(_StartPos + RAW_LENGTH), info, _IV);
			}

			return true;
		}

		public bool ExportInfoTo(HeaderRepository repository, ulong startPos, byte[] info)
		{
			lock (repository.writeLock)
			{
				_StartPos = startPos;
				_InfSize = (ushort)info.Length;

				byte[] buf = ToBufferFirstBlock();
				repository.Write((long)_StartPos, buf, 0, buf.Length);
				repository.WriteEncrypt((long)_StartPos + buf.Length, ToBufferEncryptBlock(), _IV);
				repository.WriteEncrypt((long)(_StartPos + RAW_LENGTH), info, _IV);
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
