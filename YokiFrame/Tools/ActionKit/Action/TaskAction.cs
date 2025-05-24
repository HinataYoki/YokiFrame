using System;
using System.Threading.Tasks;

namespace YokiFrame
{
    internal class TaskAction : IAction
    {
        private Func<Task> mTaskGetter = null;
        private Task mExecutingTask;
        private static readonly SimplePoolKit<TaskAction> taskActionPool = new(() => new TaskAction());

        public static TaskAction Allocate(Func<Task> taskGetter)
        {
            var coroutineAction = taskActionPool.Allocate();
            coroutineAction.ActionID = ActionKit.ID_GENERATOR++;
            coroutineAction.Deinited = false;
            coroutineAction.OnInit();
            coroutineAction.mTaskGetter = taskGetter;
            return coroutineAction;
        }

        public bool Paused { get; set; }
        public bool Deinited { get; set; }
        public ulong ActionID { get; set; }
        public ActionStatus ActionState { get; set; }

        public void OnInit()
        {
            Paused = false;
            ActionState = ActionStatus.NotStart;
        }

        public void OnStart() => StartTask();

        async void StartTask()
        {
            mExecutingTask = mTaskGetter();
            await mExecutingTask;
            this.Finish();
        }

        public void OnDeinit()
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

                MonoRecycler.AddRecycleCallback(new ActionRecycler<TaskAction>(taskActionPool, this));
            }
        }

        string IAction.LogError() => $" {mExecutingTask.Exception.InnerExceptions} 出错";
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