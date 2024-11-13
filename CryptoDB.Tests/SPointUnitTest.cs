using CryptoDataBase.CryptoContainer.Types;

namespace CryptoDB.Tests
{
    public class SPointUnitTest
    {
        [Fact]
        public void CanCheckIntersectsOf2Points1()
        {
            //20-30
            var point1 = new SPoint(20, 10);
            //10-20
            var point2 = new SPoint(10, 10);

            Assert.True(point1.IsIntersects(point2));
            Assert.True(point2.IsIntersects(point1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Points2()
        {
            //20-30
            var point1 = new SPoint(20, 10);
            //10-21
            var point2 = new SPoint(10, 11);

            Assert.True(point1.IsIntersects(point2));
            Assert.True(point2.IsIntersects(point1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Points3()
        {
            //20-40
            var point1 = new SPoint(20, 20);
            //25-35
            var point2 = new SPoint(25, 10);

            Assert.True(point1.IsIntersects(point2));
            Assert.True(point2.IsIntersects(point1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Points4()
        {
            //20-40
            var point1 = new SPoint(20, 20);
            //25-35
            var point2 = new SPoint(25, 10);

            Assert.True(point1.IsIntersects(point2));
            Assert.True(point2.IsIntersects(point1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Points5()
        {
            //20-40
            var point1 = new SPoint(20, 20);
            //20-35
            var point2 = new SPoint(20, 15);

            Assert.True(point1.IsIntersects(point2));
            Assert.True(point2.IsIntersects(point1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Points6()
        {
            //20-40
            var point1 = new SPoint(20, 20);
            //25-40
            var point2 = new SPoint(25, 15);

            Assert.True(point1.IsIntersects(point2));
            Assert.True(point2.IsIntersects(point1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Points7()
        {
            //20-40
            var point1 = new SPoint(20, 20);
            //20-40
            var point2 = new SPoint(20, 20);

            Assert.True(point1.IsIntersects(point2));
            Assert.True(point2.IsIntersects(point1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Points8()
        {
            //20-30
            var point1 = new SPoint(20, 10);
            //10-19
            var point2 = new SPoint(10, 9);

            Assert.False(point1.IsIntersects(point2));
            Assert.False(point2.IsIntersects(point1));
        }
    }
}