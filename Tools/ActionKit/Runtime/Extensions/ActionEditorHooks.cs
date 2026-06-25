using System;

namespace YokiFrame
{
    /// <summary>
    /// Action 编辑器钩子，运行时向编辑器发送通知，编辑器代码注册委托即可接收。
    /// </summary>
    public static class ActionEditorHooks
    {
        /// <summary>
        /// Action 开始执行时触发。
        /// </summary>
        public static Action<IAction> OnActionStarted;

        /// <summary>
        /// Action 完成时触发。
        /// </summary>
        public static Action<IAction> OnActionFinished;

        /// <summary>
        /// 清理所有编辑器钩子。
        /// </summary>
        public static void Clear()
        {
            OnActionStarted = null;
            OnActionFinished = null;
        }
    }
}
