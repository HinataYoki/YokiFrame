using System;

namespace YokiFrame
{
    /// <summary>
    /// ActionKit 默认控制器实现。
    /// </summary>
    public class ActionController : IActionController
    {
        private static readonly YokiFrame.SimplePoolKit<IActionController> sControllerPool = new(
            () => new ActionController(),
            static controller =>
        {
            controller.UpdateMode = ActionUpdateModes.ScaledDeltaTime;
            controller.CurExecuteActionID = 0;
            controller.Action = null;
            controller.Finish = null;
            ((ActionController)controller).mIsCancelled = false;
        });

        private bool mIsCancelled;
        private ulong mCurExecuteActionID;

        /// <summary>
        /// 从控制器池分配一个控制器。
        /// </summary>
        public static IActionController Allocate() => sControllerPool.Allocate();

        /// <summary>
        /// 当前控制器关联的 Action 运行时编号。
        /// </summary>
        public ulong CurExecuteActionID
        {
            get => mCurExecuteActionID;
            set => mCurExecuteActionID = value;
        }

        /// <summary>
        /// 当前控制器关联的 Action 运行时编号。保留旧拼写以兼容已有调用。
        /// </summary>
        [Obsolete("Use CurExecuteActionID. This misspelled alias is kept for source compatibility.", false)]
        public ulong CurExcuteActionID
        {
            get => CurExecuteActionID;
            set => CurExecuteActionID = value;
        }

        /// <summary>
        /// 当前控制器驱动的 Action。
        /// </summary>
        public IAction Action { get; set; }

        /// <summary>
        /// Action 完成后调用的回调。
        /// </summary>
        public Action<IActionController> Finish { get; set; }

        /// <summary>
        /// 当前控制器使用的时间更新模式。
        /// </summary>
        public ActionUpdateModes UpdateMode { get; set; }

        /// <summary>
        /// 当前控制器是否已取消。
        /// </summary>
        public bool IsCancelled => mIsCancelled;

        /// <summary>
        /// 当前 Action 是否暂停。
        /// </summary>
        public bool Paused
        {
            get => Action.Paused;
            set => Action.Paused = value;
        }

        /// <summary>
        /// 开始驱动 Action。
        /// </summary>
        public void OnStart()
        {
            if (Action.ActionID == CurExecuteActionID)
                Action.OnInit();
        }

        /// <summary>
        /// 结束驱动 Action。
        /// </summary>
        public void OnEnd()
        {
            if (Action != null && Action.ActionID == CurExecuteActionID)
                Action.OnDeinit();
        }

        /// <summary>
        /// 取消当前控制器。
        /// </summary>
        public void Cancel()
        {
            if (mIsCancelled) return;
            mIsCancelled = true;
            ActionKitScheduler.CancelAction(this);
        }

        /// <summary>
        /// 回收当前控制器。
        /// </summary>
        public void Recycle()
        {
            sControllerPool.Recycle(this);
        }
    }
}
