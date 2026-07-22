using System;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 绑定池化链表节点 owner 和 generation 的值类型租约，过期副本不能访问复用后的新节点。
    /// </summary>
    /// <typeparam name="T">节点值类型。</typeparam>
    public readonly struct PooledLinkedListNode<T> : IEquatable<PooledLinkedListNode<T>>
    {
        private readonly PooledLinkedList<T> mOwner;
        private readonly PooledLinkedList<T>.Node mNode;
        private readonly int mGeneration;

        /// <summary>
        /// 创建绑定底层节点当前代次的租约。
        /// </summary>
        /// <param name="owner">拥有底层节点的池化链表。</param>
        /// <param name="node">当前活动底层节点。</param>
        /// <param name="generation">节点当前租约代次。</param>
        internal PooledLinkedListNode(
            PooledLinkedList<T> owner,
            PooledLinkedList<T>.Node node,
            int generation)
        {
            mOwner = owner;
            mNode = node;
            mGeneration = generation;
        }

        /// <summary>
        /// 获取当前租约是否仍指向所属链表中的同一节点代次。
        /// </summary>
        public bool IsValid
        {
            get { return mOwner != null && mOwner.IsNodeLeaseValid(this); }
        }

        /// <summary>
        /// 获取或设置当前节点值；租约失效后访问会抛出 InvalidOperationException。
        /// </summary>
        public T Value
        {
            get
            {
                RequireValidLease();
                return mNode.Value;
            }
            set
            {
                RequireValidLease();
                mNode.Value = value;
            }
        }

        /// <summary>
        /// 获取前一活动节点的当前租约；不存在时返回无效租约。
        /// </summary>
        public PooledLinkedListNode<T> Previous
        {
            get
            {
                RequireValidLease();
                return mOwner.CreateLease(mNode.Previous);
            }
        }

        /// <summary>
        /// 获取后一活动节点的当前租约；不存在时返回无效租约。
        /// </summary>
        public PooledLinkedListNode<T> Next
        {
            get
            {
                RequireValidLease();
                return mOwner.CreateLease(mNode.Next);
            }
        }

        /// <summary>
        /// 获取租约所属链表，供链表内部执行常量时间校验。
        /// </summary>
        internal PooledLinkedList<T> Owner
        {
            get { return mOwner; }
        }

        /// <summary>
        /// 获取租约底层节点，供所属链表执行受控读写。
        /// </summary>
        internal PooledLinkedList<T>.Node StorageNode
        {
            get { return mNode; }
        }

        /// <summary>
        /// 获取租约保存的节点代次。
        /// </summary>
        internal int Generation
        {
            get { return mGeneration; }
        }

        /// <summary>
        /// 判断两个租约是否引用同一 owner、节点和代次。
        /// </summary>
        /// <param name="other">需要比较的节点租约。</param>
        /// <returns>两个租约完全相同时返回 true。</returns>
        public bool Equals(PooledLinkedListNode<T> other)
        {
            return ReferenceEquals(mOwner, other.mOwner)
                && ReferenceEquals(mNode, other.mNode)
                && mGeneration == other.mGeneration;
        }

        /// <summary>
        /// 判断对象是否为同类型且引用同一节点租约。
        /// </summary>
        /// <param name="obj">需要比较的对象。</param>
        /// <returns>对象表示相同租约时返回 true。</returns>
        public override bool Equals(object obj)
        {
            return obj is PooledLinkedListNode<T> other && Equals(other);
        }

        /// <summary>
        /// 返回由 owner、底层节点和 generation 组合的租约哈希。
        /// </summary>
        /// <returns>当前租约哈希值。</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int ownerHash = mOwner == null ? 0 : RuntimeHelpers.GetHashCode(mOwner);
                int nodeHash = mNode == null ? 0 : RuntimeHelpers.GetHashCode(mNode);
                return ((ownerHash * 397) ^ nodeHash) * 397 ^ mGeneration;
            }
        }

        /// <summary>
        /// 判断两个节点租约是否完全相同。
        /// </summary>
        /// <param name="left">左侧租约。</param>
        /// <param name="right">右侧租约。</param>
        /// <returns>租约相同时返回 true。</returns>
        public static bool operator ==(PooledLinkedListNode<T> left, PooledLinkedListNode<T> right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个节点租约是否不同。
        /// </summary>
        /// <param name="left">左侧租约。</param>
        /// <param name="right">右侧租约。</param>
        /// <returns>租约不同时返回 true。</returns>
        public static bool operator !=(PooledLinkedListNode<T> left, PooledLinkedListNode<T> right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// 确保租约仍有效，防止过期副本读写已经复用的节点。
        /// </summary>
        private void RequireValidLease()
        {
            if (!IsValid)
            {
                throw new InvalidOperationException("The pooled linked-list node lease is no longer valid.");
            }
        }
    }
}
