#if UNITY_5_3_OR_NEWER && YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>提供不污染纯 C# ActionKit 门面的 UniTask Integration 创建入口。</summary>
    public static class ActionKitUniTask
    {
        /// <summary>创建每次执行轮次调用 factory 的 UniTask Action。</summary>
        /// <param name="taskFactory">创建 UniTask 的 factory。</param>
        /// <returns>可加入动作树或直接启动的 Action。</returns>
        public static IAction From(Func<UniTask> taskFactory) => UniTaskAction.Allocate(taskFactory);

        /// <summary>创建在 ActionKit 终结时传播取消 token 的 UniTask Action。</summary>
        /// <param name="taskFactory">接收当前轮取消 token 的 factory。</param>
        /// <returns>可加入动作树或直接启动的 Action。</returns>
        public static IAction From(Func<CancellationToken, UniTask> taskFactory) => UniTaskAction.Allocate(taskFactory);

        /// <summary>直接包装一次性 UniTask；Repeat 必须改用 factory 入口。</summary>
        /// <param name="task">待观察的一次性 UniTask。</param>
        /// <returns>可加入动作树或直接启动的 Action。</returns>
        public static IAction From(UniTask task) => UniTaskAction.Allocate(task);
    }
}
#endif
