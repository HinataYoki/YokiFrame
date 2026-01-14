#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 页面注册信息
    /// </summary>
    public readonly struct YokiPageInfo
    {
        public Type PageType { get; }
        public YokiToolPageAttribute Attribute { get; }

        public string Kit => Attribute.Kit;
        public string Name => Attribute.Name;
        public string Icon => Attribute.Icon;
        public int Priority => Attribute.Priority;
        public YokiPageCategory Category => Attribute.Category;

        public YokiPageInfo(Type pageType, YokiToolPageAttribute attribute)
        {
            PageType = pageType;
            Attribute = attribute;
        }
    }

    /// <summary>
    /// YokiFrame 工具页面注册表
    /// 
    /// 使用 TypeCache 收集所有带 [YokiToolPage] 特性的页面类型。
    /// 提供页面发现、排序、过滤、实例化功能。
    /// 
    /// 依赖方向: Window → Registry → Page
    /// </summary>
    public static class YokiToolPageRegistry
    {
        private static List<YokiPageInfo> sPageInfos;
        private static readonly Dictionary<Type, IYokiToolPage> sPageInstances = new();

        /// <summary>
        /// 获取所有已注册的页面信息（已排序）
        /// </summary>
        public static IReadOnlyList<YokiPageInfo> PageInfos
        {
            get
            {
                if (sPageInfos == default)
                {
                    Collect();
                }
                return sPageInfos;
            }
        }

        /// <summary>
        /// 收集所有带 [YokiToolPage] 特性的页面
        /// </summary>
        public static void Collect()
        {
            sPageInfos = new List<YokiPageInfo>(16);
            sPageInstances.Clear();

            // 使用 TypeCache 高效收集（Unity 2019.2+）
            var typesWithAttribute = TypeCache.GetTypesWithAttribute<YokiToolPageAttribute>();

            foreach (var type in typesWithAttribute)
            {
                // 跳过抽象类和接口
                if (type.IsAbstract || type.IsInterface)
                    continue;

                // 必须实现 IYokiToolPage
                if (!typeof(IYokiToolPage).IsAssignableFrom(type))
                {
                    Debug.LogWarning($"[YokiToolPageRegistry] {type.Name} 标记了 [YokiToolPage] 但未实现 IYokiToolPage");
                    continue;
                }

                var attribute = (YokiToolPageAttribute)Attribute.GetCustomAttribute(type, typeof(YokiToolPageAttribute));
                if (attribute != default)
                {
                    sPageInfos.Add(new YokiPageInfo(type, attribute));
                }
            }

            // 按优先级排序
            sPageInfos.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        /// <summary>
        /// 获取或创建页面实例
        /// </summary>
        public static IYokiToolPage GetOrCreatePage(Type pageType)
        {
            if (sPageInstances.TryGetValue(pageType, out var existing))
            {
                return existing;
            }

            var instance = (IYokiToolPage)Activator.CreateInstance(pageType);
            sPageInstances[pageType] = instance;
            return instance;
        }

        /// <summary>
        /// 获取或创建页面实例
        /// </summary>
        public static T GetOrCreatePage<T>() where T : class, IYokiToolPage
        {
            return GetOrCreatePage(typeof(T)) as T;
        }

        /// <summary>
        /// 按分类筛选页面
        /// </summary>
        public static IEnumerable<YokiPageInfo> GetPagesByCategory(YokiPageCategory category)
        {
            foreach (var info in PageInfos)
            {
                if (info.Category == category)
                {
                    yield return info;
                }
            }
        }

        /// <summary>
        /// 按 Kit 名称筛选页面
        /// </summary>
        public static IEnumerable<YokiPageInfo> GetPagesByKit(string kit)
        {
            foreach (var info in PageInfos)
            {
                if (string.Equals(info.Kit, kit, StringComparison.OrdinalIgnoreCase))
                {
                    yield return info;
                }
            }
        }

        /// <summary>
        /// 清除缓存（用于热重载）
        /// </summary>
        public static void ClearCache()
        {
            sPageInfos = default;
            sPageInstances.Clear();
        }
    }
}
#endif
