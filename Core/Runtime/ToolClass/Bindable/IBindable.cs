using System;

namespace YokiFrame
{
    public interface IBindable<T>
    {
        T Value { get; set; }
        LinkUnRegister<T> Bind(Action<T> callback);
        void UnBind(Action<T> value);
        void UnBindAll();
    }

    public static class BindableExtensions
    {
        public static LinkUnRegister<T> BindWithCallback<T>(this IBindable<T> self, Action<T> callback)
        {
            var unregister = self.Bind(callback);
            callback?.Invoke(self.Value);
            return unregister;
        }
    }
}