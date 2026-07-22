using System;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>
    /// 在宿主 Tick 上轮询 Task 终态，避免 async continuation 回写已经复用的 Action 租约。
    /// </summary>
    internal sealed class TaskAction : ActionBase, IPooledAction
    {
        private static readonly ObjectPool<TaskAction> sPool = PoolKit.Create(
            static () => new TaskAction(), null, static action => action.ResetForPool(), ActionPoolSettings.Default);
        private Func<Task> mTaskFactory;
        private Task mTask;

        /// <summary>限制实例只能由静态 Allocate 和 PoolKit 创建。</summary>
        private TaskAction() { }

        /// <summary>
        /// 分配一个延迟调用 Task factory 的 Action。
        /// </summary>
        /// <param name="taskFactory">首次执行时创建 Task 的委托。</param>
        /// <returns>新的 TaskAction 租约。</returns>
        internal static TaskAction Allocate(Func<Task> taskFactory)
        {
            if (taskFactory == null) throw new ArgumentNullException(nameof(taskFactory));
            TaskAction action = AllocateCore();
            action.mTaskFactory = taskFactory;
            return action;
        }

        /// <summary>
        /// 直接包装已有 Task，不创建捕获闭包。
        /// </summary>
        /// <param name="task">待观察 Task。</param>
        /// <returns>新的 TaskAction 租约。</returns>
        internal static TaskAction Allocate(Task task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            TaskAction action = AllocateCore();
            action.mTask = task;
            return action;
        }

        /// <summary>重置当前轮次；factory 模式丢弃上一轮已完成 Task，使 Repeat 每轮重新创建任务。</summary>
        public override void OnInit()
        {
            base.OnInit();
            if (mTaskFactory != null) ReleaseTaskReference();
        }

        /// <summary>创建或采用 Task，并同步观察已经结束的终态。</summary>
        public override void OnStart()
        {
            if (mTask == null) mTask = mTaskFactory();
            if (mTask == null) throw new InvalidOperationException("Task factory returned null.");
            ObserveCompletion();
        }

        /// <summary>每个宿主 Tick 只读取 Task 状态，完成时在当前线程解析终态。</summary>
        public override void OnExecute(float dt) => ObserveCompletion();

        /// <summary>
        /// 取消 ActionKit 不取消底层 Task；若 Task 仍运行则安装只观察未来 fault 的静态 continuation。
        /// </summary>
        public override void OnDeinit()
        {
            ReleaseTaskReference();
            mTaskFactory = null;
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>创建 Task 状态和 factory 目标按需诊断文本。</summary>
        public override string GetDebugInfo()
        {
            if (mTask != null) return "TaskAction(" + mTask.Status + ")";
            return mTaskFactory == null
                ? "TaskAction"
                : "TaskAction -> " + mTaskFactory.Method.DeclaringType + "." + mTaskFactory.Method.Name;
        }
#endif

        /// <summary>把已释放实例归还 TaskAction 局部池。</summary>
        void IPooledAction.ReturnToPool() => sPool.Recycle(this);

        /// <summary>从局部池分配公共状态，并安装新的运行 ID。</summary>
        private static TaskAction AllocateCore()
        {
            TaskAction action = sPool.Allocate();
            action.PreparePooled(ActionKitScheduler.NextActionId());
            return action;
        }

        /// <summary>在当前线程解析完成、取消或 fault；异常交给 Scheduler 形成 Faulted 终态。</summary>
        private void ObserveCompletion()
        {
            if (mTask == null || !mTask.IsCompleted) return;
            mTask.GetAwaiter().GetResult();
            this.Finish();
        }

        /// <summary>为取消后仍运行的 Task 安装无 Action 引用的 fault 观察器。</summary>
        private static void ObserveFutureFault(Task task)
        {
            _ = task.ContinueWith(
                static completed => { _ = completed.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>释放当前 Task 强引用，并确保现在或未来的 fault 都被观察。</summary>
        private void ReleaseTaskReference()
        {
            Task task = mTask;
            mTask = null;
            if (task == null) return;
            if (!task.IsCompleted) ObserveFutureFault(task);
            else if (task.IsFaulted) _ = task.Exception;
        }

        /// <summary>回池前清除 factory 和 Task 引用。</summary>
        private void ResetForPool()
        {
            mTaskFactory = null;
            mTask = null;
        }
    }
}
