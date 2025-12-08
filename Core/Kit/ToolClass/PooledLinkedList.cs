using System;
using System.Collections;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 池化节点双向链表
    /// </summary>
    public class PooledLinkedList<T> : IEnumerable<T>
    {
        private readonly LinkedList<T> mLinkedList = new();
        private readonly static Stack<LinkedListNode<T>> nodePool = new();
        private const int DefaultPoolCapacity = 25;
        private int maxPoolSize = DefaultPoolCapacity;

        public int Count => mLinkedList.Count;
        public int PoolSize => nodePool.Count;
        public LinkedListNode<T> First => mLinkedList.First;
        public LinkedListNode<T> Last => mLinkedList.Last;

        public int MaxPoolSize
        {
            get => maxPoolSize;
            set => maxPoolSize = value >= 0 ? value : DefaultPoolCapacity;
        }

        // 添加到链表尾部
        public LinkedListNode<T> AddLast(T value)
        {
            var node = GetNode(value);
            mLinkedList.AddLast(node);
            return node;
        }

        // 添加到链表头部
        public LinkedListNode<T> AddFirst(T value)
        {
            var node = GetNode(value);
            mLinkedList.AddFirst(node);
            return node;
        }

        // 在指定节点后插入
        public LinkedListNode<T> InsertAfter(LinkedListNode<T> node, T value)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (node.List != mLinkedList)
                throw new InvalidOperationException("指定的节点不属于此链表");

            var newNode = GetNode(value);
            mLinkedList.AddAfter(node, newNode);
            return newNode;
        }

        // 在指定节点前插入
        public LinkedListNode<T> InsertBefore(LinkedListNode<T> node, T value)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (node.List != mLinkedList)
                throw new InvalidOperationException("指定的节点不属于此链表");

            var newNode = GetNode(value);
            mLinkedList.AddBefore(node, newNode);
            return newNode;
        }

        // 按值移除第一个匹配的节点
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

        // 移除指定节点
        public void Remove(LinkedListNode<T> node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (node.List != mLinkedList) return;
            RemoveNode(node);
        }

        // 移除首节点
        public void RemoveFirst()
        {
            if (mLinkedList.First != null)
            {
                RemoveNode(mLinkedList.First);
            }
        }

        // 移除尾节点
        public void RemoveLast()
        {
            if (mLinkedList.Last != null)
            {
                RemoveNode(mLinkedList.Last);
            }
        }

        // 清空链表：将链表节点逐个回收到节点池中
        public void Clear()
        {
            while (mLinkedList.First != null)
            {
                RemoveFirst();
            }
        }

        public bool Contains(T value) => mLinkedList.Contains(value);

        public LinkedListNode<T> Find(T value) => mLinkedList.Find(value);

        // 提供反向枚举器的支持
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

        // 索引器，虽然性能不佳，但适用于少量数据
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

        #region 内部节点池管理

        /// <summary>
        /// 当需要一个节点时，先尝试从池中获取。
        /// </summary>
        private LinkedListNode<T> GetNode(T value)
        {
            var node = nodePool.Count > 0 ? nodePool.Pop() : new LinkedListNode<T>(default);
            node.Value = value;
            return node;
        }

        /// <summary>
        /// 节点从链表上移除后回收到节点池中。
        /// </summary>
        private void RemoveNode(LinkedListNode<T> node)
        {
            mLinkedList.Remove(node);
            ReturnNode(node);
        }

        /// <summary>
        /// 回收节点：如果节点仍挂在某个链表上，则抛出异常。
        /// </summary>
        private void ReturnNode(LinkedListNode<T> node)
        {
            if (node.List != null)
                throw new InvalidOperationException("节点仍挂在链表上，无法回收");
            node.Value = default;
            if (nodePool.Count < MaxPoolSize)
                nodePool.Push(node);
        }

        #endregion

        #region 池管理扩展

        /// <summary>
        /// 清空节点池中的多余节点（例如当 MaxPoolSize 调低后调用）
        /// </summary>
        public void TrimPool()
        {
            while (nodePool.Count > MaxPoolSize)
            {
                nodePool.Pop();
            }
        }

        /// <summary>
        /// 清空整个节点池（释放所有已缓存节点）
        /// </summary>
        public void ClearPool()
        {
            nodePool.Clear();
        }

        #endregion

        #region 其他功能扩展

        /// <summary>
        /// 批量添加元素到链表尾部
        /// </summary>
        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null) throw new ArgumentNullException(nameof(collection));
            foreach (var item in collection)
            {
                AddLast(item);
            }
        }

        /// <summary>
        /// 根据谓词移除所有匹配的节点，返回移除的节点数量
        /// </summary>
        public int RemoveAll(Predicate<T> match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
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
        /// 将链表中的元素复制到数组中返回
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