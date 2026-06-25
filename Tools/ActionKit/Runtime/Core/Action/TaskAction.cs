using System;
using System.Threading.Tasks;

namespace YokiFrame
{
    internal class TaskAction : ActionBase
    {
        private static readonly YokiFrame.SimplePoolKit<TaskAction> sPool = new(static () => new TaskAction());

        private Func<Task> mTaskGetter;
        private Task mExecutingTask;

        static TaskAction()
        {
            ActionKitScheduler.RegisterRecycleProcessor<TaskAction>();
        }

        internal static TaskAction Allocate(Func<Task> taskGetter)
        {
            var taskAction = sPool.Allocate();
            taskAction.ActionID = ActionKit.sIdGenerator++;
            taskAction.Deinited = false;
            taskAction.OnInit();
            taskAction.mTaskGetter = taskGetter;
            return taskAction;
        }

        public override void OnStart()
        {
            _ = StartTaskSafe();
        }

        public override void OnDeinit()
        {
            if (Deinited) return;

            Deinited = true;
            mTaskGetter = null;
            mExecutingTask = null;
            ActionRecyclerManager.AddRecycleCallback(new ActionRecycler<TaskAction>(sPool, this));
        }

        public override string GetDebugInfo()
        {
            if (mExecutingTask?.Exception != null)
                return $"TaskAction Error: {mExecutingTask.Exception.InnerExceptions}";
            return mTaskGetter != null ? $"TaskAction -> {mTaskGetter.Method.DeclaringType}.{mTaskGetter.Method.Name}" : "TaskAction";
        }

        private async Task StartTaskSafe()
        {
            try
            {
                if (Deinited || mTaskGetter == null) return;
                mExecutingTask = mTaskGetter();
                await mExecutingTask;
                if (Deinited) return;
                this.Finish();
            }
            catch (Exception e)
            {
                ActionKitRuntimeLog.Error("[TaskAction] 执行异常: " + e.Message + "\n" + e.StackTrace);
                if (Deinited) return;
                this.Finish();
            }
        }
    }
}
