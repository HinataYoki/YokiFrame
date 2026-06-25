using System;

namespace YokiFrame
{
    /// <summary>
    /// ActionKit 调度控制器契约。
    /// </summary>
    public interface IActionController
    {
        /// <summary>
        /// 当前控制器关联的 Action 运行时编号。
        /// </summary>
        ulong CurExecuteActionID { get; set; }

        /// <summary>
        /// 当前控制器关联的 Action 运行时编号。保留旧拼写以兼容已有调用。
        /// </summary>
        [Obsolete("Use CurExecuteActionID. This misspelled alias is kept for source compatibility.", false)]
        ulong CurExcuteActionID { get; set; }

        /// <summary>
        /// 当前控制器驱动的 Action。
        /// </summary>
        IAction Action { get; set; }

        /// <summary>
        /// 当前控制器使用的时间更新模式。
        /// </summary>
        ActionUpdateModes UpdateMode { get; set; }

        /// <summary>
        /// Action 完成后调用的回调。
        /// </summary>
        Action<IActionController> Finish { get; set; }

        /// <summary>
        /// 当前 Action 是否暂停。
        /// </summary>
        bool Paused { get; set; }

        /// <summary>
        /// 当前控制器是否已取消。
        /// </summary>
        bool IsCancelled { get; }

        /// <summary>
        /// 开始驱动 Action。
        /// </summary>
        void OnStart();

        /// <summary>
        /// 结束驱动 Action。
        /// </summary>
        void OnEnd();

        /// <summary>
        /// 取消当前控制器。
        /// </summary>
        void Cancel();

        /// <summary>
        /// 回收当前控制器。
        /// </summary>
        void Recycle();
    }
}
