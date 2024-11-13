using CryptoDataBase.CryptoContainer.Services;
using CryptoDataBase.CryptoContainer.Helpers;
using System.Security.Cryptography;

namespace CryptoDB.Tests
{
    public class MultithreadingStreamServiceUnitTest
    {
        [Fact]
        public void CanWriteStreamWithRightMD5Hash()
        {
            MemoryStream stream = new MemoryStream();
            var service = new MultithreadingStreamService(stream);

            var buf = new byte[1000];
            RandomHelper.GetBytes(buf);

            var bufStream = new MemoryStream(buf);

            var key = new Rfc2898DeriveBytes("test", 32, 1);

            var aes = new AesCryptoServiceProvider()
            {
                KeySize = 256,
                BlockSize = 128,
                Key = key.GetBytes(32),
                Mode = CipherMode.CBC,
                Padding = PaddingMode.ISO10126
            };

            byte[] hash;

            MD5 md5 = MD5.Create();
            md5.TransformFinalBlock(buf, 0, buf.Length);
            var initHash = md5.Hash;
            bufStream.Position = 0;
            service.WriteEncrypt(bufStream, aes, out hash, null);

            Assert.Equal(initHash, hash);
        }

        [Fact]
        public void CanWriteAndReadStream()
        {
            MemoryStream stream = new MemoryStream();
            var service = new MultithreadingStreamService(stream);

            var buf = new byte[1000];
            RandomHelper.GetBytes(buf);

            var bufStream = new MemoryStream(buf);

            var key = new Rfc2898DeriveBytes("test", 32, 1);

            var aes = new AesCryptoServiceProvider()
            {
                KeySize = 256,
                BlockSize = 128,
                Key = key.GetBytes(32),
                Mode = CipherMode.CBC,
                Padding = PaddingMode.ISO10126
            };

            byte[] hash;
            bufStream.Position = 0;
            service.WriteEncrypt(bufStream, aes, out hash, null);

            var outputStream = new MemoryStream();
            service.MultithreadDecrypt(0, outputStream, buf.Length, aes, null);

            Assert.Equal(buf, outputStream.ToArray());
            Assert.NotEqual(buf, stream.ToArray());
            Assert.NotEqual(buf.Length, stream.Length);
        }

        [Fact]
        public void CanWriteAndReadStreamPartialy()
        {
            MemoryStream stream = new MemoryStream();
            var service = new MultithreadingStreamService(stream);

            var buf = new byte[5000000];
            RandomHelper.GetBytes(buf);

            var bufStream = new MemoryStream(buf);

            var key = new Rfc2898DeriveBytes("test", 32, 1);

            var aes = new AesCryptoServiceProvider()
            {
                KeySize = 256,
                BlockSize = 128,
                Key = key.GetBytes(32),
                Mode = CipherMode.CBC,
                Padding = PaddingMode.ISO10126
            };

            byte[] hash;
            bufStream.Position = 0;
            service.WriteEncrypt(bufStream, aes, out hash, null);

            var outputStream = new MemoryStream();
            service.MultithreadDecrypt(0, outputStream, buf.Length, aes, null);

            Assert.Equal(buf, outputStream.ToArray());
            Assert.NotEqual(buf, stream.ToArray());
            Assert.NotEqual(buf.Length, stream.Length);
        }
    }
}