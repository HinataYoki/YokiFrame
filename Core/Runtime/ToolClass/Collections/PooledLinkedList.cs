using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 使用侵入式双向节点和空闲链复用节点对象的代次安全链表。
    /// </summary>
    /// <typeparam name="T">链表元素类型。</typeparam>
    public partial class PooledLinkedList<T> : IEnumerable<T>
    {
        private const int DEFAULT_POOL_CAPACITY = 64;

        private static readonly EqualityComparer<T> sComparer = EqualityComparer<T>.Default;

        private Node mFirst;
        private Node mLast;
        private Node mPoolHead;
        private int mCount;
        private int mPoolCount;
        private int mMaxPoolSize;
        private int mVersion;

        /// <summary>
        /// 创建最多保留指定空闲节点数量的池化链表；构造时不分配节点或池数组。
        /// </summary>
        /// <param name="maxPoolSize">节点池最多保留的已移除节点数量。</param>
        public PooledLinkedList(int maxPoolSize = DEFAULT_POOL_CAPACITY)
        {
            if (maxPoolSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPoolSize));
            }

            mMaxPoolSize = maxPoolSize;
        }

        /// <summary>
        /// 获取链表中当前保存的元素数量。
        /// </summary>
        public int Count
        {
            get { return mCount; }
        }

        /// <summary>
        /// 获取节点池中当前可复用节点数量。
        /// </summary>
        public int PoolSize
        {
            get { return mPoolCount; }
        }

        /// <summary>
        /// 获取链表首节点租约；链表为空时返回无效租约。
        /// </summary>
        public PooledLinkedListNode<T> First
        {
            get { return CreateLease(mFirst); }
        }

        /// <summary>
        /// 获取链表尾节点租约；链表为空时返回无效租约。
        /// </summary>
        public PooledLinkedListNode<T> Last
        {
            get { return CreateLease(mLast); }
        }

        /// <summary>
        /// 获取或设置节点池最多保留的已移除节点数量；缩小时立即裁剪。
        /// </summary>
        public int MaxPoolSize
        {
            get { return mMaxPoolSize; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                mMaxPoolSize = value;
                TrimPool();
            }
        }

        /// <summary>
        /// 获取指定索引的值；从距离更近的一端遍历，复杂度仍为 O(n)。
        /// </summary>
        /// <param name="index">从零开始的链表索引。</param>
        /// <returns>指定位置的值。</returns>
        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= mCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return index <= mCount / 2
                    ? GetNodeFromFirst(index).Value
                    : GetNodeFromLast(index).Value;
            }
        }

        /// <summary>
        /// 把值添加到链表末尾，并优先租用空闲链中的节点。
        /// </summary>
        /// <param name="value">需要添加的值。</param>
        /// <returns>绑定当前节点代次的租约。</returns>
        public PooledLinkedListNode<T> AddLast(T value)
        {
            Node node = RentNode(value);
            node.Previous = mLast;
            if (mLast == null)
            {
                mFirst = node;
            }
            else
            {
                mLast.Next = node;
            }

            mLast = node;
            mCount++;
            mVersion++;
            return CreateLease(node);
        }

        /// <summary>
        /// 把值添加到链表开头，并优先租用空闲链中的节点。
        /// </summary>
        /// <param name="value">需要添加的值。</param>
        /// <returns>绑定当前节点代次的租约。</returns>
        public PooledLinkedListNode<T> AddFirst(T value)
        {
            Node node = RentNode(value);
            node.Next = mFirst;
            if (mFirst == null)
            {
                mLast = node;
            }
            else
            {
                mFirst.Previous = node;
            }

            mFirst = node;
            mCount++;
            mVersion++;
            return CreateLease(node);
        }

        /// <summary>
        /// 在有效基准租约后插入新值。
        /// </summary>
        /// <param name="node">当前链表中的基准节点租约。</param>
        /// <param name="value">需要插入的值。</param>
        /// <returns>新值对应的节点租约。</returns>
        public PooledLinkedListNode<T> InsertAfter(PooledLinkedListNode<T> node, T value)
        {
            Node anchor = RequireValidNode(node);
            Node newNode = RentNode(value);
            newNode.Previous = anchor;
            newNode.Next = anchor.Next;
            if (anchor.Next == null)
            {
                mLast = newNode;
            }
            else
            {
                anchor.Next.Previous = newNode;
            }

            anchor.Next = newNode;
            mCount++;
            mVersion++;
            return CreateLease(newNode);
        }

        /// <summary>
        /// 在有效基准租约前插入新值。
        /// </summary>
        /// <param name="node">当前链表中的基准节点租约。</param>
        /// <param name="value">需要插入的值。</param>
        /// <returns>新值对应的节点租约。</returns>
        public PooledLinkedListNode<T> InsertBefore(PooledLinkedListNode<T> node, T value)
        {
            Node anchor = RequireValidNode(node);
            Node newNode = RentNode(value);
            newNode.Previous = anchor.Previous;
            newNode.Next = anchor;
            if (anchor.Previous == null)
            {
                mFirst = newNode;
            }
            else
            {
                anchor.Previous.Next = newNode;
            }

            anchor.Previous = newNode;
            mCount++;
            mVersion++;
            return CreateLease(newNode);
        }

        /// <summary>
        /// 移除第一个与指定值相等的节点并把节点归还空闲链。
        /// </summary>
        /// <param name="value">需要移除的值。</param>
        /// <returns>实际移除节点时返回 true。</returns>
        public bool Remove(T value)
        {
            Node node = mFirst;
            while (node != null)
            {
                if (sComparer.Equals(node.Value, value))
                {
                    RemoveNode(node);
                    return true;
                }

                node = node.Next;
            }

            return false;
        }

        /// <summary>
        /// 仅在节点租约仍属于当前链表且代次有效时移除节点。
        /// </summary>
        /// <param name="node">需要移除的节点租约。</param>
        /// <returns>实际移除当前租约时返回 true。</returns>
        public bool Remove(PooledLinkedListNode<T> node)
        {
            if (!IsNodeLeaseValid(node))
            {
                return false;
            }

            RemoveNode(node.StorageNode);
            return true;
        }

        /// <summary>
        /// 移除链表首节点并归还空闲链；链表为空时不执行操作。
        /// </summary>
        public void RemoveFirst()
        {
            if (mFirst != null)
            {
                RemoveNode(mFirst);
            }
        }

        /// <summary>
        /// 移除链表尾节点并归还空闲链；链表为空时不执行操作。
        /// </summary>
        public void RemoveLast()
        {
            if (mLast != null)
            {
                RemoveNode(mLast);
            }
        }

        /// <summary>
        /// 清空活动链表、失效全部现有租约，并按池上限回收节点。
        /// </summary>
        public void Clear()
        {
            if (mCount == 0)
            {
                return;
            }

            Node node = mFirst;
            mFirst = null;
            mLast = null;
            mCount = 0;
            mVersion++;
            while (node != null)
            {
                Node next = node.Next;
                ReturnNode(node);
                node = next;
            }
        }

        /// <summary>
        /// 判断节点租约仍由当前链表持有且代次未发生变化。
        /// </summary>
        /// <param name="lease">需要验证的节点租约。</param>
        /// <returns>租约仍有效时返回 true。</returns>
        internal bool IsNodeLeaseValid(PooledLinkedListNode<T> lease)
        {
            Node node = lease.StorageNode;
            return ReferenceEquals(lease.Owner, this)
                && node != null
                && ReferenceEquals(node.Owner, this)
                && node.IsAttached
                && node.Generation == lease.Generation;
        }

        /// <summary>
        /// 为活动节点创建包含当前代次的值类型租约。
        /// </summary>
        /// <param name="node">活动节点；为空时返回默认无效租约。</param>
        /// <returns>节点当前租约。</returns>
        internal PooledLinkedListNode<T> CreateLease(Node node)
        {
            return node == null
                ? default
                : new PooledLinkedListNode<T>(this, node, node.Generation);
        }

        /// <summary>
        /// 校验租约并返回底层活动节点。
        /// </summary>
        /// <param name="lease">需要解析的节点租约。</param>
        /// <returns>租约对应的底层节点。</returns>
        private Node RequireValidNode(PooledLinkedListNode<T> lease)
        {
            if (!IsNodeLeaseValid(lease))
            {
                throw new InvalidOperationException("The pooled linked-list node lease is no longer valid.");
            }

            return lease.StorageNode;
        }

        /// <summary>
        /// 从空闲链租出节点或创建节点，并推进代次区分历史租约。
        /// </summary>
        /// <param name="value">节点当前保存的值。</param>
        /// <returns>尚未连接到双向链的活动节点。</returns>
        private Node RentNode(T value)
        {
            Node node;
            if (mPoolHead == null)
            {
                node = new Node(this);
            }
            else
            {
                node = mPoolHead;
                mPoolHead = node.PoolNext;
                node.PoolNext = null;
                mPoolCount--;
            }

            node.Generation = NextGeneration(node.Generation);
            node.IsAttached = true;
            node.Value = value;
            return node;
        }

        /// <summary>
        /// 从双向链解除活动节点并进入统一回收路径。
        /// </summary>
        /// <param name="node">当前链表中的活动节点。</param>
        private void RemoveNode(Node node)
        {
            if (node.Previous == null)
            {
                mFirst = node.Next;
            }
            else
            {
                node.Previous.Next = node.Next;
            }

            if (node.Next == null)
            {
                mLast = node.Previous;
            }
            else
            {
                node.Next.Previous = node.Previous;
            }

            mCount--;
            mVersion++;
            ReturnNode(node);
        }

        /// <summary>
        /// 清空节点数据并按池上限把节点压入无额外数组的空闲链。
        /// </summary>
        /// <param name="node">已经脱离双向链的节点。</param>
        private void ReturnNode(Node node)
        {
            node.Value = default;
            node.Previous = null;
            node.Next = null;
            node.IsAttached = false;
            if (mPoolCount >= mMaxPoolSize)
            {
                node.PoolNext = null;
                return;
            }

            node.PoolNext = mPoolHead;
            mPoolHead = node;
            mPoolCount++;
        }

        /// <summary>
        /// 从首节点向后移动指定距离。
        /// </summary>
        /// <param name="index">从首节点开始的零基偏移。</param>
        /// <returns>目标活动节点。</returns>
        private Node GetNodeFromFirst(int index)
        {
            Node node = mFirst;
            while (index > 0)
            {
                node = node.Next;
                index--;
            }

            return node;
        }

        /// <summary>
        /// 从尾节点向前移动到指定零基索引。
        /// </summary>
        /// <param name="index">从首节点计算的目标索引。</param>
        /// <returns>目标活动节点。</returns>
        private Node GetNodeFromLast(int index)
        {
            Node node = mLast;
            int remaining = mCount - index - 1;
            while (remaining > 0)
            {
                node = node.Previous;
                remaining--;
            }

            return node;
        }

        /// <summary>
        /// 校验枚举期间链表没有发生修改。
        /// </summary>
        /// <param name="version">枚举开始时保存的版本。</param>
        private void EnsureVersion(int version)
        {
            if (version != mVersion)
            {
                throw new InvalidOperationException("The pooled linked list was modified during enumeration.");
            }
        }

        /// <summary>
        /// 推进节点租约代次；整数完整回绕时跳过默认零值。
        /// </summary>
        /// <param name="generation">节点上一租约代次。</param>
        /// <returns>节点下一租约代次。</returns>
        private static int NextGeneration(int generation)
        {
            generation = unchecked(generation + 1);
            return generation == 0 ? 1 : generation;
        }

        /// <summary>
        /// 保存侵入式双向链、空闲链和租约代次，不向调用方暴露可复用对象引用。
        /// </summary>
        internal sealed class Node
        {
            internal readonly PooledLinkedList<T> Owner;
            internal Node Previous;
            internal Node Next;
            internal Node PoolNext;
            internal T Value;
            internal int Generation;
            internal bool IsAttached;

            /// <summary>
            /// 创建永久归属于指定池化链表的底层节点。
            /// </summary>
            /// <param name="owner">拥有节点和空闲链的池化链表。</param>
            internal Node(PooledLinkedList<T> owner)
            {
                Owner = owner;
            }
        }
    }
}
