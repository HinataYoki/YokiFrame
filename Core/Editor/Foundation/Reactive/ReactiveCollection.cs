#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 集合变化类型
    /// </summary>
    public enum CollectionChangeType
    {
        Add,
        Remove,
        Replace,
        Clear,
        Reset
    }

    /// <summary>
    /// 集合变化事件数据
    /// </summary>
    public readonly struct CollectionChangeEvent<T>
    {
        public readonly CollectionChangeType Type;
        public readonly T Item;
        public readonly T OldItem;
        public readonly int Index;

        public CollectionChangeEvent(CollectionChangeType type, T item = default, T oldItem = default, int index = -1)
        {
            Type = type;
            Item = item;
            OldItem = oldItem;
            Index = index;
        }

        public static CollectionChangeEvent<T> Added(T item, int index) 
            => new(CollectionChangeType.Add, item, default, index);
        
        public static CollectionChangeEvent<T> Removed(T item, int index) 
            => new(CollectionChangeType.Remove, item, default, index);
        
        public static CollectionChangeEvent<T> Replaced(T newItem, T oldItem, int index) 
            => new(CollectionChangeType.Replace, newItem, oldItem, index);
        
        public static CollectionChangeEvent<T> Cleared() 
            => new(CollectionChangeType.Clear);
        
        public static CollectionChangeEvent<T> Reset() 
            => new(CollectionChangeType.Reset);
    }

    /// <summary>
    /// 响应式集合 - 集合变化时自动通知订阅者
    /// 用于编辑器工具的列表数据绑定
    /// </summary>
    public sealed class ReactiveCollection<T> : IList<T>, IList, IDisposable
    {
        private readonly List<T> mItems;
        private readonly List<Action<CollectionChangeEvent<T>>> mListeners;
        private bool mIsDisposed;

        public ReactiveCollection(int capacity = 8)
        {
            mItems = new List<T>(capacity);
            mListeners = new List<Action<CollectionChangeEvent<T>>>(4);
        }

        #region IList<T> 实现

        public int Count => mItems.Count;
        public bool IsReadOnly => false;

        public T this[int index]
        {
            get => mItems[index];
            set
            {
                if (mIsDisposed) return;
                var oldItem = mItems[index];
                mItems[index] = value;
                Notify(CollectionChangeEvent<T>.Replaced(value, oldItem, index));
            }
        }

        public void Add(T item)
        {
            if (mIsDisposed) return;
            mItems.Add(item);
            Notify(CollectionChangeEvent<T>.Added(item, mItems.Count - 1));
        }

        public void Insert(int index, T item)
        {
            if (mIsDisposed) return;
            mItems.Insert(index, item);
            Notify(CollectionChangeEvent<T>.Added(item, index));
        }

        public bool Remove(T item)
        {
            if (mIsDisposed) return false;
            int index = mItems.IndexOf(item);
            if (index < 0) return false;
            
            mItems.RemoveAt(index);
            Notify(CollectionChangeEvent<T>.Removed(item, index));
            return true;
        }

        public void RemoveAt(int index)
        {
            if (mIsDisposed) return;
            var item = mItems[index];
            mItems.RemoveAt(index);
            Notify(CollectionChangeEvent<T>.Removed(item, index));
        }

        public void Clear()
        {
            if (mIsDisposed) return;
            mItems.Clear();
            Notify(CollectionChangeEvent<T>.Cleared());
        }

        public bool Contains(T item) => mItems.Contains(item);
        public int IndexOf(T item) => mItems.IndexOf(item);
        public void CopyTo(T[] array, int arrayIndex) => mItems.CopyTo(array, arrayIndex);
        public IEnumerator<T> GetEnumerator() => mItems.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region IList 显式实现（用于 ListView.itemsSource）

        bool IList.IsFixedSize => false;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => ((ICollection)mItems).SyncRoot;

        object IList.this[int index]
        {
            get => mItems[index];
            set
            {
                if (value is T typedValue)
                {
                    this[index] = typedValue;
                }
            }
        }

        int IList.Add(object value)
        {
            if (value is T typedValue)
            {
                Add(typedValue);
                return mItems.Count - 1;
            }
            return -1;
        }

        bool IList.Contains(object value) => value is T typedValue && Contains(typedValue);
        int IList.IndexOf(object value) => value is T typedValue ? IndexOf(typedValue) : -1;

        void IList.Insert(int index, object value)
        {
            if (value is T typedValue)
            {
                Insert(index, typedValue);
            }
        }

        void IList.Remove(object value)
        {
            if (value is T typedValue)
            {
                Remove(typedValue);
            }
        }

        void ICollection.CopyTo(Array array, int index) => ((ICollection)mItems).CopyTo(array, index);

        #endregion

        #region 扩展方法

        /// <summary>
        /// 批量添加（只触发一次 Reset 通知）
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            if (mIsDisposed) return;
            mItems.AddRange(items);
            Notify(CollectionChangeEvent<T>.Reset());
        }

        /// <summary>
        /// 替换所有内容（只触发一次 Reset 通知）
        /// </summary>
        public void ReplaceAll(IEnumerable<T> items)
        {
            if (mIsDisposed) return;
            mItems.Clear();
            mItems.AddRange(items);
            Notify(CollectionChangeEvent<T>.Reset());
        }

        #endregion

        #region 订阅

        /// <summary>
        /// 订阅集合变化事件
        /// </summary>
        public IDisposable Subscribe(Action<CollectionChangeEvent<T>> onChanged)
        {
            if (mIsDisposed || onChanged == null) return Disposable.Empty;
            
            mListeners.Add(onChanged);
            return Disposable.Create(() => mListeners.Remove(onChanged));
        }

        private void Notify(CollectionChangeEvent<T> evt)
        {
            for (int i = mListeners.Count - 1; i >= 0; i--)
            {
                mListeners[i]?.Invoke(evt);
            }
        }

        #endregion

        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;
            mListeners.Clear();
            mItems.Clear();
        }
    }
}
#endif
