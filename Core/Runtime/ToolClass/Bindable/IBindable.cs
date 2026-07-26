using System;

namespace YokiFrame
{
    /// <summary>
    /// 定义支持值变化订阅的轻量绑定契约。
    /// </summary>
    /// <typeparam name="T">绑定值类型。</typeparam>
    public interface IBindable<T>
    {
        /// <summary>
        /// 获取或设置当前值；实现负责决定相同值是否触发通知。
        /// </summary>
        T Value { get; set; }

        /// <summary>
        /// 注册值变化回调。
        /// </summary>
        /// <param name="callback">值变化后调用的回调。</param>
        /// <returns>用于取消当前订阅的 EventKit 令牌。</returns>
        LinkUnRegister<T> Bind(Action<T> callback);

        /// <summary>
        /// 注销最后一个匹配的值变化回调。
        /// </summary>
        /// <param name="callback">需要注销的回调。</param>
        void UnBind(Action<T> callback);

        /// <summary>
        /// 注销当前绑定值上的全部回调。
        /// </summary>
        void UnBindAll();
    }

    /// <summary>
    /// 提供绑定后立即同步当前值的便利扩展。
    /// </summary>
    public static class BindableExtensions
    {
        /// <summary>
        /// 注册值变化回调，并立即使用当前值调用一次。
        /// </summary>
        /// <typeparam name="T">绑定值类型。</typeparam>
        /// <param name="self">提供当前值和订阅能力的绑定对象。</param>
        /// <param name="callback">注册后立即回放当前值的回调。</param>
        /// <returns>用于取消后续值变化通知的令牌。</returns>
        public static LinkUnRegister<T> BindWithCallback<T>(this IBindable<T> self, Action<T> callback)
        {
            if (self == null)
            {
                throw new ArgumentNullException(nameof(self));
            }

            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            LinkUnRegister<T> token = self.Bind(callback);
            try
            {
                callback.Invoke(self.Value);
            }
            catch
            {
                token.UnRegister();
                throw;
            }

            return token;
        }
    }
}
