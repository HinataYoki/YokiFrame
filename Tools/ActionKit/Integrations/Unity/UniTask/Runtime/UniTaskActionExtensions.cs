#if UNITY_5_3_OR_NEWER && YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>提供 ISequence 与 UniTask 的 Integration fluent 扩展。</summary>
    public static class UniTaskActionExtensions
    {
        /// <summary>向容器追加每轮调用 factory 的 UniTask Action。</summary>
        /// <param name="self">目标顺序语义容器。</param>
        /// <param name="taskFactory">每轮创建 UniTask 的 factory。</param>
        /// <returns>原容器。</returns>
        public static ISequence UniTask(this ISequence self, Func<UniTask> taskFactory)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            return ActionRuntime.AppendCreated(self, UniTaskAction.Allocate(taskFactory));
        }

        /// <summary>向容器追加接收当前轮取消 token 的 UniTask Action。</summary>
        /// <param name="self">目标顺序语义容器。</param>
        /// <param name="taskFactory">接收当前轮取消 token 的 factory。</param>
        /// <returns>原容器。</returns>
        public static ISequence UniTask(this ISequence self, Func<CancellationToken, UniTask> taskFactory)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            return ActionRuntime.AppendCreated(self, UniTaskAction.Allocate(taskFactory));
        }

        /// <summary>直接包装一次性 UniTask，不创建捕获闭包；Repeat 必须改用 factory。</summary>
        /// <param name="self">待观察的一次性 UniTask。</param>
        /// <returns>可加入动作树或直接启动的 Action。</returns>
        public static IAction ToAction(this UniTask self) => UniTaskAction.Allocate(self);
    }
}
#endif
