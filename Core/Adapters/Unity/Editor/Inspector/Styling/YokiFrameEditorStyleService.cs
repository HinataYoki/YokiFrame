#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;

namespace YokiFrame.Unity.Inspector
{
    /// <summary>
    /// YokiFrame Unity Editor 共享样式服务。
    /// 样式按设计令牌、通用组件和宿主功能三层加载，不依赖旧版目录结构。
    /// </summary>
    public static class YokiFrameEditorStyleService
    {
        private const string TOKEN_STYLE_NAME = "YokiFrameEditorTokens";
        private const string COMPONENT_STYLE_NAME = "YokiFrameEditorComponents";
        private const string ADVANCED_COMPONENT_STYLE_NAME = "YokiFrameEditorAdvancedComponents";
        private const string INSPECTOR_STYLE_NAME = "InspectorKit";
        private const string INSPECTOR_HIERARCHY_STYLE_NAME = "InspectorKitHierarchy";

        private static StyleSheet sTokens;
        private static StyleSheet sComponents;
        private static StyleSheet sAdvancedComponents;
        private static StyleSheet sInspector;
        private static StyleSheet sInspectorHierarchy;

        /// <summary>
        /// 将指定样式 profile 应用到视觉树根元素。
        /// </summary>
        /// <param name="root">需要应用样式的视觉树根元素。</param>
        /// <param name="profile">样式 profile。</param>
        public static void Apply(VisualElement root, YokiFrameEditorStyleProfile profile)
        {
            if (root == null)
                return;

            AddStyleSheet(root, LoadStyleSheet(YokiFrameEditorStyleSheet.Tokens));
            AddStyleSheet(root, LoadStyleSheet(YokiFrameEditorStyleSheet.Components));
            AddStyleSheet(root, LoadStyleSheet(YokiFrameEditorStyleSheet.AdvancedComponents));
            if (profile == YokiFrameEditorStyleProfile.Inspector)
            {
                AddStyleSheet(root, LoadStyleSheet(YokiFrameEditorStyleSheet.InspectorKit));
                AddStyleSheet(root, LoadStyleSheet(YokiFrameEditorStyleSheet.InspectorKitHierarchy));
            }
        }

        /// <summary>
        /// 清理样式缓存，供 Unity 域重载或资源重新导入后调用。
        /// </summary>
        public static void ClearCache()
        {
            sTokens = null;
            sComponents = null;
            sAdvancedComponents = null;
            sInspector = null;
            sInspectorHierarchy = null;
        }

        /// <summary>
        /// 按语义枚举加载并缓存 USS 资源。
        /// </summary>
        /// <param name="styleSheet">需要加载的样式类别。</param>
        /// <returns>样式资源；找不到时返回 null。</returns>
        private static StyleSheet LoadStyleSheet(YokiFrameEditorStyleSheet styleSheet)
        {
            switch (styleSheet)
            {
                case YokiFrameEditorStyleSheet.Tokens:
                    if (sTokens == null)
                        sTokens = FindStyleSheet(TOKEN_STYLE_NAME);
                    return sTokens;
                case YokiFrameEditorStyleSheet.Components:
                    if (sComponents == null)
                        sComponents = FindStyleSheet(COMPONENT_STYLE_NAME);
                    return sComponents;
                case YokiFrameEditorStyleSheet.AdvancedComponents:
                    if (sAdvancedComponents == null)
                        sAdvancedComponents = FindStyleSheet(ADVANCED_COMPONENT_STYLE_NAME);
                    return sAdvancedComponents;
                case YokiFrameEditorStyleSheet.InspectorKit:
                    if (sInspector == null)
                        sInspector = FindStyleSheet(INSPECTOR_STYLE_NAME);
                    return sInspector;
                case YokiFrameEditorStyleSheet.InspectorKitHierarchy:
                    if (sInspectorHierarchy == null)
                        sInspectorHierarchy = FindStyleSheet(INSPECTOR_HIERARCHY_STYLE_NAME);
                    return sInspectorHierarchy;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 在当前包或项目中按精确文件名解析样式资源。
        /// </summary>
        /// <param name="assetName">不含扩展名的 USS 文件名。</param>
        /// <returns>找到的样式资源；找不到时返回 null。</returns>
        private static StyleSheet FindStyleSheet(string assetName)
        {
            string[] guids = AssetDatabase.FindAssets(assetName + " t:StyleSheet");
            string expectedSuffix = "/" + assetName + ".uss";
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (!path.EndsWith(expectedSuffix, System.StringComparison.Ordinal))
                    continue;

                return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }

            return null;
        }

        /// <summary>
        /// 向视觉树添加尚未加载的样式表，避免重复引用。
        /// </summary>
        /// <param name="root">目标根元素。</param>
        /// <param name="styleSheet">待添加样式表。</param>
        private static void AddStyleSheet(VisualElement root, StyleSheet styleSheet)
        {
            if (styleSheet == null)
                return;

            for (int index = 0; index < root.styleSheets.count; index++)
            {
                if (root.styleSheets[index] == styleSheet)
                    return;
            }

            root.styleSheets.Add(styleSheet);
        }
    }

    /// <summary>
    /// YokiFrame Unity Editor 样式 profile。
    /// </summary>
    public enum YokiFrameEditorStyleProfile
    {
        /// <summary>只加载 Tokens 和通用组件。</summary>
        Core,
        /// <summary>加载通用层和 InspectorKit 专属样式。</summary>
        Inspector
    }

    /// <summary>
    /// YokiFrame Unity Editor 样式资源类别。
    /// </summary>
    public enum YokiFrameEditorStyleSheet
    {
        /// <summary>设计令牌。</summary>
        Tokens,
        /// <summary>通用 UI 组件。</summary>
        Components,
        /// <summary>扩展状态、验证和工具组件。</summary>
        AdvancedComponents,
        /// <summary>InspectorKit 专属组件。</summary>
        InspectorKit,
        /// <summary>InspectorKit 紧凑层级组件。</summary>
        InspectorKitHierarchy
    }
}
#endif
