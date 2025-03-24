using CryptoDataBase.CryptoContainer.Comparers;
using CryptoDataBase.CryptoContainer.Components;
using CryptoDataBase.CryptoContainer.Helpers;
using CryptoDataBase.CryptoContainer.Types;
using System;
using System.Collections.Generic;

namespace CryptoDataBase.CryptoContainer.Services
{
    public class FreeSpaceMapService
    {
        private object _freeSpaceMapLocker = new object();
        private BList<Segment> _freeSpaceMapSize = new BList<Segment>(new SegmentSizeComparer());
        private BList<Segment> _freeSpaceMapPos = new BList<Segment>(new SegmentPositionComparer());
        private readonly Dictionary<ulong, PositionCounter> _counter = new Dictionary<ulong, PositionCounter>();

        public FreeSpaceMapService(ulong freeSpaceSize)
        {
            if (freeSpaceSize > 0)
            {
                Segment freeSpace = new Segment(0, freeSpaceSize);
                _freeSpaceMapPos.Add(freeSpace);
                _freeSpaceMapSize.Add(freeSpace);
            }
        }

        public void RemoveFreeSpace(ulong start, ulong size)
        {
            lock (_freeSpaceMapLocker)
            {
                if (size == 0)
                {
                    return;
                }

                var segment = new Segment(start, size);
                CutSegment(segment);
                IncCounter(start);
            }
        }

        public ulong GetFreeSpacePos(ulong size, ulong fileSize)
        {
            lock (_freeSpaceMapLocker)
            {
                if (size == 0)
                {
                    return RandomHelper.Random(ulong.MaxValue - 2) + 2;
                }

                int indexBySize = _freeSpaceMapSize.GetGreaterOrEqualIndex(new Segment(0, size));

                if (indexBySize >= _freeSpaceMapSize.Count)
                {
                    IncCounter(fileSize);

                    return fileSize;
                }

                Segment segment = _freeSpaceMapSize[indexBySize];

                var result = segment.Start;

                IncCounter(result);
                _freeSpaceMapSize.RemoveAt(indexBySize);

                if (segment.Size == size)
                {
                    _freeSpaceMapPos.Remove(segment);
                }
                else
                {
                    segment.Start += size;
                    segment.Size -= size;

                    _freeSpaceMapSize.Add(segment);
                }

                return result;
            }
        }

        public void AddFreeSpace(ulong start, ulong size)
        {
            lock (_freeSpaceMapLocker)
            {
                if (size == 0)
                {
                    return;
                }

                if (DecCounter(start))
                {
                    return;
                }

                Segment freeSpace = new Segment(start, size);

                if (_freeSpaceMapPos.Count == 0)
                {
                    _freeSpaceMapPos.Add(freeSpace);
                    _freeSpaceMapSize.Add(freeSpace);

                    return;
                }

                MergeSegments(freeSpace);
            }
        }

        public bool IsFreeSpace(ulong start, ulong size)
        {
            lock (_freeSpaceMapLocker)
            {
                int index = _freeSpaceMapPos.GetLessOrEqualIndex(new Segment(start, size));

                if (index < 0)
                {
                    return false;
                }

                Segment segment = _freeSpaceMapPos[index];

                if (segment.End >= (start + size))
                {
                    return true;
                }

                return false;
            }
        }

        public ulong GetTotalFreeSpaceSize()
        {
            ulong size = 0;

            foreach (Segment segment in _freeSpaceMapSize)
            {
                size += segment.Size;
            }

            return size;
        }

        public ulong GetTotalSegmentsCount()
        {
            ulong size = 0;

            foreach (Segment segment in _freeSpaceMapSize)
            {
                size += segment.Size;
            }

            return size;
        }

        private List<Segment> GetIntersectsSegments(Segment segment, bool withLastStart = false)
        {
            var index = _freeSpaceMapPos.GetLessOrEqualIndex(segment);

            if (index < 0 && _freeSpaceMapPos.Count > 0)
            {
                index = 0;
            }

            if (index < 0)
            {
                return null;
            }

            var result = new List<Segment>();
            var currentSegment = _freeSpaceMapPos[index];

            while (segment.IsIntersects(currentSegment))
            {
                result.Add(currentSegment);

                index++;

                if (index >= _freeSpaceMapPos.Count)
                {
                    break;
                }

                currentSegment = _freeSpaceMapPos[index];
            };

            if (index == 0 && _freeSpaceMapPos.Count > 1 && segment.IsIntersects(_freeSpaceMapPos[1]))
            {
                result.Add(_freeSpaceMapPos[1]);
            }

            return result.Count > 0 ? result : null;
        }

