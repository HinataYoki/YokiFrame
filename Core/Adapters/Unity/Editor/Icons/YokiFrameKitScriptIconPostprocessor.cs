#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.Unity.Editor
{
    /// <summary>
    /// 为 YokiFrame 源码开发目录中的 C# 脚本手动维护与文档树一致的 Unity 原生 MonoImporter 图标。
    /// 已发布包直接交付包含图标字段的 .meta，不在导入或域加载期间改写 package importer。
    /// </summary>
    internal static class YokiFrameKitScriptIconPostprocessor
    {
        private const string SOURCE_ROOT = "Assets/YokiFrame/";
        private const string ICON_RELATIVE_ROOT = "Core/Adapters/Unity/Editor/Icons/";
        private const string APPLY_SCRIPT_ICONS_MENU = "YokiFrame/Developer/Apply Kit Script Icons";

        private static readonly IReadOnlyDictionary<string, string> sKitRoots =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Core/Runtime/Architecture/"] = "Architecture",
                ["Core/Editor/CodeGenKit/"] = "CodeGenKit",
                ["Core/Adapters/Unity/Runtime/Inspector/"] = "InspectorKit",
                ["Core/Adapters/Unity/Editor/Inspector/"] = "InspectorKit",
                ["Core/Runtime/EventKit/"] = "EventKit",
                ["Core/Runtime/FsmKit/"] = "FsmKit",
                ["Core/Runtime/LogKit/"] = "LogKit",
                ["Core/Runtime/PoolKit/"] = "PoolKit",
                ["Core/Runtime/ResKit/"] = "ResKit",
                ["Core/Runtime/SingletonKit/"] = "SingletonKit",
                ["Core/Runtime/ToolClass/"] = "ToolClass",
                ["Tools/ActionKit/"] = "ActionKit",
                ["Tools/AudioKit/"] = "AudioKit",
                ["Tools/LocalizationKit/"] = "LocalizationKit",
                ["Tools/SaveKit/"] = "SaveKit",
                ["Tools/SceneKit/"] = "SceneKit",
                ["Tools/SpatialKit/"] = "SpatialKit",
                ["Tools/UIKit/"] = "UIKit",
            };

        /// <summary>
        /// 提供源码维护菜单，用于在新增或调整 YokiFrame 源码脚本后显式补齐图标。
        /// 已安装的 Git URL 或 embedded package 应保留随包交付的 .meta，不通过此入口写入。
        /// </summary>
        [MenuItem(APPLY_SCRIPT_ICONS_MENU)]
        private static void ApplyExistingScriptIconsFromMenu()
        {
            ApplyExistingScriptIcons();
        }

        /// <summary>
        /// 控制开发菜单仅在当前项目包含 YokiFrame 源码树时可用，避免修改安装包缓存。
        /// </summary>
        /// <returns>当前项目存在可写源码目录时返回 true。</returns>
        [MenuItem(APPLY_SCRIPT_ICONS_MENU, true)]
        private static bool CanApplyExistingScriptIconsFromMenu()
        {
            return AssetDatabase.IsValidFolder(SOURCE_ROOT.TrimEnd('/'));
        }

        /// <summary>
        /// 扫描当前项目的 YokiFrame 源码树并为需要更新的脚本写入图标。
        /// 此方法只由用户显式菜单调用，避免导入回调再次触发资源导入。
        /// </summary>
        private static void ApplyExistingScriptIcons()
        {
            var sourceRoot = SOURCE_ROOT.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(sourceRoot))
            {
                return;
            }

            var scriptGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { sourceRoot });
            for (var index = 0; index < scriptGuids.Length; index++)
            {
                ApplyIconIfNeeded(AssetDatabase.GUIDToAssetPath(scriptGuids[index]));
            }
        }

        /// <summary>
        /// 根据源码脚本路径解析功能图标，并在 .meta 尚未引用该图标时保存 MonoImporter 设置。
        /// 调用方必须已限定为显式菜单，禁止从资源导入或域加载回调进入。
        /// </summary>
        /// <param name="assetPath">Unity 资源路径。</param>
        private static void ApplyIconIfNeeded(string assetPath)
        {
            var normalizedPath = assetPath.Replace('\\', '/');
            if (!normalizedPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || normalizedPath.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return;
            }

            var iconKey = ResolveIconKey(normalizedPath);
            if (iconKey == null)
            {
                return;
            }

            var iconPath = ResolveIconPath(iconKey);
            var iconGuid = AssetDatabase.AssetPathToGUID(iconPath);
            if (string.IsNullOrEmpty(iconGuid))
            {
                return;
            }

            var metaPath = normalizedPath + ".meta";
            if (File.Exists(metaPath)
                && File.ReadAllText(metaPath).IndexOf(
                    "icon: {fileID: 2800000, guid: " + iconGuid + ", type: 3}",
                    StringComparison.Ordinal) >= 0)
            {
                return;
            }

            var importer = AssetImporter.GetAtPath(normalizedPath) as MonoImporter;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (importer == null || icon == null)
            {
                return;
            }

            importer.SetIcon(icon);
            importer.SaveAndReimport();
        }

        /// <summary>
        /// 返回源码脚本所属功能域；测试代码、安装包和未登记目录保持 Unity 默认图标。
        /// </summary>
        /// <param name="assetPath">规范化后的 Unity 资源路径。</param>
        /// <returns>图标文件名，不匹配时返回 <see langword="null"/>。</returns>
        private static string ResolveIconKey(string assetPath)
        {
            if (!assetPath.StartsWith(SOURCE_ROOT, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var relativePath = assetPath.Substring(SOURCE_ROOT.Length);
            foreach (var kitRoot in sKitRoots)
            {
                if (relativePath.StartsWith(kitRoot.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kitRoot.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// 返回源码开发目录内指定功能图标的 AssetDatabase 路径。
        /// </summary>
        /// <param name="iconKey">已解析的功能图标键。</param>
        /// <returns>图标资源路径。</returns>
        private static string ResolveIconPath(string iconKey)
        {
            return SOURCE_ROOT + ICON_RELATIVE_ROOT + iconKey + ".png";
        }
    }
}
#endif
