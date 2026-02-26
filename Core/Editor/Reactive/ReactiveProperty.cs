#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 响应式属性 - 值变化时自动通知订阅者
    /// 用于编辑器工具的数据绑定，避免轮询刷新
    /// </summary>
    /// <typeparam name="T">属性值类型</typeparam>
    public sealed class ReactiveProperty<T> : IDisposable
    {
        private T mValue;
        private readonly List<Action<T, T>> mListeners;
        private readonly IEqualityComparer<T> mComparer;
        private bool mIsDisposed;

        /// <summary>
        /// 创建响应式属性
        /// </summary>
        /// <param name="initialValue">初始值</param>
        /// <param name="comparer">值比较器，用于判断值是否变化</param>
        public ReactiveProperty(T initialValue = default, IEqualityComparer<T> comparer = null)
        {
            mValue = initialValue;
            mComparer = comparer ?? EqualityComparer<T>.Default;
            mListeners = new(4);
        }

        /// <summary>
        /// 当前值，设置时自动通知订阅者
        /// </summary>
        public T Value
        {
            get => mValue;
            set
            {
                if (mIsDisposed) return;
                if (mComparer.Equals(mValue, value)) return;
                
                var oldValue = mValue;
                mValue = value;
                NotifyListeners(oldValue, value);
            }
        }

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsDisposed => mIsDisposed;

        /// <summary>
        /// 订阅值变化事件（接收新旧值）
        /// </summary>
        /// <param name="onChanged">变化回调，参数为 (oldValue, newValue)</param>
        /// <returns>用于取消订阅的 IDisposable</returns>
        public IDisposable Subscribe(Action<T, T> onChanged)
        {
            if (mIsDisposed || onChanged is null) return Disposable.Empty;
            
            mListeners.Add(onChanged);
            return Disposable.Create(() => mListeners.Remove(onChanged));
        }

        /// <summary>
        /// 订阅值变化事件（仅接收新值）
        /// </summary>
        /// <param name="onChanged">变化回调，参数为新值</param>
        /// <returns>用于取消订阅的 IDisposable</returns>
        public IDisposable Subscribe(Action<T> onChanged)
        {
            if (onChanged is null) return Disposable.Empty;
            return Subscribe((_, newVal) => onChanged(newVal));
        }

        /// <summary>
        /// 强制设置值并通知（即使值相同）
        /// </summary>
        public void SetValueAndForceNotify(T value)
        {
            if (mIsDisposed) return;
            
            var oldValue = mValue;
            mValue = value;
            NotifyListeners(oldValue, value);
        }

        private void NotifyListeners(T oldValue, T newValue)
        {
            // 倒序遍历，允许在回调中取消订阅
            for (int i = mListeners.Count - 1; i >= 0; i--)
            {
                mListeners[i]?.Invoke(oldValue, newValue);
            }
        }

        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;
            mListeners.Clear();
        }

        /// <summary>
        /// 隐式转换为值类型
        /// </summary>
        public static implicit operator T(ReactiveProperty<T> property) => property.Value;

        public override string ToString() => mValue?.ToString() ?? "null";
    }
}
#endif
