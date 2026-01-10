#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 可绑定属性 - 用于包装非响应式数据源
    /// 适用于需要将外部数据源（如 SerializedProperty、配置文件等）转换为响应式数据的场景
    /// </summary>
    /// <typeparam name="T">属性值类型</typeparam>
    public sealed class BindableProperty<T> : IDisposable
    {
        private readonly Func<T> mGetter;
        private readonly Action<T> mSetter;
        private readonly IEqualityComparer<T> mComparer;
        private readonly List<Action<T>> mListeners;
        private T mCachedValue;
        private bool mIsDisposed;

        /// <summary>
        /// 创建可绑定属性
        /// </summary>
        /// <param name="getter">值获取器</param>
        /// <param name="setter">值设置器（可选，为 null 时属性只读）</param>
        /// <param name="comparer">值比较器</param>
        public BindableProperty(Func<T> getter, Action<T> setter = null, IEqualityComparer<T> comparer = null)
        {
            mGetter = getter ?? throw new ArgumentNullException(nameof(getter));
            mSetter = setter;
            mComparer = comparer ?? EqualityComparer<T>.Default;
            mListeners = new List<Action<T>>(4);
            mCachedValue = getter();
        }

        /// <summary>
        /// 当前值
        /// 获取时返回缓存值，设置时调用 setter 并通知订阅者
        /// </summary>
        public T Value
        {
            get => mCachedValue;
            set
            {
                if (mIsDisposed) return;
                if (mSetter == null)
                {
                    UnityEngine.Debug.LogWarning("[BindableProperty] 属性为只读，无法设置值");
                    return;
                }
                
                if (mComparer.Equals(mCachedValue, value)) return;
                
                mSetter(value);
                mCachedValue = value;
                NotifyListeners(value);
            }
        }

        /// <summary>
        /// 是否为只读属性
        /// </summary>
        public bool IsReadOnly => mSetter == null;

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsDisposed => mIsDisposed;

        /// <summary>
        /// 订阅值变化事件
        /// </summary>
        /// <param name="onChanged">变化回调，参数为新值</param>
        /// <returns>用于取消订阅的 IDisposable</returns>
        public IDisposable Subscribe(Action<T> onChanged)
        {
            if (mIsDisposed || onChanged == null) return Disposable.Empty;
            
            mListeners.Add(onChanged);
            return Disposable.Create(() => mListeners.Remove(onChanged));
        }

        /// <summary>
        /// 订阅值变化事件并立即触发一次回调
        /// </summary>
        /// <param name="onChanged">变化回调，参数为新值</param>
        /// <returns>用于取消订阅的 IDisposable</returns>
        public IDisposable SubscribeWithInitialValue(Action<T> onChanged)
        {
            if (mIsDisposed || onChanged == null) return Disposable.Empty;
            
            onChanged(mCachedValue);
            return Subscribe(onChanged);
        }

        /// <summary>
        /// 手动刷新 - 从数据源重新获取值
        /// 如果值发生变化，会通知所有订阅者
        /// </summary>
        public void Refresh()
        {
            if (mIsDisposed) return;
            
            var newValue = mGetter();
            if (mComparer.Equals(mCachedValue, newValue)) return;
            
            mCachedValue = newValue;
            NotifyListeners(newValue);
        }

        /// <summary>
        /// 强制刷新 - 从数据源重新获取值并通知（即使值相同）
        /// </summary>
        public void ForceRefresh()
        {
            if (mIsDisposed) return;
            
            mCachedValue = mGetter();
            NotifyListeners(mCachedValue);
        }

        private void NotifyListeners(T value)
        {
            // 倒序遍历，允许在回调中取消订阅
            for (int i = mListeners.Count - 1; i >= 0; i--)
            {
                try
                {
                    mListeners[i]?.Invoke(value);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[BindableProperty] 订阅者回调异常: {ex}");
                }
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
        public static implicit operator T(BindableProperty<T> property) => property.Value;

        public override string ToString() => mCachedValue?.ToString() ?? "null";
    }

    /// <summary>
    /// BindableProperty 工厂方法
    /// </summary>
    public static class BindableProperty
    {
        /// <summary>
        /// 从 getter/setter 创建可绑定属性
        /// </summary>
        public static BindableProperty<T> Create<T>(Func<T> getter, Action<T> setter = null)
        {
            return new BindableProperty<T>(getter, setter);
        }

        /// <summary>
        /// 从字段创建可绑定属性（通过引用）
        /// </summary>
        /// <example>
        /// int myField = 10;
        /// var prop = BindableProperty.FromField(() => ref myField);
        /// </example>
        public static BindableProperty<T> FromField<T>(RefGetter<T> refGetter)
        {
            return new BindableProperty<T>(
                () => refGetter(),
                value => refGetter() = value
            );
        }

        /// <summary>
        /// 引用获取器委托
        /// </summary>
        public delegate ref T RefGetter<T>();
    }
}
#endif
