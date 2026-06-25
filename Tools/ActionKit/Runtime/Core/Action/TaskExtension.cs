using System;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="ISequence"/> 的 Task Action 扩展方法。
    /// </summary>
    public static class TaskExtension
    {
        /// <summary>
        /// 向序列中追加一个由 <see cref="System.Threading.Tasks.Task"/> 驱动的 Action。
        /// </summary>
        /// <param name="self">目标序列。</param>
        /// <param name="taskGetter">返回任务的委托。</param>
        public static ISequence Task(this ISequence self, Func<Task> taskGetter) =>
            self.Append(TaskAction.Allocate(taskGetter));

        /// <summary>
        /// 将 Task 包装为 Action。
        /// </summary>
        /// <param name="self">目标 Task。</param>
        public static IAction ToAction(this Task self) => TaskAction.Allocate(() => self);
    }
}
