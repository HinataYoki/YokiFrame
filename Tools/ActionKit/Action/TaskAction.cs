using System;
using System.Threading.Tasks;

namespace YokiFrame
{
    internal class TaskAction : ActionBase
    {
        private Func<Task> mTaskGetter = null;
        private Task mExecutingTask;
        private static readonly SimplePoolKit<TaskAction> mPool = new(() => new TaskAction());

        public static TaskAction Allocate(Func<Task> taskGetter)
        {
            var taskAction = mPool.Allocate();
            taskAction.ActionID = ActionKit.ID_GENERATOR++;
            taskAction.Deinited = false;
            taskAction.OnInit();
            taskAction.mTaskGetter = taskGetter;
            return taskAction;
        }

        public override void OnStart() => StartTask();

        async void StartTask()
        {
            mExecutingTask = mTaskGetter();
            await mExecutingTask;
            this.Finish();
        }

        public override void OnDeinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                mTaskGetter = null;
                if (mExecutingTask != null)
                {
                    mExecutingTask.Dispose();
                    mExecutingTask = null;
                }

                MonoRecycler.AddRecycleCallback(new ActionRecycler<TaskAction>(mPool, this));
            }
        }

        public override string GetDebugInfo()
        {
            if (mExecutingTask?.Exception != null)
                return $"TaskAction Error: {mExecutingTask.Exception.InnerExceptions}";
            return mTaskGetter != null ? $"TaskAction -> {mTaskGetter.Method.DeclaringType}.{mTaskGetter.Method.Name}" : "TaskAction";
        }
    }

    public static class TaskExtension
    {
        public static ISequence Task(this ISequence self, Func<Task> taskGetter)
        {
            return self.Append(TaskAction.Allocate(taskGetter));
        }

        public static IAction ToAction(this Task self)
        {
            return TaskAction.Allocate(() => self);
        }
    }
}