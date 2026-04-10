#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Immutable registration info for a discovered tool page.
    /// </summary>
    public readonly struct YokiPageInfo
    {
        /// <summary>
        /// Page implementation type.
        /// </summary>
        public Type PageType { get; }

        /// <summary>
        /// Declarative page metadata from <see cref="YokiToolPageAttribute"/>.
        /// </summary>
        public YokiToolPageAttribute Attribute { get; }

        /// <summary>
        /// Owning kit name.
        /// </summary>
        public string Kit => Attribute.Kit;

        /// <summary>
        /// Display name shown in the tools window.
        /// </summary>
        public string Name => Attribute.Name;

        /// <summary>
        /// Icon id used by the page.
        /// </summary>
        public string Icon => Attribute.Icon;

        /// <summary>
        /// Sorting priority, lower values appear first.
        /// </summary>
        public int Priority => Attribute.Priority;

        /// <summary>
        /// Page category.
        /// </summary>
        public YokiPageCategory Category => Attribute.Category;

        /// <summary>
        /// Creates a page info record.
        /// </summary>
        public YokiPageInfo(Type pageType, YokiToolPageAttribute attribute)
        {
            PageType = pageType;
            Attribute = attribute;
        }
    }

    /// <summary>
    /// Registry that discovers and instantiates YokiFrame tool pages.
    /// </summary>
    /// <remarks>
    /// The registry collects all types decorated with <see cref="YokiToolPageAttribute"/> through
    /// <see cref="TypeCache"/>, validates them against <see cref="IYokiToolPage"/>, sorts them by priority,
    /// and caches page instances for reuse.
    /// </remarks>
    public static class YokiToolPageRegistry
    {
        private static List<YokiPageInfo> sPageInfos;
        private static readonly Dictionary<Type, IYokiToolPage> sPageInstances = new();

        /// <summary>
        /// Gets all discovered page metadata in sorted order.
        /// </summary>
        public static IReadOnlyList<YokiPageInfo> PageInfos
        {
            get
            {
                if (sPageInfos == null)
                {
                    Collect();
                }

                return sPageInfos;
            }
        }

        /// <summary>
        /// Collects all page types marked with <see cref="YokiToolPageAttribute"/>.
        /// </summary>
        public static void Collect()
        {
            sPageInfos = new List<YokiPageInfo>(16);
            sPageInstances.Clear();

            var pageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var typesWithAttribute = TypeCache.GetTypesWithAttribute<YokiToolPageAttribute>();

            foreach (var type in typesWithAttribute)
            {
                try
                {
                    if (type.IsAbstract || type.IsInterface)
                    {
                        continue;
                    }

                    if (!typeof(IYokiToolPage).IsAssignableFrom(type))
                    {
                        Debug.LogWarning($"[YokiToolPageRegistry] {type.Name} is marked with [YokiToolPage] but does not implement IYokiToolPage.");
                        continue;
                    }

                    var attribute = (YokiToolPageAttribute)Attribute.GetCustomAttribute(type, typeof(YokiToolPageAttribute));
                    if (attribute == null)
                    {
                        continue;
                    }

                    string pageKey = $"{attribute.Category}|{attribute.Kit}|{attribute.Name}";
                    if (!pageKeys.Add(pageKey))
                    {
                        Debug.LogWarning($"[YokiToolPageRegistry] Duplicate page key skipped: {pageKey} ({type.FullName})");
                        continue;
                    }

                    sPageInfos.Add(new YokiPageInfo(type, attribute));
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[YokiToolPageRegistry] Failed to collect page type: {type.FullName}\n{ex}");
                }
            }

            sPageInfos.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        /// <summary>
        /// Gets or creates a page instance for the specified type.
        /// </summary>
        /// <param name="pageType">Concrete page type.</param>
        /// <returns>The cached or newly created page instance.</returns>
        public static IYokiToolPage GetOrCreatePage(Type pageType)
        {
            if (sPageInstances.TryGetValue(pageType, out var existing))
            {
                return existing;
            }

            try
            {
                var instance = (IYokiToolPage)Activator.CreateInstance(pageType);
                sPageInstances[pageType] = instance;
                return instance;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[YokiToolPageRegistry] Failed to create page instance: {pageType.FullName}\n{ex}");
                return null;
            }
        }

        /// <summary>
        /// Gets or creates a page instance for the specified generic type.
        /// </summary>
        public static T GetOrCreatePage<T>() where T : class, IYokiToolPage
        {
            return GetOrCreatePage(typeof(T)) as T;
        }

        /// <summary>
        /// Enumerates pages by category.
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
        /// Enumerates pages by kit name.
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
        /// Clears registry caches so page metadata can be rebuilt.
        /// </summary>
        public static void ClearCache()
        {
            sPageInfos = null;
            sPageInstances.Clear();
        }
    }
}
#endif
