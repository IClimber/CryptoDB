using System.Collections.Generic;

namespace CryptoDataBase.CryptoContainer.Components
{
    public class BList<T> : List<T>
    {
        private IComparer<T> comparer;

        public BList() : this(null)
        {

        }

        public BList(IComparer<T> comparer)
        {
            if (comparer == null)
            {
                this.comparer = Comparer<T>.Default;
            }
            else
            {
                this.comparer = comparer;
            }
        }

        public new void Add(T item)
        {
            var index = GetGreaterOrEqualIndex(item);

            Insert(index, item);
        }

        public new void Remove(T item)
        {
            var index = GetItemIndex(item);

            RemoveAt(index);
        }

        public int GetGreaterOrEqualIndex(T item)
        {
            int index = Count;
            int min = 0;
            int max = Count - 1;

            while (min <= max)
            {
                int mid = (min + max) / 2;
                int num = comparer.Compare(item, this[mid]);

                if (num == 0)
                {
                    return mid;
                }

                if (num < 0)
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

        public int GetLessOrEqualIndex(T item)
        {
            int index = -1;
            int min = 0;
            int max = Count - 1;

            while (min <= max)
            {
                int mid = (min + max) / 2;
                int num = comparer.Compare(item, this[mid]);

                if (num == 0)
                {
                    return mid;
                }

                if (num < 0)
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

        public int GetItemIndex(T item)
        {
            int index = -1;
            int min = 0;
            int max = Count - 1;

            while (min <= max)
            {
                int mid = (min + max) / 2;
                int num = comparer.Compare(item, this[mid]);

                if (num == 0)
                {
                    return mid;
                }

                if (num < 0)
                {
                    max = mid - 1;
                }
                else
                {
                    min = mid + 1;
                }
            }

            return index;
        }
    }
}
