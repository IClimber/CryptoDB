using CryptoDataBase.CryptoContainer.Services;

namespace CryptoDB.Tests
{
    public class FreeSpaceMapUnitTest
    {
        [Fact]
        public void CanGetFreeSpacePostionInEmptyMap()
        {
            var service = new FreeSpaceMapService(100);

            var start = service.GetFreeSpacePos(20, 100);

            Assert.Equal((ulong)0, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingFreeSpace()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(0, 20);
            var start = service.GetFreeSpacePos(20, 100);

            Assert.Equal((ulong)20, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingFreeSpace1()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(10, 20);
            var start = service.GetFreeSpacePos(20, 100);

            Assert.Equal((ulong)30, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingFreeSpace2()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(10, 20);
            service.RemoveFreeSpace(40, 10);
            service.RemoveFreeSpace(60, 20);
            var start = service.GetFreeSpacePos(20, 100);

            Assert.Equal((ulong)80, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingFreeSpace3()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(10, 20);
            service.RemoveFreeSpace(40, 10);
            service.RemoveFreeSpace(80, 20);
            var start = service.GetFreeSpacePos(30, 100);

            Assert.Equal((ulong)50, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingAndAddingFreeSpaceInTheSame0Position()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(0, 20);
            service.AddFreeSpace(0, 20);
            var start = service.GetFreeSpacePos(20, 100);

            Assert.Equal((ulong)0, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingAndAddingFreeSpaceInTheSameNot0Position()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(10, 20);
            service.AddFreeSpace(10, 20);
            var start = service.GetFreeSpacePos(20, 100);

            Assert.Equal((ulong)0, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingAndAddingFreeSpaceInTheSameNot0Position1()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(0, 20);
            service.AddFreeSpace(10, 10);
            var start = service.GetFreeSpacePos(10, 100);

            Assert.Equal((ulong)10, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingAndAddingFreeSpaceInTheSameNot0Position2()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(0, 20);
            service.AddFreeSpace(10, 10);
            var start = service.GetFreeSpacePos(10, 100);

            Assert.Equal((ulong)10, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingAndAddingFreeSpaceInTheSameNot0Position3()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(0, 20);
            service.AddFreeSpace(10, 9);
            var start = service.GetFreeSpacePos(9, 100);

            Assert.Equal((ulong)10, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingAndAddingFreeSpaceInTheSameNot0Position4()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(20, 60);
            service.AddFreeSpace(25, 50);
            var start = service.GetFreeSpacePos(50, 100);

            Assert.Equal((ulong)25, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingAndAddingFreeSpaceInTheSameNot0Position5()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(40, 20);
            service.AddFreeSpace(50, 10);
            var start = service.GetFreeSpacePos(50, 100);

            Assert.Equal((ulong)50, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingAndAddingFreeSpaceInTheSameNot0Position6()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(20, 20);
            service.RemoveFreeSpace(40, 20);
            service.RemoveFreeSpace(60, 10);
            var start = service.GetFreeSpacePos(30, 100);

            Assert.Equal((ulong)70, start);
        }

        //TODO
        //[Fact]
        /*public void CanGetFreeSpacePostionAfterRemovingAndAddingFreeSpaceInTheSameNot0Position7()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(20, 20);
            service.RemoveFreeSpace(40, 20);
            service.RemoveFreeSpace(60, 10);
            var start = service.GetFreeSpacePos(40, 100);

            Assert.Equal((ulong)70, start);
        }*/

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingAndAddingFreeSpaceInTheSameNot0Position8()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(20, 20);
            service.RemoveFreeSpace(40, 20);
            service.RemoveFreeSpace(60, 10);
            service.AddFreeSpace(50, 30);
            var start = service.GetFreeSpacePos(30, 100);

            Assert.Equal((ulong)50, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingAndAddingFreeSpaceInTheSameNot0Position9()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(20, 20);
            service.RemoveFreeSpace(40, 20);
            service.RemoveFreeSpace(60, 10);
            service.AddFreeSpace(30, 30);
            var start = service.GetFreeSpacePos(30, 100);

            Assert.Equal((ulong)30, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingAndAddingFreeSpaceInTheSameNot0Position10()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(20, 20);
            service.RemoveFreeSpace(40, 20);
            service.RemoveFreeSpace(60, 10);
            service.AddFreeSpace(30, 20);
            var start = service.GetFreeSpacePos(30, 100);

            Assert.Equal((ulong)70, start);
        }

        [Fact]
        public void CanGetFreeSpacePostionAfterRemovingAndAddingFreeSpaceInTheSameNot0Position11()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(20, 20);
            service.RemoveFreeSpace(50, 20);
            service.RemoveFreeSpace(60, 10);
            service.AddFreeSpace(20, 20);
            var start = service.GetFreeSpacePos(40, 100);

            Assert.Equal((ulong)0, start);
        }

        [Fact]
        public void CanGetFreeSpacePostion12()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(20, 20);
            service.RemoveFreeSpace(50, 20);
            service.RemoveFreeSpace(60, 10);
            service.AddFreeSpace(21, 20);
            var start = service.GetFreeSpacePos(29, 100);

            Assert.Equal((ulong)21, start);
        }

        [Fact]
        public void CanGetFreeSpacePostion13()
        {
            var service = new FreeSpaceMapService(100);

            service.RemoveFreeSpace(20, 20);
            service.RemoveFreeSpace(50, 20);
            service.RemoveFreeSpace(60, 10);
            service.AddFreeSpace(21, 20);
            service.AddFreeSpace(10, 20);
            var start = service.GetFreeSpacePos(40, 100);

            Assert.Equal((ulong)0, start);
        }
    }
}