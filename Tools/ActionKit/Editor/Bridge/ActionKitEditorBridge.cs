#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 的 Editor 监控桥接。
    /// 负责在 PlayMode 中把 Action 生命周期事件同步到 <see cref="EditorDataBridge"/>。
    /// </summary>
    [InitializeOnLoad]
    public static class ActionKitEditorBridge
    {
        private static readonly Bridge sBridge;

        static ActionKitEditorBridge()
        {
            sBridge = new Bridge();
        }

        /// <summary>
        /// ActionKit 实际桥接实现。
        /// </summary>
        private sealed class Bridge : PlayModeEditorBridgeBase
        {
            /// <summary>
            /// 进入 PlayMode 后挂接运行时 Action 调试回调。
            /// </summary>
            protected override void OnEnteredPlayMode()
            {
                ActionEditorHooks.OnActionStarted = OnActionStarted;
                ActionEditorHooks.OnActionFinished = OnActionFinished;
            }

            /// <summary>
            /// 退出 PlayMode 前清理运行时 Action 调试回调。
            /// </summary>
            protected override void OnExitingPlayMode()
            {
                ActionEditorHooks.Clear();
            }

            /// <summary>
            /// 推送 Action 开始事件。
            /// </summary>
            private static void OnActionStarted(IAction action)
            {
                EditorDataBridge.NotifyDataChanged(DataChannels.ACTION_STARTED, action);
            }

            /// <summary>
            /// 推送 Action 完成事件。
            /// </summary>
            private static void OnActionFinished(IAction action)
            {
                EditorDataBridge.NotifyDataChanged(DataChannels.ACTION_FINISHED, action);
            }
        }
    }
}
#endif
