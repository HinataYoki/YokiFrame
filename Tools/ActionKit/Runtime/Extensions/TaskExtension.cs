using System;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>提供 ISequence 与 Task 的 TaskAction fluent 扩展。</summary>
    public static class TaskExtension
    {
        /// <summary>
        /// 向容器追加由 factory 创建的 Task。
        /// </summary>
        /// <param name="self">目标顺序语义容器。</param>
        /// <param name="taskGetter">每次 Repeat 轮次开始时创建 Task 的 factory。</param>
        /// <returns>原容器。</returns>
        public static ISequence Task(this ISequence self, Func<Task> taskGetter)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            return ActionRuntime.AppendCreated(self, TaskAction.Allocate(taskGetter));
        }

        /// <summary>
        /// 直接把已有 Task 包装为一次性 Action，不创建捕获闭包；Action 取消不会取消底层 Task。
        /// </summary>
        /// <param name="self">待观察的 Task。</param>
        /// <returns>可加入动作树或直接启动的 Action。</returns>
        public static IAction ToAction(this Task self) => TaskAction.Allocate(self);
    }
}
