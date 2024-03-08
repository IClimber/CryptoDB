using CryptoDataBase.CryptoContainer.Comparers;
using CryptoDataBase.CryptoContainer.Components;
using CryptoDataBase.CryptoContainer.Exceptions;
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
        private readonly HashSet<PositionCounter> _counter = new HashSet<PositionCounter>();

        public FreeSpaceMapService(long freeSpaceSize)
        {
            if (freeSpaceSize > 0)
            {
                SPoint freeSpace = new SPoint(0, (ulong)freeSpaceSize);
                _freeSpaceMapPos.Add(freeSpace);
                _freeSpaceMapSize.Add(freeSpace);
            }
        }

        //Просто стирає вільне місце з карти вільного місця
        public void RemoveFreeSpace(ulong start, ulong length)
        {
            lock (_freeSpaceMapLocker)
            {
                if (length == 0)
                {
                    return;
                }

                var point = new SPoint(start, length);
                MergePoints(point);
                IncCounter(start);
            }
        }

        public ulong GetFreeSpacePos(ulong size, long fileSize)
        {
            lock (_freeSpaceMapLocker)
            {
                if (size == 0)
                {
                    return RandomHelper.Random(ulong.MaxValue - 2) + 2;
                }

                ulong result = (ulong)fileSize;

                int indexBySize = _freeSpaceMapSize.GetGreaterOrEqualIndex(new SPoint(0, size));

                if (indexBySize >= _freeSpaceMapSize.Count)
                {
                    IncCounter(result);

                    return result;
                }

                SPoint sPoint = _freeSpaceMapSize[indexBySize];

                result = sPoint.Start;

                IncCounter(result);
                _freeSpaceMapSize.RemoveAt(indexBySize);

                if ((sPoint.Size - size) == 0)
                {
                    int indexByPos = _freeSpaceMapPos.GetLessOrEqualIndex(new SPoint(sPoint.Start, 0));
                    _freeSpaceMapPos.RemoveAt(indexByPos);

                    return result;
                }

                sPoint.Start += size;
                sPoint.Size -= size;

                InsertSPointToSizeMap(sPoint);

                return result;
            }
        }

        public bool AddFreeSpace(ulong start, ulong length)
        {
            lock (_freeSpaceMapLocker)
            {
                if (length == 0)
                {
                    return true;
                }

                if (DecCounter(start))
                {
                    return true;
                }

                if (_freeSpaceMapPos.Count == 0)
                {
                    SPoint freeSpace = new SPoint(start, length);
                    _freeSpaceMapPos.Add(freeSpace);
                    _freeSpaceMapSize.Add(freeSpace);

                    return true;
                }

                int indexByPos = _freeSpaceMapPos.GetLessOrEqualIndex(new SPoint(start, 0));

                SPoint prevPoint = null;
                SPoint nextPoint = null;

                if (indexByPos >= 0)
                {
                    prevPoint = _freeSpaceMapPos[indexByPos];
                }

                if ((indexByPos + 1) < _freeSpaceMapPos.Count)
                {
                    nextPoint = _freeSpaceMapPos[indexByPos + 1];
                }

                if ((prevPoint != null && start < (prevPoint.Start + prevPoint.Size)) || (nextPoint != null && (start + length) > nextPoint.Start))
                {
                    return false;
                }

                if (prevPoint != null && nextPoint != null && start == (prevPoint.Start + prevPoint.Size) && (start + length) == nextPoint.Start)
                {
                    prevPoint.Size += length + nextPoint.Size;
                    _freeSpaceMapSize.Remove(prevPoint);
                    InsertSPointToSizeMap(prevPoint);

                    _freeSpaceMapPos.RemoveAt(indexByPos + 1);
                    _freeSpaceMapSize.Remove(nextPoint);

                    return true;
                }

                if (prevPoint != null && start == (prevPoint.Start + prevPoint.Size))
                {
                    prevPoint.Size += length;
                    _freeSpaceMapSize.Remove(prevPoint);
                    InsertSPointToSizeMap(prevPoint);

                    return true;
                }

                if (nextPoint != null && (start + length) == nextPoint.Start)
                {
                    nextPoint.Start -= length;
                    nextPoint.Size += length;
                    _freeSpaceMapSize.Remove(nextPoint);
                    InsertSPointToSizeMap(nextPoint);

                    return true;
                }

                SPoint newSPoint = new SPoint(start, length);

                _freeSpaceMapPos.Insert(indexByPos + 1, newSPoint);
                InsertSPointToSizeMap(newSPoint);

                return true;
            }
        }

        public bool IsFreeSpace(ulong start, ulong size)
        {
            int index = _freeSpaceMapPos.GetLessOrEqualIndex(new SPoint(start, 0));
            SPoint sPoint = index >= 0 ? _freeSpaceMapPos[index] : null;

            if (sPoint == null)
            {
                return false;
            }

            if (sPoint.Start <= start && (sPoint.Start + sPoint.Size) >= (start + size))
            {
                return true;
            }

            return false;
        }

        private List<int> GetIntersectsIndexes(SPoint sPoint)
        {
            var index = _freeSpaceMapPos.GetLessOrEqualIndex(sPoint);

            if (index < 0)
            {
                return null;
            }

            var result = new List<int>();
            var point = _freeSpaceMapPos[index];

            while (point.End <= sPoint.End || (point.Start < (sPoint.End)))
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

        private void MergePoints(SPoint sPoint)
        {
            var indexes = GetIntersectsIndexes(sPoint);

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

            _freeSpaceMapSize.Remove(point);

            if (point.EaqualsValue(sPoint))
            {
                _freeSpaceMapPos.RemoveAt(index);

                return;
            }

            if (point.Start < sPoint.Start && point.End == sPoint.End)
            {
                point.Size = sPoint.Start - point.Start;
            }

            if (point.Start == sPoint.Start && point.Size > sPoint.Size)
            {
                point.Start = sPoint.End;
                point.Size = point.Size - sPoint.Size;
            }

            if (point.Start < sPoint.Start && point.End > sPoint.End)
            {
                var newPoint = new SPoint(sPoint.End, point.End - sPoint.End);
                point.Size = sPoint.Start - point.Start;

                _freeSpaceMapPos.Add(newPoint);
                _freeSpaceMapSize.Add(newPoint);
            }

            _freeSpaceMapSize.Add(point);
        }

        private void MergeListOfPoints(SPoint sPoint, List<int> indexes)
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

        private void InsertSPointToSizeMap(SPoint sPoint)
        {
            int indexBySize = _freeSpaceMapSize.BinarySearch(sPoint, new SPointSizeComparer());
            indexBySize = indexBySize < 0 ? Math.Abs(indexBySize) - 1 : indexBySize;

            _freeSpaceMapSize.Insert(indexBySize, sPoint);
        }

        private PositionCounter GetCounter(ulong start)
        {

            _counter.TryGetValue(new PositionCounter(start), out PositionCounter element);

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

            _counter.Add(new PositionCounter(start));
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

                _counter.Remove(element);
            }

            return false;
        }
    }
}
