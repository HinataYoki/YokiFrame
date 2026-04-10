#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Provider implemented by kits or core modules that contribute documentation modules.
    /// </summary>
    public interface IDocumentationModuleProvider
    {
        /// <summary>
        /// Returns documentation modules owned by the current provider.
        /// </summary>
        IEnumerable<DocModule> GetModules();
    }

    /// <summary>
    /// Registry that discovers documentation modules contributed by Core and kit editor code.
    /// </summary>
    internal static class DocumentationModuleRegistry
    {
        private static List<DocModule> sModules;

        /// <summary>
        /// Gets all registered documentation modules in sorted order.
        /// </summary>
        public static IReadOnlyList<DocModule> Modules
        {
            get
            {
                if (sModules == null)
                {
                    Collect();
                }

                return sModules;
            }
        }

        /// <summary>
        /// Rebuilds the documentation module registry.
        /// </summary>
        public static void Collect()
        {
            sModules = new List<DocModule>(32);
            var moduleKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var providerTypes = TypeCache.GetTypesDerivedFrom<IDocumentationModuleProvider>();

            foreach (var providerType in providerTypes)
            {
                if (providerType.IsAbstract || providerType.IsInterface)
                {
                    continue;
                }

                try
                {
                    if (Activator.CreateInstance(providerType) is not IDocumentationModuleProvider provider)
                    {
                        continue;
                    }

                    var modules = provider.GetModules();
                    if (modules == null)
                    {
                        continue;
                    }

                    foreach (var module in modules)
                    {
                        if (module == null || string.IsNullOrEmpty(module.Name))
                        {
                            continue;
                        }

                        string moduleKey = $"{module.Category}|{module.Name}";
                        if (!moduleKeys.Add(moduleKey))
                        {
                            continue;
                        }

                        sModules.Add(module);
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[DocumentationModuleRegistry] Failed to load provider '{providerType.FullName}': {ex}");
                }
            }

            sModules.Sort(DocModuleComparer.Instance);
        }

        /// <summary>
        /// Clears cached module data.
        /// </summary>
        public static void ClearCache()
        {
            sModules = null;
        }
    }

    internal sealed class DocModuleComparer : IComparer<DocModule>
    {
        public static readonly DocModuleComparer Instance = new();

        public int Compare(DocModule x, DocModule y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return 1;
            if (y == null) return -1;

            int categoryCompare = GetCategoryOrder(x.Category).CompareTo(GetCategoryOrder(y.Category));
            if (categoryCompare != 0) return categoryCompare;

            return string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetCategoryOrder(string category)
        {
            return category switch
            {
                "CORE" => 0,
                "CORE KIT" => 1,
                "TOOLS" => 2,
                _ => 99,
            };
        }
    }

    /// <summary>
    /// Documentation module metadata and content container.
    /// </summary>
    public class DocModule
    {
        public string Name;
        public string Icon;
        public string Category;
        public string Description;
        public List<string> Keywords = new();
        public List<DocSection> Sections = new();
    }

    /// <summary>
    /// One documentation section inside a module.
    /// </summary>
    public class DocSection
    {
        public string Title;
        public string Description;
        public List<CodeExample> CodeExamples = new();
    }

    /// <summary>
    /// Code example entry displayed inside a documentation section.
    /// </summary>
    public class CodeExample
    {
        public string Title;
        public string Code;
        public string Explanation;
    }
}
#endif
