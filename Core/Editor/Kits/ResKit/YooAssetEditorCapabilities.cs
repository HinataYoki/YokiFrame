#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
using UnityEditor;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooAsset 版本能力单一入口。
    /// 所有版本相关的 #if 判断集中于此，后续维护只需修改此类。
    /// </summary>
    public static class YooAssetEditorCapabilities
    {
        /// <summary>是否运行 YooAsset 3.x</summary>
        public static bool IsV3 =>
#if YOOASSET_3_0_OR_NEWER
            true;
#else
            false;
#endif

        /// <summary>是否支持内置收集器 UI（2.x / 3.x 各有一套）</summary>
        public static bool HasCollectorUI => true;

        /// <summary>是否支持打包配置面板（2.x / 3.x 各有一套）</summary>
        public static bool HasBuildPanel => true;

        /// <summary>
        /// 创建打包配置卡片 — 自动分发 V2 / V3 实现。
        /// </summary>
        /// <returns>Build 配置 VisualElement，不支持时返回 null</returns>
        public static VisualElement CreateBuildConfigCard(SerializedProperty property)
        {
#if YOOASSET_3_0_OR_NEWER
            return YooInitConfigDrawer.CreateBuildConfigCardV3(property);
#else
            return YooInitConfigDrawer.CreateBuildConfigCardV2(property);
#endif
        }
    }
}
#endif
