using System;
using System.Security.Cryptography;
using System.IO;

namespace CryptoDataBasev0
{
    public struct Head
    {
		public const byte Length = 33;
        public UInt64 StartPos { get; set; }
		public AesCryptoServiceProvider AES { get; set; }
		public byte[] IV { get; set; } //byte[16]
		public bool Exists { get; set; } //BlockStatus
		public ElementType ElType { get; set; }
		//Зробити InfSize приватним
		public UInt16 InfSize { get { return _InfSize; } set { _InfSize = (UInt16)(Math.Ceiling(value / 16.0) * 16 + _ReservLength); } }
		private UInt16 _InfSize;
		private const byte _ReservLength = 16;

		public Head(UInt64 startPos, UInt16 infSize, AesCryptoServiceProvider AES, ElementType blockType, bool exists)
		{
			IV = new byte[16];
			CryptoRandom.GetBytes(IV);

			this.AES = AES;
			StartPos = startPos;
			Exists = exists; //Якщо < 128 то файл існує, інакше видалений
			ElType = blockType;
			_InfSize = (UInt16)(Math.Ceiling(infSize / 16.0) * 16 + _ReservLength); //Збільшую довжину імені файла + системної інформації до кратне 16 + байти резерву;
		}

		public Head(Stream fileStream, UInt64 startPos, AesCryptoServiceProvider AES)
		{
			//try
			{
				byte[] buf = new byte[17];
				StartPos = startPos;
				fileStream.Position = (int)startPos;
				fileStream.Read(buf, 0, buf.Length);

				this.AES = AES;
				IV = new byte[16];
				Buffer.BlockCopy(buf, 0, IV, 0, 16);
				Exists = buf[16] < 128;

				this.AES.IV = IV;
				this.AES.Padding = PaddingMode.None;
				Crypto.AES_Decrypt(fileStream, buf, 16, this.AES);

				_InfSize = BitConverter.ToUInt16(buf, 13);
				ElType = (ElementType)(buf[15] / 128);
			}
			//finally
			{ }
		}

		public byte[] ToBuffer()
		{
			byte[] result = new byte[Length]; //не забути про доповнення кратне 16
			byte[] trash = new byte[13];
			CryptoRandom.GetBytes(trash);
			Buffer.BlockCopy(IV, 0, result, 0, IV.Length);
			byte BlockStatus = (byte)(Convert.ToByte(!Exists) * 128 + (int)CryptoRandom.Random(128));
			Buffer.BlockCopy(BitConverter.GetBytes(BlockStatus), 0, result, 16, 1);
			Buffer.BlockCopy(trash, 0, result, 17, trash.Length);
			Buffer.BlockCopy(BitConverter.GetBytes(_InfSize), 0, result, 30, 2);
			Buffer.BlockCopy(BitConverter.GetBytes((byte)ElType * 128 + (int)CryptoRandom.Random(128)), 0, result, 32, 1);

			return result;
		}

		public byte[] ToBufferFirstBlock()
		{
			byte[] result = new byte[17];
			Buffer.BlockCopy(IV, 0, result, 0, IV.Length);
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

		public void DeleteAndWrite(SafeStreamAccess fileStream)
		{
			Exists = false;
			fileStream.WriteByte((long)StartPos + 16, (byte)(Convert.ToByte(!Exists) * 128 + (int)CryptoRandom.Random(128)));
		}
	}
}
