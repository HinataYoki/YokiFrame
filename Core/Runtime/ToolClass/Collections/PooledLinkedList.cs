using System;
using System.Collections;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Doubly linked list that reuses detached nodes through an internal pool.
    /// </summary>
    public class PooledLinkedList<T> : IEnumerable<T>
    {
        private readonly LinkedList<T> mLinkedList = new();
        private readonly Stack<LinkedListNode<T>> mNodePool;
        private const int DEFAULT_POOL_CAPACITY = 64;
        private int mMaxPoolSize;

        /// <summary>
        /// Number of items currently stored in the linked list.
        /// </summary>
        public int Count => mLinkedList.Count;

        /// <summary>
        /// Number of cached nodes currently held in the pool.
        /// </summary>
        public int PoolSize => mNodePool.Count;

        /// <summary>
        /// First node in the list.
        /// </summary>
        public LinkedListNode<T> First => mLinkedList.First;

        /// <summary>
        /// Last node in the list.
        /// </summary>
        public LinkedListNode<T> Last => mLinkedList.Last;

        /// <summary>
        /// Maximum number of detached nodes retained in the pool.
        /// </summary>
        public int MaxPoolSize
        {
            get => mMaxPoolSize;
            set => mMaxPoolSize = value >= 0 ? value : DEFAULT_POOL_CAPACITY;
        }

        /// <summary>
        /// Creates a pooled linked list.
        /// </summary>
        /// <param name="initialPoolCapacity">Initial pool capacity.</param>
        public PooledLinkedList(int initialPoolCapacity = DEFAULT_POOL_CAPACITY)
        {
            mMaxPoolSize = initialPoolCapacity;
            mNodePool = new Stack<LinkedListNode<T>>(initialPoolCapacity);
        }

        /// <summary>
        /// Preallocates nodes into the pool.
        /// </summary>
        public void Prewarm(int count)
        {
            int toCreate = Math.Min(count, mMaxPoolSize) - mNodePool.Count;
            for (int i = 0; i < toCreate; i++)
            {
                mNodePool.Push(new LinkedListNode<T>(default));
            }
        }

        /// <summary>
        /// Adds an item to the end of the list.
        /// </summary>
        public LinkedListNode<T> AddLast(T value)
        {
            var node = GetNode(value);
            mLinkedList.AddLast(node);
            return node;
        }

        /// <summary>
        /// Adds an item to the beginning of the list.
        /// </summary>
        public LinkedListNode<T> AddFirst(T value)
        {
            var node = GetNode(value);
            mLinkedList.AddFirst(node);
            return node;
        }

        /// <summary>
        /// Inserts an item after the specified node.
        /// </summary>
        public LinkedListNode<T> InsertAfter(LinkedListNode<T> node, T value)
        {
            if (node is null) throw new ArgumentNullException(nameof(node));
            if (node.List != mLinkedList)
                throw new InvalidOperationException("The specified node does not belong to this list.");

            var newNode = GetNode(value);
            mLinkedList.AddAfter(node, newNode);
            return newNode;
        }

        /// <summary>
        /// Inserts an item before the specified node.
        /// </summary>
        public LinkedListNode<T> InsertBefore(LinkedListNode<T> node, T value)
        {
            if (node is null) throw new ArgumentNullException(nameof(node));
            if (node.List != mLinkedList)
                throw new InvalidOperationException("The specified node does not belong to this list.");

            var newNode = GetNode(value);
            mLinkedList.AddBefore(node, newNode);
            return newNode;
        }

        /// <summary>
        /// Removes the first node whose value matches the supplied item.
        /// </summary>
        public bool Remove(T value)
        {
            var node = mLinkedList.Find(value);
            if (node != null)
            {
                RemoveNode(node);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes the specified node.
        /// </summary>
        public void Remove(LinkedListNode<T> node)
        {
            if (node is null) throw new ArgumentNullException(nameof(node));
            if (node.List != mLinkedList) return;
            RemoveNode(node);
        }

        /// <summary>
        /// Removes the first node.
        /// </summary>
        public void RemoveFirst()
        {
            if (mLinkedList.First != null)
            {
                RemoveNode(mLinkedList.First);
            }
        }

        /// <summary>
        /// Removes the last node.
        /// </summary>
        public void RemoveLast()
        {
            if (mLinkedList.Last != null)
            {
                RemoveNode(mLinkedList.Last);
            }
        }

        /// <summary>
        /// Clears the list and returns nodes to the pool.
        /// </summary>
        public void Clear()
        {
            while (mLinkedList.First != null)
            {
                RemoveFirst();
            }
        }

        /// <summary>
        /// Returns whether the list contains the specified value.
        /// </summary>
        public bool Contains(T value) => mLinkedList.Contains(value);

        /// <summary>
        /// Finds the first node containing the specified value.
        /// </summary>
        public LinkedListNode<T> Find(T value) => mLinkedList.Find(value);

        /// <summary>
        /// Enumerates items in reverse order.
        /// </summary>
        public IEnumerable<T> Reverse()
        {
            var node = mLinkedList.Last;
            while (node != null)
            {
                yield return node.Value;
                node = node.Previous;
            }
        }

        /// <summary>
        /// Enumerates items in forward order.
        /// </summary>
        public IEnumerator<T> GetEnumerator() => mLinkedList.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Gets the value at the specified index.
        /// </summary>
        /// <remarks>
        /// This is a linear-time lookup and should not be used in hot paths.
        /// </remarks>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                var node = mLinkedList.First;
                while (index-- > 0)
                {
                    node = node.Next;
                }
                return node.Value;
            }
        }

        #region Pool Management

        /// <summary>
        /// Gets a node from the pool or creates a new one.
        /// </summary>
        private LinkedListNode<T> GetNode(T value)
        {
            var node = mNodePool.Count > 0 ? mNodePool.Pop() : new LinkedListNode<T>(default);
            node.Value = value;
            return node;
        }

        /// <summary>
        /// Removes a node from the list and returns it to the pool.
        /// </summary>
        private void RemoveNode(LinkedListNode<T> node)
        {
            mLinkedList.Remove(node);
            ReturnNode(node);
        }

        /// <summary>
        /// Returns a detached node to the pool.
        /// </summary>
        private void ReturnNode(LinkedListNode<T> node)
        {
            if (node.List != null)
                throw new InvalidOperationException("The node is still attached to a list and cannot be returned to the pool.");
            node.Value = default;
            if (mNodePool.Count < MaxPoolSize)
                mNodePool.Push(node);
        }

        #endregion

        #region Pool Extensions

        /// <summary>
        /// Trims the pool so it does not exceed <see cref="MaxPoolSize"/>.
        /// </summary>
        public void TrimPool()
        {
            while (mNodePool.Count > MaxPoolSize)
            {
                mNodePool.Pop();
            }
        }

        /// <summary>
        /// Clears the detached-node pool.
        /// </summary>
        public void ClearPool()
        {
            mNodePool.Clear();
        }

        #endregion

        #region Utility Extensions

        /// <summary>
        /// Adds all items from the supplied collection to the end of the list.
        /// </summary>
        public void AddRange(IEnumerable<T> collection)
        {
            if (collection is null) throw new ArgumentNullException(nameof(collection));
            foreach (var item in collection)
            {
                AddLast(item);
            }
        }

        /// <summary>
        /// Removes every item that matches the supplied predicate.
        /// </summary>
        public int RemoveAll(Predicate<T> match)
        {
            if (match is null) throw new ArgumentNullException(nameof(match));
            int removedCount = 0;
            for (var node = mLinkedList.First; node != null;)
            {
                var next = node.Next;
                if (match(node.Value))
                {
                    RemoveNode(node);
                    removedCount++;
                }
                node = next;
            }
            return removedCount;
        }

        /// <summary>
        /// Copies all items into a new array.
        /// </summary>
        public T[] ToArray()
        {
            T[] array = new T[Count];
            int i = 0;
            foreach (var item in mLinkedList)
            {
                array[i++] = item;
            }
            return array;
        }

        #endregion
    }
}
