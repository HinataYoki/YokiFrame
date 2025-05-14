using System;

namespace YokiFrame
{
    public class BindValue<T>
    {
        public BindValue(T value = default) => mValue = value;
        private T mValue;
        public T Value
        {
            get => mValue;
            set
            {
                if (mCompareFunc(mValue, value)) return;
                var oldValue = mValue;
                mValue = value;
                onValueChanged?.Trigger((oldValue, mValue));
            }
        }
        public void SetValueWithoutEvent(T value) => mValue = value;

        private readonly EasyEvent<(T, T)> onValueChanged = new();
        public LinkUnRegister<(T, T)> Bind(Action<(T, T)> callback)
        {
            if (callback != null)
            {
                return onValueChanged.Register(callback);
            }
            throw new ArgumentNullException(nameof(callback));
        }
        public LinkUnRegister<(T, T)> BindWithCallback(Action<(T, T)> callback)
        {
            if (callback != null)
            {
                callback?.Invoke((mValue, mValue));
                return onValueChanged.Register(callback);
            }
            throw new ArgumentNullException(nameof(callback));
        }

        public void UnBind(Action<(T, T)> callback)
        {
            if (callback != null) onValueChanged.UnRegister(callback);
        }
        public void UnBindAll() => onValueChanged.UnRegisterAll();

        private static Func<T, T, bool> mCompareFunc = (x, y) => x.Equals(y);
        public static void SetCompareFunc(Func<T, T, bool> func) => mCompareFunc = func;
        public override string ToString() => Value.ToString();
    }
}