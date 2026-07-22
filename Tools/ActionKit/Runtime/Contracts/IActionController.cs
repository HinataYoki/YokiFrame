using System;

namespace YokiFrame
{
    /// <summary>
    /// 提供根 Action 的稳定生命周期 handle；完成后旧 handle 不会被复用给其它动作。
    /// </summary>
    public interface IActionController
    {
        /// <summary>获取当前 handle 创建时绑定的 Action ID。</summary>
        ulong CurExecuteActionID { get; }

        /// <summary>获取当前 handle 绑定的根 Action。</summary>
        IAction Action { get; }

        /// <summary>获取或在 Scheduler 宿主线程设置当前根 Action 使用的时间源。</summary>
        ActionUpdateModes UpdateMode { get; set; }

        /// <summary>获取仅在正常完成时调用的 controller 回调。</summary>
        Action<IActionController> Finish { get; }

        /// <summary>获取或在 Scheduler 宿主线程设置根动作树是否暂停。</summary>
        bool Paused { get; set; }

        /// <summary>获取当前 handle 是否已请求或完成取消。</summary>
        bool IsCancelled { get; }

        /// <summary>获取当前 handle 是否已经离开调度器。</summary>
        bool IsCompleted { get; }

        /// <summary>获取当前 handle 是否因生命周期异常结束。</summary>
        bool IsFaulted { get; }

        /// <summary>从任意线程请求取消当前执行租约；重复调用保持幂等，清理由宿主 Tick 完成。</summary>
        void Cancel();
    }
}
