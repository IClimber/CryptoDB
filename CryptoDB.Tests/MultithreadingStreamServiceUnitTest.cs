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

            var data = new byte[1000];
            RandomHelper.GetBytes(data);

            var dataStream = new MemoryStream(data);

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
            md5.TransformFinalBlock(data, 0, data.Length);
            var initHash = md5.Hash;
            dataStream.Position = 0;
            service.WriteEncrypt(dataStream, aes, out hash, null);

            Assert.Equal(initHash, hash);
        }

        [Fact]
        public void CanWriteAndReadStream()
        {
            MemoryStream stream = new MemoryStream();
            var service = new MultithreadingStreamService(stream);

            var data = new byte[1000];
            RandomHelper.GetBytes(data);

            var dataStream = new MemoryStream(data);

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
            dataStream.Position = 0;
            service.WriteEncrypt(dataStream, aes, out hash, null);

            var outputStream = new MemoryStream();
            service.MultithreadDecrypt(0, outputStream, data.Length, aes, null);

            Assert.Equal(data, outputStream.ToArray());
            Assert.NotEqual(data, stream.ToArray());
            Assert.NotEqual(data.Length, stream.Length);
        }

        [Fact]
        public void CanWriteAndReadStreamPartialy()
        {
            MemoryStream stream = new MemoryStream();
            var service = new MultithreadingStreamService(stream);

            var data = new byte[5000000];
            RandomHelper.GetBytes(data);

            var dataStream = new MemoryStream(data);

            var key = new Rfc2898DeriveBytes("test", 32, 1);

            var aes = new AesCryptoServiceProvider()
            {
                KeySize = 256,
                BlockSize = 128,
                Key = key.GetBytes(32),
                Mode = CipherMode.CBC,
                Padding = PaddingMode.ISO10126
            };

            MD5 md5 = MD5.Create();
            md5.TransformFinalBlock(data, 0, data.Length);
            var initHash = md5.Hash;

            byte[] hash;
            dataStream.Position = 0;
            service.WriteEncrypt(dataStream, aes, out hash, null);

            var outputStream = new MemoryStream();
            service.MultithreadDecrypt(0, outputStream, data.Length, aes, null);

            Assert.Equal(initHash, hash);
            Assert.Equal(data, outputStream.ToArray());
            Assert.NotEqual(data, stream.ToArray());
            Assert.NotEqual(data.Length, stream.Length);
        }

        [Fact]
        public void CanWriteAndReadArray()
        {
            MemoryStream stream = new MemoryStream();
            var service = new MultithreadingStreamService(stream);

            var data = new byte[1000];
            RandomHelper.GetBytes(data);

            var key = new Rfc2898DeriveBytes("test", 32, 1);

            var aes = new AesCryptoServiceProvider()
            {
                KeySize = 256,
                BlockSize = 128,
                Key = key.GetBytes(32),
                Mode = CipherMode.CBC,
                Padding = PaddingMode.ISO10126
            };

            service.WriteEncrypt(data, aes);

            var outputStream = new MemoryStream();
            service.MultithreadDecrypt(0, outputStream, data.Length, aes, null);

            Assert.Equal(data, outputStream.ToArray());
            Assert.NotEqual(data, stream.ToArray());
            Assert.NotEqual(data.Length, stream.Length);
        }

        [Fact]
        public void CanWriteArrayWithoutEncription()
        {
            MemoryStream stream = new MemoryStream();
            var service = new MultithreadingStreamService(stream);

            var data = new byte[1000];
            RandomHelper.GetBytes(data);

            service.Write(0, data, 0, data.Length);


            Assert.Equal(data, stream.ToArray());
            Assert.Equal(data.Length, stream.Length);
        }

        [Fact]
        public void CanWriteArrayWithOffsetWithoutEncription()
        {
            var initData= new byte[1000];
            RandomHelper.GetBytes(initData);

            MemoryStream stream = new MemoryStream();
            stream.Write(initData, 0, initData.Length);
            var service = new MultithreadingStreamService(stream);

            var data = new byte[1000];
            RandomHelper.GetBytes(data);

            service.Write(100, data, 0, data.Length);

            var outputData = new byte[1000];
            stream.Position = 100;
            stream.Read(outputData, 0, data.Length);

            Assert.Equal(data, outputData);
            Assert.Equal(1100, stream.Length);
        }
    }
}