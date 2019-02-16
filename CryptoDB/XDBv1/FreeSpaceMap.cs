﻿using System;
using System.Collections.Generic;

namespace CryptoDataBase
{
	public class FreeSpaceMap
	{
		private Object _freeSpaceMapLocker = new Object();
		private List<SPoint> _freeSpaceMapSize = new List<SPoint>();
		private List<SPoint> _freeSpaceMapPos = new List<SPoint>();

		public FreeSpaceMap(long freeSpaceSize)
		{
			SPoint freeSpace = new SPoint(0, (ulong)freeSpaceSize);
			if (freeSpaceSize > 0)
			{
				_freeSpaceMapPos.Add(freeSpace);
				_freeSpaceMapSize.Add(freeSpace);
			}
		}

		private int _GetIndexBySize(UInt64 Size)
		{
			int index = -1;
			int min = 0,
				max = _freeSpaceMapSize.Count - 1;

			while (min <= max)
			{
				int mid = (min + max) / 2;
				if (Size == _freeSpaceMapSize[mid].Size)
				{
					return mid;
				}
				else if (Size < _freeSpaceMapSize[mid].Size)
				{
					max = mid - 1;
					index = mid;
				}
				else
				{
					min = mid + 1;
				}
			}
			return index;
		}

		private int _GetIndexByPos(UInt64 startPos)
		{
			int index = -1;
			int min = 0;
			int max = _freeSpaceMapPos.Count - 1;

			while (min <= max)
			{
				int mid = (min + max) / 2;
				if (startPos == _freeSpaceMapPos[mid].Start)
				{
					return mid;
				}
				else if (startPos < _freeSpaceMapPos[mid].Start)
				{
					max = mid - 1;
				}
				else
				{
					min = mid + 1;
					index = mid;
				}
			}
			return index;
		}

		private SPoint _SeparateFreeSpacePoint(SPoint sPoint, UInt64 start, UInt64 size)
		{
			if (size > sPoint.Size)
			{
				throw new Exception("Розмір файлу не може бути більше ніж розмір вільного місця");
			}

			if (start < sPoint.Start)
			{
				throw new Exception("Не туди вставляєш");
			}

			if (start > sPoint.Start)
			{
				if ((start + size) < (sPoint.Start + sPoint.Size))
				{
					SPoint result = sPoint.Clone();
					sPoint.Size = start - sPoint.Start;

					result.Start = start + size;
					result.Size -= sPoint.Size + size;

					return result;
				}
				else
				{
					sPoint.Size = start - sPoint.Start;
				}
			}
			else
			{
				if (size < sPoint.Size)
				{
					sPoint.Start += size;
					sPoint.Size -= size;
				}
				else
				{
					sPoint.Size = 0;
				}
			}

			return null;
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

				int indexByPos = _GetIndexByPos(start);

				if (indexByPos < 0)
				{
					return;
				}

				SPoint sPoint = _freeSpaceMapPos[indexByPos];
				SPoint newSPoint = _SeparateFreeSpacePoint(sPoint, start, length);

				_freeSpaceMapSize.Remove(sPoint);

				if (sPoint.Size == 0)
				{
					_freeSpaceMapPos.RemoveAt(indexByPos);

					return;
				}

				_InsertSPointToSizeMap(sPoint);

				if (newSPoint != null)
				{
					//indexByPos = freeSpaceMapPos.BinarySearch(newSPoint, new PosComparer());
					//indexByPos = indexByPos < 0 ? Math.Abs(indexByPos) - 1 : indexByPos;
					
					_InsertSPointToSizeMap(newSPoint);
					_freeSpaceMapPos.Insert(indexByPos + 1, newSPoint);
				}
			}
		}

		public ulong GetFreeSpacePos(UInt64 size, long fileSize, bool withWrite = true)
		{
			lock (_freeSpaceMapLocker)
			{
				if (size == 0)
				{
					return CryptoRandom.Random(UInt64.MaxValue - 2) + 2;
				}

				UInt64 result = (ulong)fileSize;

				int indexBySize = _GetIndexBySize(size);

				if (indexBySize < 0)
				{
					return result;
				}

				SPoint sPoint = _freeSpaceMapSize[indexBySize];

				result = sPoint.Start;

				if (!withWrite)
				{
					return result;
				}

				_freeSpaceMapSize.RemoveAt(indexBySize);

				if ((sPoint.Size - size) == 0)
				{
					int indexByPos = _GetIndexByPos(sPoint.Start);
					_freeSpaceMapPos.RemoveAt(indexByPos);
					return result;
				}

				sPoint.Start += size;
				sPoint.Size -= size;

				_InsertSPointToSizeMap(sPoint);

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

				if (_freeSpaceMapPos.Count == 0)
				{
					SPoint freeSpace = new SPoint(start, length);
					_freeSpaceMapPos.Add(freeSpace);
					_freeSpaceMapSize.Add(freeSpace);

					return true;
				}

				int indexByPos = _GetIndexByPos(start);

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
					_InsertSPointToSizeMap(prevPoint);

					_freeSpaceMapPos.RemoveAt(indexByPos + 1);
					_freeSpaceMapSize.Remove(nextPoint);

					return true;
				}

				if (prevPoint != null && start == (prevPoint.Start + prevPoint.Size))
				{
					prevPoint.Size += length;
					_freeSpaceMapSize.Remove(prevPoint);
					_InsertSPointToSizeMap(prevPoint);

					return true;
				}

				if (nextPoint != null && (start + length) == nextPoint.Start)
				{
					nextPoint.Start -= length;
					nextPoint.Size += length;
					_freeSpaceMapSize.Remove(nextPoint);
					_InsertSPointToSizeMap(nextPoint);

					return true;
				}

				SPoint newSPoint = new SPoint(start, length);

				_freeSpaceMapPos.Insert(indexByPos + 1, newSPoint);
				_InsertSPointToSizeMap(newSPoint);

				return true;
			}
		}

		private void _InsertSPointToSizeMap(SPoint sPoint)
		{
			int indexBySize = _freeSpaceMapSize.BinarySearch(sPoint, new PSizeComparer());
			indexBySize = indexBySize < 0 ? Math.Abs(indexBySize) - 1 : indexBySize;

			_freeSpaceMapSize.Insert(indexBySize, sPoint);
		}
	}
}
