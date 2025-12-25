using System;

namespace YokiFrame
{
    public interface IBindable<T>
    {
        T Value { get; set; }
        LinkUnRegister<T> Bind(Action<T> callback);
        void UnBind(Action<T> value);
        void UnBindAll();
        virtual LinkUnRegister<T> BindWithCallbvack(Action<T> callback)
        {
            callback?.Invoke(Value);
            return Bind(callback);
        }
    }
}