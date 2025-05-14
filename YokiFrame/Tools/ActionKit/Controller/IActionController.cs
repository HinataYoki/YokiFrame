using System;

namespace YokiFrame
{
    public enum ActionUpdateModes
    {
        ScaledDeltaTime,
        UnscaledDeltaTime,
    }

    public interface IActionController
    {
        /// <summary>
        /// 当前执行的任务ID
        /// </summary>
        ulong CurExcuteActionID { get; set; }
        /// <summary>
        /// 任务
        /// </summary>
        IAction Action { get; set; }
        /// <summary>
        /// 任务更新模式
        /// </summary>
        ActionUpdateModes UpdateMode { get; set; }
        /// <summary>
        /// 任务结束回调
        /// </summary>
        Action<IActionController> Finish { get; set; }
        /// <summary>
        /// 暂停任务
        /// </summary>
        bool Paused { get; set; }

        /// <summary>
        /// 任务开始
        /// </summary>
        void OnStart();
        /// <summary>
        /// 任务结束
        /// </summary>
        void OnEnd();
        /// <summary>
        /// 回收
        /// </summary>
        void Recycle();
    }
}