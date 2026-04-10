using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Basic bindable value implementation backed by <see cref="EasyEvent{T}"/>.
    /// </summary>
    public class BindValue<T> : IBindable<T>
    {
        protected T mValue;

        private readonly EasyEvent<T> mOnValueChanged = new();
        private static Func<T, T, bool> mCompareFunc = EqualityComparer<T>.Default.Equals;

        /// <summary>
        /// Current value. Setting a different value triggers bound callbacks.
        /// </summary>
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

        /// <summary>
        /// Creates a bindable value with an optional initial value.
        /// </summary>
        public BindValue(T value = default) => mValue = value;

        /// <summary>
        /// Implicitly unwraps the current value.
        /// </summary>
        public static implicit operator T(BindValue<T> bindValue) => bindValue.Value;

        /// <summary>
        /// Registers a value-changed callback.
        /// </summary>
        public LinkUnRegister<T> Bind(Action<T> callback)
        {
            if (callback is not null)
            {
                return mOnValueChanged.Register(callback);
            }

            throw new ArgumentNullException(nameof(callback));
        }

        /// <summary>
        /// Unregisters one value-changed callback.
        /// </summary>
        public void UnBind(Action<T> callback)
        {
            if (callback is not null) mOnValueChanged.UnRegister(callback);
        }

        /// <summary>
        /// Unregisters all value-changed callbacks.
        /// </summary>
        public void UnBindAll() => mOnValueChanged.UnRegisterAll();

        /// <summary>
        /// Updates the stored value without notifying listeners.
        /// </summary>
        public void SetValueWithoutEvent(T value) => mValue = value;

        /// <summary>
        /// Overrides the value comparison function used to detect changes.
        /// </summary>
        public static void SetCompareFunc(Func<T, T, bool> func) => mCompareFunc = func;

        /// <summary>
        /// Returns the current value as a string.
        /// </summary>
        public override string ToString() => mValue.ToString();
    }
}
