#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 样式配置文件类型
    /// </summary>
    public enum YokiStyleProfile
    {
        /// <summary>完整样式（Window 使用）</summary>
        Full,
        /// <summary>仅核心组件（Inspector 使用）</summary>
        CoreOnly,
        /// <summary>指定 Kit 样式</summary>
        Kit
    }

    /// <summary>
    /// YokiFrame 样式服务
    /// 
    /// 提供样式加载、缓存、去重、按 Profile 应用功能。
    /// 四层样式加载顺序：Tokens → Core → Shell → Kit
    /// 
    /// 依赖方向: Window/Page → StyleService → StyleRegistry
    /// </summary>
    public static class YokiStyleService
    {
        #region 常量路径

        // 四层样式路径（动态根路径，支持 Packages/、Assets/、Assets/Plugins/）
        private static string TOKENS_PATH => YokiEditorPaths.CombineWithStylingRoot("Tokens/YokiTokens.uss");
        private static string CORE_PATH => YokiEditorPaths.CombineWithStylingRoot("Core/YokiCoreComponents.uss");
        private static string SHELL_PATH => YokiEditorPaths.CombineWithStylingRoot("Shell/YokiWindowShell.uss");

        #endregion

        #region 缓存

        private static StyleSheet sCachedTokens;
        private static StyleSheet sCachedCore;
        private static StyleSheet sCachedShell;
        private static readonly Dictionary<string, StyleSheet> sKitStyleCache = new(16);
        private static readonly HashSet<int> sAppliedStyleIds = new(32);

        #endregion

        #region 公共 API

        /// <summary>
        /// 应用样式到 VisualElement
        /// </summary>
        /// <param name="root">目标元素</param>
        /// <param name="profile">样式配置</param>
        /// <param name="kitName">Kit 名称（仅 Kit profile 需要）</param>
        public static void Apply(VisualElement root, YokiStyleProfile profile, string kitName = null)
        {
            if (root == default) return;

            sAppliedStyleIds.Clear();

            switch (profile)
            {
                case YokiStyleProfile.Full:
                    ApplyFullProfile(root);
                    break;

                case YokiStyleProfile.CoreOnly:
                    ApplyCoreOnlyProfile(root);
                    break;

                case YokiStyleProfile.Kit:
                    ApplyKitProfile(root, kitName);
                    break;
            }
        }

        /// <summary>
        /// 加载指定 Kit 的样式表
        /// </summary>
        public static StyleSheet LoadKitStyleSheet(string kitName)
        {
            if (string.IsNullOrEmpty(kitName)) return default;

            if (sKitStyleCache.TryGetValue(kitName, out var cached))
            {
                return cached;
            }

            // 从 Registry 获取路径
            var styles = YokiStyleRegistry.GetStylesForKit(kitName);
            foreach (var info in styles)
            {
                var path = ResolveStyleSheetPath(info.StyleSheetPath);
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != default)
                {
                    sKitStyleCache[kitName] = sheet;
                    return sheet;
                }
            }

            return default;
        }

        /// <summary>
        /// 为页面元素应用 Kit 专属样式（懒加载）
        /// </summary>
        public static void ApplyKitStyleToElement(VisualElement pageElement, string kitName)
        {
            if (pageElement == default || string.IsNullOrEmpty(kitName)) return;

            var kitSheet = LoadKitStyleSheet(kitName);
            if (kitSheet == default) return;

            // 检查是否已添加
            for (int i = 0; i < pageElement.styleSheets.count; i++)
            {
                if (pageElement.styleSheets[i] == kitSheet) return;
            }

            pageElement.styleSheets.Add(kitSheet);
        }

        /// <summary>
        /// 清除缓存（用于热重载）
        /// </summary>
        public static void ClearCache()
        {
            sCachedTokens = default;
            sCachedCore = default;
            sCachedShell = default;
            sKitStyleCache.Clear();
            sAppliedStyleIds.Clear();
        }

        #endregion


        #region 私有方法

        /// <summary>
        /// 应用完整样式配置（Window 使用）
        /// </summary>
        private static void ApplyFullProfile(VisualElement root)
        {
            var tokens = LoadTokens();
            var core = LoadCore();
            var shell = LoadShell();

            // 应用样式（去重）
            if (tokens != default) AddStyleSheetIfNotExists(root, tokens);
            if (core != default) AddStyleSheetIfNotExists(root, core);
            if (shell != default) AddStyleSheetIfNotExists(root, shell);

            // Kit 样式通过 ApplyKitStyleToElement() 懒加载
        }

        /// <summary>
        /// 应用仅核心组件样式（Inspector 使用）
        /// </summary>
        private static void ApplyCoreOnlyProfile(VisualElement root)
        {
            var tokens = LoadTokens();
            var core = LoadCore();

            if (tokens != default) AddStyleSheetIfNotExists(root, tokens);
            if (core != default) AddStyleSheetIfNotExists(root, core);
        }

        /// <summary>
        /// 应用指定 Kit 样式
        /// </summary>
        private static void ApplyKitProfile(VisualElement root, string kitName)
        {
            // 先应用核心样式
            ApplyCoreOnlyProfile(root);

            // 再应用 Kit 专用样式
            if (!string.IsNullOrEmpty(kitName))
            {
                var kitSheet = LoadKitStyleSheet(kitName);
                if (kitSheet != default)
                {
                    AddStyleSheetIfNotExists(root, kitSheet);
                }
            }
        }

        /// <summary>
        /// 添加样式表（去重）
        /// </summary>
        private static void AddStyleSheetIfNotExists(VisualElement root, StyleSheet sheet)
        {
            int id = sheet.GetInstanceID();
            if (sAppliedStyleIds.Contains(id)) return;

            root.styleSheets.Add(sheet);
            sAppliedStyleIds.Add(id);
        }

        private static string ResolveStyleSheetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;

            // Already an AssetDatabase path
            if (path.StartsWith("Assets/") || path.StartsWith("Packages/"))
            {
                return path;
            }

            // Relative to EditorTools root
            if (path.StartsWith("Styling/"))
            {
                return YokiEditorPaths.CombineWithEditorToolsRoot(path);
            }

            // Relative to Styling root
            return YokiEditorPaths.CombineWithStylingRoot(path);
        }

        #endregion

        #region 样式加载

        private static StyleSheet LoadTokens()
        {
            if (sCachedTokens == default)
            {
                sCachedTokens = AssetDatabase.LoadAssetAtPath<StyleSheet>(TOKENS_PATH);
            }
            return sCachedTokens;
        }

        private static StyleSheet LoadCore()
        {
            if (sCachedCore == default)
            {
                sCachedCore = AssetDatabase.LoadAssetAtPath<StyleSheet>(CORE_PATH);
            }
            return sCachedCore;
        }

        private static StyleSheet LoadShell()
        {
            if (sCachedShell == default)
            {
                sCachedShell = AssetDatabase.LoadAssetAtPath<StyleSheet>(SHELL_PATH);
            }
            return sCachedShell;
        }

        #endregion
    }
}
#endif
