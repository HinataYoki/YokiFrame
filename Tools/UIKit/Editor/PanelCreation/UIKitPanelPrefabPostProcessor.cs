#if UNITY_EDITOR
using UnityEditor;

namespace YokiFrame
{
    /// <summary>
    /// 在编辑器编译完成后继续处理待绑定的 UIKit 面板 Prefab。
    /// </summary>
    [InitializeOnLoad]
    internal static class UIKitPanelPrefabPostProcessor
    {
        static UIKitPanelPrefabPostProcessor()
        {
            EditorApplication.delayCall += UIKitPanelPrefabCreator.ProcessPendingPrefabBindings;
        }
    }
}
#endif
