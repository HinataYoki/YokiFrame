#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ActionKit 编辑器桥接 - 注册运行时钩子并转发到 EditorDataBridge
    /// 实现响应式编辑器更新，运行时零侵入
    /// </summary>
    [InitializeOnLoad]
    public static class ActionKitEditorBridge
    {
        static ActionKitEditorBridge()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    RegisterHooks();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    UnregisterHooks();
                    break;
            }
        }

        private static void RegisterHooks()
        {
            // 注册运行时钩子，转发到 EditorDataBridge
            ActionEditorHooks.OnActionStarted = OnActionStarted;
            ActionEditorHooks.OnActionFinished = OnActionFinished;
        }

        private static void UnregisterHooks()
        {
            ActionEditorHooks.Clear();
        }

        #region 事件处理

        private static void OnActionStarted(IAction action)
        {
            EditorDataBridge.NotifyDataChanged(DataChannels.ACTION_STARTED, action);
        }

        private static void OnActionFinished(IAction action)
        {
            EditorDataBridge.NotifyDataChanged(DataChannels.ACTION_FINISHED, action);
        }

        #endregion
    }
}
#endif
