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
		//Розмір заголовку в байтах
		public const byte RawLength = 33;
		private const byte ReservLength = 16;

		public HeaderRepository Repository => _repository;
		private HeaderRepository _repository;
		public ulong StartPos => _startPos;
		//Стартова позиція заголовку в файлі
		private ulong _startPos;
		public byte[] IV => _iv;
		private readonly byte[] _iv; //byte[16]
		public bool Exists => _exists;
		private bool _exists = true;
		public ElementType ElementType => _elementType;
		private readonly ElementType _elementType;
		public ushort InfSize => _infSize;
		private ushort _infSize;
		private bool _wasWroteInFile = false;
		private byte[] _infDdata;

		public Header(HeaderRepository repository, ElementType blockType)
		{
			_iv = new byte[16];
			CryptoRandom.GetBytes(_iv);

			_exists = true;
			_elementType = blockType;
			_repository = repository;
			_infSize = 0;
		}

		public Header(
			HeaderRepository repository,
			ulong startPos,
			byte[] iv,
			bool exists,
			ElementType elementType,
			ushort infSize = 0,
			byte[] infDdata = null
			)
		{
			_repository = repository;
			_startPos = startPos;
			_iv = iv;
			_exists = exists;
			_elementType = elementType;
			_infSize = infSize;
			_infDdata = infDdata;

			_wasWroteInFile = _infDdata != null;
		}

		//Перші 17 байт (нешифровані)
		public byte[] ToBufferFirstBlock()
		{
			byte[] result = new byte[17];
			Buffer.BlockCopy(_iv, 0, result, 0, _iv.Length);
			result[16] = (byte)(Convert.ToByte(!Exists) * 128 + (int)CryptoRandom.Random(128));

			return result;
		}

		public byte[] ToBufferEncryptBlock()
		{
			byte[] result = new byte[16];
			CryptoRandom.GetBytes(result);
			Buffer.BlockCopy(BitConverter.GetBytes(_infSize), 0, result, 13, 2);
			Buffer.BlockCopy(BitConverter.GetBytes((byte)ElementType * 128 + (int)CryptoRandom.Random(128)), 0, result, 15, 1);

			return result;
		}

		public bool SaveInfo(byte[] info)
		{
			lock (_repository.WriteLock)
			{
				if (info.Length > _infSize)
				{
					Delete();
					_exists = true;
					_infSize = (ushort)info.Length;
					_startPos = _repository.GetStartPosBySize(_repository.GetEndPosition(), (ushort)(RawLength + _infSize));

					byte[] nonEncryptedFirstBlock = ToBufferFirstBlock();
					_repository.Write((long)_startPos, nonEncryptedFirstBlock, 0, nonEncryptedFirstBlock.Length);
					_repository.WriteEncrypt((long)_startPos + nonEncryptedFirstBlock.Length, ToBufferEncryptBlock(), _iv);
					_wasWroteInFile = true;
				}

				_repository.WriteEncrypt((long)(_startPos + RawLength), info, _iv);
			}

			return true;
		}

		public bool ExportInfoTo(HeaderRepository repository, ulong startPos, byte[] info)
		{
			lock (repository.WriteLock)
			{
				_startPos = startPos;
				_infSize = (ushort)info.Length;

				byte[] buf = ToBufferFirstBlock();
				repository.Write((long)_startPos, buf, 0, buf.Length);
				repository.WriteEncrypt((long)_startPos + buf.Length, ToBufferEncryptBlock(), _iv);
				repository.WriteEncrypt((long)(_startPos + RawLength), info, _iv);
			}

			return true;
		}

		public static ushort GetNewInfSizeByBufLength(int length)
		{
			return (ushort)(Math.Ceiling(length / 16.0) * 16 + ReservLength);
		}

		public byte[] GetInfoBuf()
		{
			return _infDdata;
		}

		public void Delete()
		{
			_exists = false;

			if (_wasWroteInFile)
			{
				_repository.WriteByte((long)_startPos + 16, (byte)(Convert.ToByte(!_exists) * 128 + (int)CryptoRandom.Random(128)));
			}
		}

		public void Restore()
		{
			_exists = true;

			if (_wasWroteInFile)
			{
				_repository.WriteByte((long)_startPos + 16, (byte)(Convert.ToByte(!_exists) * 128 + (int)CryptoRandom.Random(128)));
			}
		}
	}
}
