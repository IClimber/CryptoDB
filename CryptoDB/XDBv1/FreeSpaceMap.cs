using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataBase
{
	public class FreeSpaceMap
	{
		private Object _freeSpaceMapLocker = new Object();
		private List<SPoint> freeSpaceMapSize = new List<SPoint>();
		private List<SPoint> freeSpaceMapPos = new List<SPoint>();

		public FreeSpaceMap(long freeSpaceSize)
		{
			SPoint freeSpace = new SPoint(0, (ulong)freeSpaceSize);
			freeSpaceMapPos.Add(freeSpace);
			freeSpaceMapSize.Add(freeSpace);
		}

		private int GetIndexBySize(UInt64 Size)
		{
			int index = -1;
			int min = 0,
				max = freeSpaceMapSize.Count - 1;

			while (min <= max)
			{
				int mid = (min + max) / 2;
				if (Size == freeSpaceMapSize[mid].Size)
				{
					return mid;
				}
				else if (Size < freeSpaceMapSize[mid].Size)
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

		private int GetIndexByPos(UInt64 startPos)
		{
			int index = -1;
			int min = 0;
			int max = freeSpaceMapPos.Count - 1;

			while (min <= max)
			{
				int mid = (min + max) / 2;
				if (startPos == freeSpaceMapPos[mid].Start)
				{
					return mid;
				}
				else if (startPos < freeSpaceMapPos[mid].Start)
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

		private SPoint _DivideFreeSpacePoint(SPoint sPoint, UInt64 start, UInt64 size)
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

				int indexByPos = GetIndexByPos(start);

				if (indexByPos < 0)
				{
					return;
				}

				SPoint sPoint = freeSpaceMapPos[indexByPos];
				SPoint newSPoint = _DivideFreeSpacePoint(sPoint, start, length);

				freeSpaceMapSize.Remove(sPoint);

				if (sPoint.Size == 0)
				{
					freeSpaceMapPos.RemoveAt(indexByPos);

					return;
				}

				int indexBySize = freeSpaceMapSize.BinarySearch(sPoint, new PSizeComparer());
				indexBySize = indexBySize < 0 ? Math.Abs(indexBySize) - 1 : indexBySize;

				freeSpaceMapSize.Insert(indexBySize, sPoint);

				if (newSPoint != null)
				{
					//indexByPos = freeSpaceMapPos.BinarySearch(newSPoint, new PosComparer());
					//indexByPos = indexByPos < 0 ? Math.Abs(indexByPos) - 1 : indexByPos;
					indexByPos++;

					indexBySize = freeSpaceMapSize.BinarySearch(newSPoint, new PSizeComparer());
					indexBySize = indexBySize < 0 ? Math.Abs(indexBySize) - 1 : indexBySize;

					freeSpaceMapPos.Insert(indexByPos, newSPoint);
					freeSpaceMapSize.Insert(indexBySize, newSPoint);

					return;
				}

				return;
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

				int indexBySize = GetIndexBySize(size);

				if (indexBySize < 0)
				{
					return result;
				}

				SPoint sPoint = freeSpaceMapSize[indexBySize];

				result = sPoint.Start;

				if (!withWrite)
				{
					return result;
				}

				freeSpaceMapSize.RemoveAt(indexBySize);

				if ((sPoint.Size - size) == 0)
				{
					int indexByPos = GetIndexByPos(sPoint.Start);
					freeSpaceMapPos.RemoveAt(indexByPos);
					return result;
				}

				sPoint.Start += size;
				sPoint.Size -= size;

				indexBySize = freeSpaceMapSize.BinarySearch(sPoint, new PSizeComparer());
				indexBySize = indexBySize < 0 ? Math.Abs(indexBySize) - 1 : indexBySize;

				freeSpaceMapSize.Insert(indexBySize, sPoint);


				return result;
			}
		}
	}
}
