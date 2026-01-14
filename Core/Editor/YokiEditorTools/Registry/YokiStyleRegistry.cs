#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 样式注册信息
    /// </summary>
    public readonly struct YokiStyleInfo
    {
        public string Kit { get; }
        public string StyleSheetPath { get; }
        public int Priority { get; }

        public YokiStyleInfo(string kit, string styleSheetPath, int priority)
        {
            Kit = kit;
            StyleSheetPath = styleSheetPath;
            Priority = priority;
        }
    }

    /// <summary>
    /// YokiFrame 样式注册表
    /// 
    /// 收集所有带 [YokiEditorStyle] 特性的样式声明。
    /// 提供 Kit → StyleSheets 的映射关系。
    /// 
    /// 依赖方向: StyleService → StyleRegistry
    /// </summary>
    public static class YokiStyleRegistry
    {
        private static Dictionary<string, List<YokiStyleInfo>> sKitStyles;
        private static List<YokiStyleInfo> sAllStyles;

        /// <summary>
        /// 获取所有已注册的样式信息
        /// </summary>
        public static IReadOnlyList<YokiStyleInfo> AllStyles
        {
            get
            {
                if (sAllStyles == default)
                {
                    Collect();
                }
                return sAllStyles;
            }
        }

        /// <summary>
        /// 收集所有带 [YokiEditorStyle] 特性的样式声明
        /// </summary>
        public static void Collect()
        {
            sKitStyles = new Dictionary<string, List<YokiStyleInfo>>(16);
            sAllStyles = new List<YokiStyleInfo>(32);

            // 扫描所有程序集的 assembly 级别特性
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var attributes = assembly.GetCustomAttributes(typeof(YokiEditorStyleAttribute), false);
                    foreach (YokiEditorStyleAttribute attr in attributes)
                    {
                        var info = new YokiStyleInfo(attr.Kit, attr.StyleSheetPath, attr.Priority);
                        sAllStyles.Add(info);

                        if (!sKitStyles.TryGetValue(attr.Kit, out var list))
                        {
                            list = new List<YokiStyleInfo>(4);
                            sKitStyles[attr.Kit] = list;
                        }
                        list.Add(info);
                    }
                }
                catch
                {
                    // 忽略无法加载的程序集
                }
            }

            // 按优先级排序
            sAllStyles.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            foreach (var list in sKitStyles.Values)
            {
                list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }

            // Debug.Log($"[YokiStyleRegistry] 收集到 {sAllStyles.Count} 个样式声明，涉及 {sKitStyles.Count} 个 Kit");
        }

        /// <summary>
        /// 获取指定 Kit 的所有样式信息
        /// </summary>
        public static IReadOnlyList<YokiStyleInfo> GetStylesForKit(string kit)
        {
            if (sKitStyles == default)
            {
                Collect();
            }

            if (sKitStyles.TryGetValue(kit, out var list))
            {
                return list;
            }
            return Array.Empty<YokiStyleInfo>();
        }

        /// <summary>
        /// 获取所有已注册的 Kit 名称
        /// </summary>
        public static IEnumerable<string> GetRegisteredKits()
        {
            if (sKitStyles == default)
            {
                Collect();
            }
            return sKitStyles.Keys;
        }

        /// <summary>
        /// 清除缓存（用于热重载）
        /// </summary>
        public static void ClearCache()
        {
            sKitStyles = default;
            sAllStyles = default;
        }
    }
}
#endif
