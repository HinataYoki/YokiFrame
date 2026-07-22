using System;
using System.Collections;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 提供 PooledLinkedList 的预热、枚举和批量操作。
    /// </summary>
    public partial class PooledLinkedList<T>
    {
        /// <summary>
        /// 预先创建不超过池上限的节点，避免后续首次添加产生节点分配。
        /// </summary>
        /// <param name="count">希望空闲链中至少具备的节点数量。</param>
        public void Prewarm(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            int targetCount = Math.Min(count, mMaxPoolSize);
            while (mPoolCount < targetCount)
            {
                var node = new Node(this)
                {
                    PoolNext = mPoolHead
                };
                mPoolHead = node;
                mPoolCount++;
            }
        }

        /// <summary>
        /// 判断链表是否包含指定值。
        /// </summary>
        /// <param name="value">需要查找的值。</param>
        /// <returns>存在匹配值时返回 true。</returns>
        public bool Contains(T value)
        {
            return Find(value).IsValid;
        }

        /// <summary>
        /// 查找第一个包含指定值的节点租约。
        /// </summary>
        /// <param name="value">需要查找的值。</param>
        /// <returns>第一个匹配租约；不存在时返回无效租约。</returns>
        public PooledLinkedListNode<T> Find(T value)
        {
            Node node = mFirst;
            while (node != null)
            {
                if (sComparer.Equals(node.Value, value))
                {
                    return CreateLease(node);
                }

                node = node.Next;
            }

            return default;
        }

        /// <summary>
        /// 从尾到头枚举当前链表值；该便利入口会创建迭代器对象。
        /// </summary>
        /// <returns>反向值序列。</returns>
        public IEnumerable<T> Reverse()
        {
            int version = mVersion;
            Node node = mLast;
            while (node != null)
            {
                EnsureVersion(version);
                T value = node.Value;
                node = node.Previous;
                yield return value;
            }

            EnsureVersion(version);
        }

        /// <summary>
        /// 返回直接 foreach 不产生迭代器对象分配的结构体枚举器。
        /// </summary>
        /// <returns>当前链表枚举器。</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <summary>
        /// 通过泛型 IEnumerable 契约返回枚举器；接口调用会装箱结构体枚举器。
        /// </summary>
        /// <returns>泛型枚举器。</returns>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 通过非泛型 IEnumerable 契约返回枚举器；接口调用会装箱结构体枚举器。
        /// </summary>
        /// <returns>非泛型枚举器。</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 把空闲链裁剪到 MaxPoolSize，不影响仍在双向链中的活动节点。
        /// </summary>
        public void TrimPool()
        {
            while (mPoolCount > mMaxPoolSize)
            {
                Node node = mPoolHead;
                mPoolHead = node.PoolNext;
                node.PoolNext = null;
                mPoolCount--;
            }
        }

        /// <summary>
        /// 释放全部空闲节点引用；不会修改仍在链表中的活动节点。
        /// </summary>
        public void ClearPool()
        {
            while (mPoolHead != null)
            {
                Node node = mPoolHead;
                mPoolHead = node.PoolNext;
                node.PoolNext = null;
            }

            mPoolCount = 0;
        }

        /// <summary>
        /// 按输入枚举顺序把全部值追加到链表末尾。
        /// </summary>
        /// <param name="collection">需要追加的值集合；不能是当前链表自身。</param>
        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            if (ReferenceEquals(collection, this))
            {
                throw new InvalidOperationException("A pooled linked list cannot add itself as a range.");
            }

            foreach (T item in collection)
            {
                AddLast(item);
            }
        }

        /// <summary>
        /// 移除全部满足条件的节点；匹配器执行期间不得修改当前链表。
        /// </summary>
        /// <param name="match">返回 true 表示需要移除当前值的匹配器。</param>
        /// <returns>实际移除的节点数量。</returns>
        public int RemoveAll(Predicate<T> match)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            int removedCount = 0;
            int version = mVersion;
            Node node = mFirst;
            while (node != null)
            {
                Node next = node.Next;
                bool shouldRemove = match.Invoke(node.Value);
                EnsureVersion(version);
                if (shouldRemove)
                {
                    RemoveNode(node);
                    removedCount++;
                    version = mVersion;
                }

                node = next;
            }

            return removedCount;
        }

        /// <summary>
        /// 按链表顺序复制当前全部值到新数组。
        /// </summary>
        /// <returns>独立值数组。</returns>
        public T[] ToArray()
        {
            T[] values = new T[mCount];
            int index = 0;
            Node node = mFirst;
            while (node != null)
            {
                values[index] = node.Value;
                index++;
                node = node.Next;
            }

            return values;
        }

        /// <summary>
        /// 按双向链顺序枚举值，并在枚举期间检测链表修改。
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly PooledLinkedList<T> mList;
            private readonly int mVersion;
            private Node mNext;
            private T mCurrent;

            /// <summary>
            /// 创建绑定当前链表版本的结构体枚举器。
            /// </summary>
            /// <param name="list">需要枚举的池化链表。</param>
            internal Enumerator(PooledLinkedList<T> list)
            {
                mList = list;
                mVersion = list.mVersion;
                mNext = list.mFirst;
                mCurrent = default;
            }

            /// <summary>
            /// 获取当前枚举值。
            /// </summary>
            public T Current
            {
                get { return mCurrent; }
            }

            /// <summary>
            /// 通过非泛型枚举器契约获取当前值。
            /// </summary>
            object IEnumerator.Current
            {
                get { return mCurrent; }
            }

            /// <summary>
            /// 移动到下一个活动节点；链表被修改时抛出异常。
            /// </summary>
            /// <returns>存在下一个值时返回 true。</returns>
            public bool MoveNext()
            {
                mList.EnsureVersion(mVersion);
                if (mNext == null)
                {
                    mCurrent = default;
                    return false;
                }

                mCurrent = mNext.Value;
                mNext = mNext.Next;
                return true;
            }

            /// <summary>
            /// 把枚举位置重置到链表首节点，同时要求版本保持不变。
            /// </summary>
            void IEnumerator.Reset()
            {
                mList.EnsureVersion(mVersion);
                mNext = mList.mFirst;
                mCurrent = default;
            }

            /// <summary>
            /// 结构体枚举器不持有非托管资源，因此释放时无需执行操作。
            /// </summary>
            public void Dispose()
            {
            }
        }
    }
}
