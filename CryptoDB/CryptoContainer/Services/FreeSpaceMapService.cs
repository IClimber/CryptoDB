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
        private BList<SPoint> _freeSpaceMapSize = new BList<SPoint>(new SPointSizeComparer());
        private BList<SPoint> _freeSpaceMapPos = new BList<SPoint>(new SPointPositionComparer());
        private readonly Dictionary<ulong, PositionCounter> _counter = new Dictionary<ulong, PositionCounter>();

        public FreeSpaceMapService(ulong freeSpaceSize)
        {
            if (freeSpaceSize > 0)
            {
                SPoint freeSpace = new SPoint(0, freeSpaceSize);
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

                var point = new SPoint(start, size);
                CutPoints(point);
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

                int indexBySize = _freeSpaceMapSize.GetGreaterOrEqualIndex(new SPoint(0, size));

                if (indexBySize >= _freeSpaceMapSize.Count)
                {
                    IncCounter(fileSize);

                    return fileSize;
                }

                SPoint point = _freeSpaceMapSize[indexBySize];

                var result = point.Start;

                IncCounter(result);
                _freeSpaceMapSize.RemoveAt(indexBySize);

                if (point.Size == size)
                {
                    _freeSpaceMapPos.Remove(point);
                }
                else
                {
                    point.Start += size;
                    point.Size -= size;

                    _freeSpaceMapSize.Add(point);
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

                SPoint freeSpace = new SPoint(start, size);

                if (_freeSpaceMapPos.Count == 0)
                {
                    _freeSpaceMapPos.Add(freeSpace);
                    _freeSpaceMapSize.Add(freeSpace);

                    return;
                }

                MergePoints(freeSpace);
            }
        }

        public bool IsFreeSpace(ulong start, ulong size)
        {
            lock (_freeSpaceMapLocker)
            {
                int index = _freeSpaceMapPos.GetLessOrEqualIndex(new SPoint(start, size));

                if (index < 0)
                {
                    return false;
                }

                SPoint point = _freeSpaceMapPos[index];

                if (point.End >= (start + size))
                {
                    return true;
                }

                return false;
            }
        }

        private List<int> GetIntersectsIndexes(SPoint sPoint, bool withLastStart = false)
        {
            var index = _freeSpaceMapPos.GetLessOrEqualIndex(sPoint);

            if (index < 0)
            {
                return null;
            }

            var result = new List<int>();
            var point = _freeSpaceMapPos[index];

            while (point.End <= sPoint.End || (!withLastStart && point.Start < sPoint.End) || (withLastStart && point.Start <= sPoint.End))
            {
                result.Add(index);

                index++;

                if (index >= _freeSpaceMapPos.Count)
                {
                    break;
                }

                point = _freeSpaceMapPos[index];
            };

            return result.Count > 0 ? result : null;
        }

        private List<SPoint> GetIntersectsPoints(List<int> indexes)
        {
            if (indexes == null)
            {
                return null;
            }

            var result = new List<SPoint>();

            foreach (var index in indexes)
            {
                result.Add(_freeSpaceMapPos[index]);
            }

            return result.Count > 0 ? result : null;
        }

        private void CutPoints(SPoint sPoint)
        {
            var indexes = GetIntersectsIndexes(sPoint);

            if (indexes == null)
            {
                return;
            }

            if (indexes.Count > 1)
            {
                CutListOfPoints(sPoint, indexes);

                return;
            }

            var index = indexes[0];

            var point = _freeSpaceMapPos[index];

            _freeSpaceMapSize.Remove(point);

            if (point.EaqualsValue(sPoint))
            {
                _freeSpaceMapPos.RemoveAt(index);

                return;
            }

            if (point.End == sPoint.End)
            {
                point.Size -= sPoint.Size;
            }

            if (point.Start == sPoint.Start && point.Size > sPoint.Size)
            {
                point.Start = sPoint.End;
                point.Size -= sPoint.Size;
            }

            //sPoint full in point
            if (point.Start < sPoint.Start && point.End > sPoint.End)
            {
                var newPoint = new SPoint(sPoint.End, point.End - sPoint.End);
                point.Size = sPoint.Start - point.Start;

                _freeSpaceMapPos.Add(newPoint);
                _freeSpaceMapSize.Add(newPoint);
            }

            _freeSpaceMapSize.Add(point);
        }

        private void CutListOfPoints(SPoint sPoint, List<int> indexes)
        {
            var list = GetIntersectsPoints(indexes);

            if (list == null)
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                _freeSpaceMapPos.Remove(list[i]);
                _freeSpaceMapSize.Remove(list[i]);
            }

            var firstPoint = list[0];
            var lastPoint = list[list.Count - 1];

            if (firstPoint.Start < sPoint.Start)
            {
                SPoint newFirstPoint = new SPoint(firstPoint.Start, sPoint.Start - firstPoint.Start);

                _freeSpaceMapPos.Add(newFirstPoint);
                _freeSpaceMapSize.Add(newFirstPoint);
            }

            if ((lastPoint.Start + lastPoint.Size) > (sPoint.Start + sPoint.Size))
            {
                SPoint newLastPoint = new SPoint(sPoint.Start + sPoint.Size, lastPoint.Start + lastPoint.Size - (sPoint.Start + sPoint.Size));

                _freeSpaceMapPos.Add(newLastPoint);
                _freeSpaceMapSize.Add(newLastPoint);
            }
        }

        private void MergePoints(SPoint sPoint)
        {
            var indexes = GetIntersectsIndexes(sPoint, true);

            if (indexes == null)
            {
                return;
            }

            if (indexes.Count > 1)
            {
                MergeListOfPoints(sPoint, indexes);

                return;
            }

            var index = indexes[0];

            var point = _freeSpaceMapPos[index];

            //sPoint full in point
            if (point.End >= sPoint.End)
            {
                return;
            }

            if (point.End >= sPoint.Start)
            {
                point.Size = sPoint.End - point.Start;

                _freeSpaceMapSize.UpdatePosition(point);
            }
            else
            {
                var newPoint = sPoint.Clone();
                _freeSpaceMapPos.Add(newPoint);
                _freeSpaceMapSize.Add(newPoint);
            }
        }

        private void MergeListOfPoints(SPoint sPoint, List<int> indexes)
        {
            if (indexes == null)
            {
                return;
            }

            var list = GetIntersectsPoints(indexes);
            var firstPointIndex = list[0].End < sPoint.Start ? 1 : 0;
            var firstPoint = list[firstPointIndex];
            var lastPoint = list[list.Count - 1];

            firstPoint.Start = Math.Min(sPoint.Start, firstPoint.Start);
            firstPoint.Size = Math.Max(sPoint.End, lastPoint.End) - firstPoint.Start;
            _freeSpaceMapSize.UpdatePosition(firstPoint);

            for (int i = firstPointIndex + 1; i < list.Count; i++)
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
