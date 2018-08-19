using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gosub.Http
{
    /// <summary>
    /// List with constant time removal from beginning
    /// </summary>
    class QueueList<T>
    {
        static T[] sEmptyArray = new T[0];
        T[] mArray = sEmptyArray;
        int mCount;
        int mHead = 0;

        // Array length is always a power of two, so mArray.Length-1
        // is a mask for wrap around buffer starting at mHead
        int Index(int index) => (mHead + index) & (mArray.Length - 1);

        public int Count => mCount;

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= mCount)
                    throw new IndexOutOfRangeException();
                return mArray[Index(index)];
            }
            set
            {
                if (index < 0 || index >= mCount)
                    throw new IndexOutOfRangeException();
                mArray[Index(index)] = value;
            }
        }

        void EnsureCapacity(int count)
        {
            if (count <= mArray.Length)
                return;
            if (count >= int.MaxValue / 4)
                throw new OverflowException("QueueList is too big!");

            // Capicity is always a power of two
            int capacity = Math.Max(4, mArray.Length);
            while (capacity < count)
                capacity *= 2;

            // Copy elements
            var newArray = new T[capacity];
            int mask = mArray.Length - 1;
            for (int i = 0; i < mCount; i++)
                newArray[i] = mArray[mHead++ & mask];

            mArray = newArray;
            mHead = 0;
        }

        /// <summary>
        /// Push element to end of list
        /// </summary>
        public void Push(T value)
        {
            EnsureCapacity(mCount + 1);
            mArray[Index(mCount++)] = value;
        }

        /// <summary>
        /// Pop element from end of list
        /// </summary>
        public T Pop()
        {
            if (mCount <= 0)
                throw new InvalidOperationException("List empty");
            var index = Index(--mCount);
            var value = mArray[index];
            mArray[index] = default(T);
            return value;
        }

        /// <summary>
        /// Push element to end of list
        /// </summary>
        public void Enqueue(T value)
        {
            Push(value);
        }

        /// <summary>
        /// Remove element from beginning of list
        /// </summary>
        public T Dequeue()
        {
            if (mCount <= 0)
                throw new InvalidOperationException("List empty");
            mCount--;
            var index = mHead++ & (mArray.Length - 1);
            var value = mArray[index];
            mArray[index] = default(T);
            return value;
        }

    }
}