        private void CutSegment(Segment segment)
        {
            var segments = GetIntersectsSegments(segment);

            if (segments == null)
            {
                return;
            }

            if (segments.Count > 1)
            {
                CutListOfSegments(segment, segments);

                return;
            }

            var firstSegment = segments[0];

            _freeSpaceMapSize.Remove(firstSegment);

            if (firstSegment.EaqualsValue(segment))
            {
                _freeSpaceMapPos.Remove(firstSegment);

                return;
            }

            if (firstSegment.End == segment.End)
            {
                firstSegment.Size -= segment.Size;
            }

            if (firstSegment.Start == segment.Start && firstSegment.Size > segment.Size)
            {
                firstSegment.Start = segment.End;
                firstSegment.Size -= segment.Size;
            }

            if (segment.IsFullIn(firstSegment))
            {
                var newSegment = new Segment(segment.End, firstSegment.End - segment.End);
                firstSegment.Size = segment.Start - firstSegment.Start;

                _freeSpaceMapPos.Add(newSegment);
                _freeSpaceMapSize.Add(newSegment);
            }

            _freeSpaceMapSize.Add(firstSegment);
        }

        private void CutListOfSegments(Segment segment, List<Segment> list)
        {
            if (list == null)
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                _freeSpaceMapPos.Remove(list[i]);
                _freeSpaceMapSize.Remove(list[i]);
            }

            var firstSegment = list[0];
            var lastSegment = list[list.Count - 1];

            if (firstSegment.Start < segment.Start)
            {
                Segment newFirstSegment = new Segment(firstSegment.Start, segment.Start - firstSegment.Start);

                _freeSpaceMapPos.Add(newFirstSegment);
                _freeSpaceMapSize.Add(newFirstSegment);
            }

            if ((lastSegment.Start + lastSegment.Size) > (segment.Start + segment.Size))
            {
                Segment newLastSegment = new Segment(segment.Start + segment.Size, lastSegment.Start + lastSegment.Size - (segment.Start + segment.Size));

                _freeSpaceMapPos.Add(newLastSegment);
                _freeSpaceMapSize.Add(newLastSegment);
            }
        }

        private void MergeSegments(Segment segment)
        {
            var segments = GetIntersectsSegments(segment, true);

            if (segments == null)
            {
                var newSegment = segment.Clone();
                _freeSpaceMapPos.Add(newSegment);
                _freeSpaceMapSize.Add(newSegment);

                return;
            }

            if (segments.Count > 1)
            {
                MergeListOfSegments(segment, segments);

                return;
            }

            var firstSegment = segments[0];

            _freeSpaceMapSize.Remove(firstSegment);

            firstSegment.Size = Math.Max(firstSegment.End, segment.End) - Math.Min(firstSegment.Start, segment.Start);
            firstSegment.Start = Math.Min(firstSegment.Start, segment.Start);

            _freeSpaceMapSize.Add(firstSegment);

            return;
        }

        private void MergeListOfSegments(Segment segment, List<Segment> list)
        {
            if (list == null)
            {
                return;
            }

            var firstSegmentIndex = list[0].End < segment.Start ? 1 : 0;
            var firstSegment = list[firstSegmentIndex];
            var lastSegment = list[list.Count - 1];

            _freeSpaceMapSize.Remove(firstSegment);
            firstSegment.Start = Math.Min(segment.Start, firstSegment.Start);
            firstSegment.Size = Math.Max(segment.End, lastSegment.End) - firstSegment.Start;
            _freeSpaceMapSize.Add(firstSegment);

            for (int i = firstSegmentIndex + 1; i < list.Count; i++)
            {
                _freeSpaceMapPos.Remove(list[i]);
                _freeSpaceMapSize.Remove(list[i]);
            }
        }

        private PositionCounter GetCounter(ulong start)
        {
            _counter.TryGetValue(start, out PositionCounter element);

            return element;
        }

        private void IncCounter(ulong start)
        {
            var element = GetCounter(start);

            if (element != null)
            {
                element.Count++;

                return;
            }

            _counter.Add(start, new PositionCounter());
        }

        private bool DecCounter(ulong start)
        {
            var element = GetCounter(start);

            if (element != null)
            {
                element.Count--;

                if (element.Count > 0)
                {
                    return true;
                }

                _counter.Remove(start);
            }

            return false;
        }
    }
}
