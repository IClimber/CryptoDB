using CryptoDataBase.CryptoContainer.Types;

namespace CryptoDB.Tests
{
    public class SegmentUnitTest
    {
        [Fact]
        public void CanCheckIntersectsOf2Segments1()
        {
            //20-30
            var segment1 = new Segment(20, 10);
            //10-20
            var segment2 = new Segment(10, 10);

            Assert.True(segment1.IsIntersects(segment2));
            Assert.True(segment2.IsIntersects(segment1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Segments2()
        {
            //20-30
            var segment1 = new Segment(20, 10);
            //10-21
            var segment2 = new Segment(10, 11);

            Assert.True(segment1.IsIntersects(segment2));
            Assert.True(segment2.IsIntersects(segment1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Segments3()
        {
            //20-40
            var segment1 = new Segment(20, 20);
            //25-35
            var segment2 = new Segment(25, 10);

            Assert.True(segment1.IsIntersects(segment2));
            Assert.True(segment2.IsIntersects(segment1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Segments4()
        {
            //20-40
            var segment1 = new Segment(20, 20);
            //25-35
            var segment2 = new Segment(25, 10);

            Assert.True(segment1.IsIntersects(segment2));
            Assert.True(segment2.IsIntersects(segment1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Segments5()
        {
            //20-40
            var segment1 = new Segment(20, 20);
            //20-35
            var segment2 = new Segment(20, 15);

            Assert.True(segment1.IsIntersects(segment2));
            Assert.True(segment2.IsIntersects(segment1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Segments6()
        {
            //20-40
            var segment1 = new Segment(20, 20);
            //25-40
            var segment2 = new Segment(25, 15);

            Assert.True(segment1.IsIntersects(segment2));
            Assert.True(segment2.IsIntersects(segment1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Segments7()
        {
            //20-40
            var segment1 = new Segment(20, 20);
            //20-40
            var segment2 = new Segment(20, 20);

            Assert.True(segment1.IsIntersects(segment2));
            Assert.True(segment2.IsIntersects(segment1));
        }

        [Fact]
        public void CanCheckIntersectsOf2Segments8()
        {
            //20-30
            var segment1 = new Segment(20, 10);
            //10-19
            var segment2 = new Segment(10, 9);

            Assert.False(segment1.IsIntersects(segment2));
            Assert.False(segment2.IsIntersects(segment1));
        }
    }
}