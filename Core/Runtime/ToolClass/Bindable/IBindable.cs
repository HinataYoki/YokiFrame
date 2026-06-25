using System;

namespace YokiFrame
{
    /// <summary>
    /// 支持变化绑定的值契约。
    /// </summary>
    public interface IBindable<T>
    {
        T Value { get; set; }

        /// <summary>注册值变化回调。</summary>
        LinkUnRegister<T> Bind(Action<T> callback);

        /// <summary>注销一个值变化回调。</summary>
        void UnBind(Action<T> value);

        /// <summary>注销全部回调。</summary>
        void UnBindAll();
    }

    /// <summary>
    /// 可绑定值的便利扩展。
    /// </summary>
    public static class BindableExtensions
    {
        /// <summary>
        /// 注册回调，并立即用当前值调用一次。
        /// </summary>
        public static LinkUnRegister<T> BindWithCallback<T>(this IBindable<T> self, Action<T> callback)
        {
            var unregister = self.Bind(callback);
            callback?.Invoke(self.Value);
            return unregister;
        }
    }
}
