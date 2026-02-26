using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public class BindValue<T> : IBindable<T>
    {
        protected T mValue;
        public virtual T Value
        {
            get => mValue;
            set
            {
                if (mCompareFunc(mValue, value)) return;
                mValue = value;
                mOnValueChanged?.Trigger(mValue);
            }
        }

        private readonly EasyEvent<T> mOnValueChanged = new();
        // 使用 EqualityComparer<T>.Default 避免值类型装箱
        private static Func<T, T, bool> mCompareFunc = EqualityComparer<T>.Default.Equals;

        public BindValue(T value = default) => mValue = value;
        public static implicit operator T(BindValue<T> bindValue) => bindValue.Value;

        public LinkUnRegister<T> Bind(Action<T> callback)
        {
            if (callback is not null)
            {
                return mOnValueChanged.Register(callback);
            }
            throw new ArgumentNullException(nameof(callback));
        }

        public void UnBind(Action<T> callback)
        {
            if (callback is not null) mOnValueChanged.UnRegister(callback);
        }

        public void UnBindAll() => mOnValueChanged.UnRegisterAll();
        public void SetValueWithoutEvent(T value) => mValue = value;
        public static void SetCompareFunc(Func<T, T, bool> func) => mCompareFunc = func;
        public override string ToString() => mValue.ToString();
    }
}