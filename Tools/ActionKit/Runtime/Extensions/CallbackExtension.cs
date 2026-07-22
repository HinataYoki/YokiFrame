using System;

namespace YokiFrame
{
    /// <summary>提供 ISequence 的回调节点 fluent 扩展。</summary>
    public static class CallbackExtension
    {
        /// <summary>
        /// 向容器追加立即回调节点。
        /// </summary>
        /// <param name="self">目标顺序语义容器。</param>
        /// <param name="callback">首次执行时调用的委托。</param>
        /// <returns>原容器。</returns>
        public static ISequence Callback(this ISequence self, Action callback)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            return ActionRuntime.AppendCreated(self, YokiFrame.Callback.Allocate(callback));
        }
    }
}
