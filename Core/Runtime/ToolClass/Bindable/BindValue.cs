using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 基于 EasyEvent&lt;T&gt; 的基础可绑定值实现。
    /// </summary>
    public class BindValue<T> : IBindable<T>
    {
        protected T mValue;

        private readonly EasyEvent<T> mOnValueChanged = new();
        private static Func<T, T, bool> sCompareFunc = EqualityComparer<T>.Default.Equals;

        /// <summary>
        /// 当前值。设置为不同值时会触发已绑定回调。
        /// </summary>
        public virtual T Value
        {
            get => mValue;
            set
            {
                if (sCompareFunc(mValue, value)) return;
                mValue = value;
                mOnValueChanged?.Trigger(mValue);
            }
        }

        /// <summary>创建可绑定值，可传入初始值。</summary>
        public BindValue(T value = default) => mValue = value;

        /// <summary>隐式取出当前值。</summary>
        public static implicit operator T(BindValue<T> bindValue) => bindValue.Value;

        /// <summary>注册值变化回调。</summary>
        public LinkUnRegister<T> Bind(Action<T> callback)
        {
            if (callback is not null)
                return mOnValueChanged.Register(callback);
            throw new ArgumentNullException(nameof(callback));
        }

        /// <summary>注销一个值变化回调。</summary>
        public void UnBind(Action<T> callback)
        {
            if (callback is not null) mOnValueChanged.UnRegister(callback);
        }

        /// <summary>注销全部值变化回调。</summary>
        public void UnBindAll() => mOnValueChanged.UnRegisterAll();

        /// <summary>更新存储值，但不通知监听器。</summary>
        public void SetValueWithoutEvent(T value) => mValue = value;

        /// <summary>覆盖用于检测变化的值比较函数。</summary>
        public static void SetCompareFunc(Func<T, T, bool> func) => sCompareFunc = func;

        /// <summary>以字符串形式返回当前值。</summary>
        public override string ToString() => mValue?.ToString() ?? string.Empty;
    }
}
