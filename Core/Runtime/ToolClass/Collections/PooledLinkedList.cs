using System;
using System.Collections;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 双向链表，通过内部池复用已脱离链表的节点。
    /// </summary>
    public class PooledLinkedList<T> : IEnumerable<T>
    {
        private readonly LinkedList<T> mLinkedList = new();
        private readonly Stack<LinkedListNode<T>> mNodePool;
        private const int DEFAULT_POOL_CAPACITY = 64;
        private int mMaxPoolSize;

        /// <summary>当前链表中的元素数量。</summary>
        public int Count => mLinkedList.Count;

        /// <summary>当前池中缓存的节点数量。</summary>
        public int PoolSize => mNodePool.Count;

        /// <summary>链表首节点。</summary>
        public LinkedListNode<T> First => mLinkedList.First;

        /// <summary>链表尾节点。</summary>
        public LinkedListNode<T> Last => mLinkedList.Last;

        /// <summary>池中最多保留的脱离节点数量。</summary>
        public int MaxPoolSize
        {
            get => mMaxPoolSize;
            set => mMaxPoolSize = value >= 0 ? value : DEFAULT_POOL_CAPACITY;
        }

        public PooledLinkedList(int initialPoolCapacity = DEFAULT_POOL_CAPACITY)
        {
            mMaxPoolSize = initialPoolCapacity;
            mNodePool = new Stack<LinkedListNode<T>>(initialPoolCapacity);
        }

        public void Prewarm(int count)
        {
            int toCreate = Math.Min(count, mMaxPoolSize) - mNodePool.Count;
            for (int i = 0; i < toCreate; i++)
                mNodePool.Push(new LinkedListNode<T>(default));
        }

        public LinkedListNode<T> AddLast(T value)
        {
            var node = GetNode(value);
            mLinkedList.AddLast(node);
            return node;
        }

        public LinkedListNode<T> AddFirst(T value)
        {
            var node = GetNode(value);
            mLinkedList.AddFirst(node);
            return node;
        }

        public LinkedListNode<T> InsertAfter(LinkedListNode<T> node, T value)
        {
            if (node is null) throw new ArgumentNullException(nameof(node));
            if (node.List != mLinkedList)
                throw new InvalidOperationException("The specified node does not belong to this list.");
            var newNode = GetNode(value);
            mLinkedList.AddAfter(node, newNode);
            return newNode;
        }

        public LinkedListNode<T> InsertBefore(LinkedListNode<T> node, T value)
        {
            if (node is null) throw new ArgumentNullException(nameof(node));
            if (node.List != mLinkedList)
                throw new InvalidOperationException("The specified node does not belong to this list.");
            var newNode = GetNode(value);
            mLinkedList.AddBefore(node, newNode);
            return newNode;
        }

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

        public void Remove(LinkedListNode<T> node)
        {
            if (node is null) throw new ArgumentNullException(nameof(node));
            if (node.List != mLinkedList) return;
            RemoveNode(node);
        }

        public void RemoveFirst()
        {
            if (mLinkedList.First != null)
                RemoveNode(mLinkedList.First);
        }

        public void RemoveLast()
        {
            if (mLinkedList.Last != null)
                RemoveNode(mLinkedList.Last);
        }

        public void Clear()
        {
            while (mLinkedList.First != null)
                RemoveFirst();
        }

        public bool Contains(T value) => mLinkedList.Contains(value);
        public LinkedListNode<T> Find(T value) => mLinkedList.Find(value);

        public IEnumerable<T> Reverse()
        {
            var node = mLinkedList.Last;
            while (node != null)
            {
                yield return node.Value;
                node = node.Previous;
            }
        }

        public IEnumerator<T> GetEnumerator() => mLinkedList.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                var node = mLinkedList.First;
                while (index-- > 0)
                    node = node.Next;
                return node.Value;
            }
        }

        private LinkedListNode<T> GetNode(T value)
        {
            var node = mNodePool.Count > 0 ? mNodePool.Pop() : new LinkedListNode<T>(default);
            node.Value = value;
            return node;
        }

        private void RemoveNode(LinkedListNode<T> node)
        {
            mLinkedList.Remove(node);
            ReturnNode(node);
        }

        private void ReturnNode(LinkedListNode<T> node)
        {
            if (node.List != null)
                throw new InvalidOperationException("The node is still attached to a list and cannot be returned to the pool.");
            node.Value = default;
            if (mNodePool.Count < MaxPoolSize)
                mNodePool.Push(node);
        }

        public void TrimPool()
        {
            while (mNodePool.Count > MaxPoolSize)
                mNodePool.Pop();
        }

        public void ClearPool() => mNodePool.Clear();

        public void AddRange(IEnumerable<T> collection)
        {
            if (collection is null) throw new ArgumentNullException(nameof(collection));
            foreach (var item in collection)
                AddLast(item);
        }

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

        public T[] ToArray()
        {
            T[] array = new T[Count];
            int i = 0;
            foreach (var item in mLinkedList)
                array[i++] = item;
            return array;
        }
    }
}
