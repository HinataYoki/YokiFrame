#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>把旧生成文件的处理并入现有生成事务，避免用户额外执行扫描或清理命令。</summary>
    internal static class UIKitGeneratedCodeMigration
    {
        private const string DESIGNER_SUFFIX = ".Designer.cs";
        private const string PANEL_FOLDER = "Panel";
        private const string COMPONENT_FOLDER = "Component";
        private const string ELEMENT_FOLDER = "Element";

        /// <summary>准备旧布局迁移和当前作用域中可安全清理的遗留文件。</summary>
        internal static void Prepare(UIKitPanelCodeLayout layout, List<UIKitBindNode> nodes,
            UIKitGeneratedOwnerKind? rootKind, string rootTypeName,
            Dictionary<string, string> relocations, List<string> deletions)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (relocations == null) throw new ArgumentNullException(nameof(relocations));
            if (deletions == null) throw new ArgumentNullException(nameof(deletions));
            HashSet<string> expected = new(StringComparer.OrdinalIgnoreCase);
            if (rootKind == null)
            {
                expected.Add(layout.PanelScriptPath);
                expected.Add(layout.PanelDesignerPath);
            }
            else
            {
                string userPath = rootKind == UIKitGeneratedOwnerKind.Element
                    ? layout.GetElementPath(rootTypeName, false)
                    : layout.GetComponentPath(rootTypeName, false);
                expected.Add(userPath);
                expected.Add(rootKind == UIKitGeneratedOwnerKind.Element
                    ? layout.GetElementPath(rootTypeName, true)
                    : layout.GetComponentPath(rootTypeName, true));
            }
            CollectExpected(layout, nodes, expected);
            CollectLegacyMoves(layout, expected, relocations);
            CollectStaleFiles(layout, expected, deletions);
        }

        /// <summary>递归收集当前绑定树需要的脚本路径。</summary>
        private static void CollectExpected(UIKitPanelCodeLayout layout, List<UIKitBindNode> nodes,
            HashSet<string> expected)
        {
            foreach (UIKitBindNode node in nodes)
            {
                UIKitBindOutputKind kind = node.Strategy.OutputKind;
                if (kind == UIKitBindOutputKind.Element || kind == UIKitBindOutputKind.Component)
                {
                    expected.Add(kind == UIKitBindOutputKind.Element
                        ? layout.GetElementPath(node.TypeName, false)
                        : layout.GetComponentPath(node.TypeName, false));
                    expected.Add(kind == UIKitBindOutputKind.Element
                        ? layout.GetElementPath(node.TypeName, true)
                        : layout.GetComponentPath(node.TypeName, true));
                    CollectExpected(kind == UIKitBindOutputKind.Component
                        ? layout.ForComponent(node.TypeName) : layout, node.Children, expected);
                }
                else
                {
                    CollectExpected(layout, node.Children, expected);
                }
            }
        }

        /// <summary>把旧 UIComponent/UIElement 文件登记到新目录，目标存在时交给所有权校验阻断。</summary>
        private static void CollectLegacyMoves(UIKitPanelCodeLayout layout, HashSet<string> expected,
            Dictionary<string, string> relocations)
        {
            foreach (string destination in expected)
            {
                if (!destination.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                string fileName = Path.GetFileName(destination);
                string legacy = destination;
                string normalized = destination.Replace('\\', '/');
                if (normalized.Contains("/" + COMPONENT_FOLDER + "/"))
                {
                    legacy = normalized.Contains("/" + ELEMENT_FOLDER + "/")
                        ? layout.ScriptFolder + "/UIComponent/" + layout.ElementComponentName + "/UIElement/" + fileName
                        : layout.ScriptFolder + "/UIComponent/" + fileName;
                }
                else if (normalized.Contains("/" + PANEL_FOLDER + "/") && normalized.Contains("/" + ELEMENT_FOLDER + "/"))
                {
                    legacy = layout.ScriptFolder + "/" + layout.PanelName + "/UIElement/" + fileName;
                }
                else if (string.Equals(destination, layout.PanelScriptPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(destination, layout.PanelDesignerPath, StringComparison.OrdinalIgnoreCase))
                {
                    legacy = layout.ScriptFolder + "/" + layout.PanelName + "/" + fileName;
                }
                MoveFileIfNeeded(legacy, destination, relocations);
            }
        }

        /// <summary>登记单文件移动，不覆盖目标文件或其 Unity 元数据。</summary>
        private static void MoveFileIfNeeded(string source, string destination,
            Dictionary<string, string> relocations)
        {
            source = source.Replace('\\', '/');
            destination = destination.Replace('\\', '/');
            if (source == destination || !File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(source))) return;
            if (File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(destination))
                || File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(destination + ".meta")))
                throw new InvalidOperationException("UIKit 生成目标已存在，不会覆盖旧代码: " + destination);
            if (relocations.TryGetValue(source, out string current))
            {
                if (!string.Equals(current, destination, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("UIKit 生成文件存在多个目标: " + source);
                return;
            }
            relocations.Add(source, destination);
        }

        /// <summary>扫描当前生成作用域，找出无使用、无引用且没有业务代码的旧 owner 文件。</summary>
        private static void CollectStaleFiles(UIKitPanelCodeLayout layout, HashSet<string> expected,
            List<string> deletions)
        {
            List<string> roots = new() { layout.PanelFolder, layout.ScriptFolder + "/" + COMPONENT_FOLDER };
            HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                string absolute = UIKitPanelCodeLayout.ToAbsolutePath(root);
                if (!Directory.Exists(absolute)) continue;
                foreach (string file in Directory.GetFiles(absolute, "*.cs", SearchOption.AllDirectories))
                {
                    string path = UIKitPanelCodeLayout.ToAssetPath(file);
                    if (!visited.Add(path) || expected.Contains(path)) continue;
                    if (!TryGetGeneratedPair(path, out string userPath, out string designerPath)) continue;
                    if (!File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(userPath)) || !IsSafeToDelete(userPath)) continue;
                    AddDeletion(userPath, deletions);
                    AddDeletion(designerPath, deletions);
                }
            }
        }

        /// <summary>将用户脚本和 Designer 配成一组，避免只删除一半生成结果。</summary>
        private static bool TryGetGeneratedPair(string path, out string userPath, out string designerPath)
        {
            string assetPath = UIKitPanelCodeLayout.ToAssetPath(path);
            userPath = assetPath.EndsWith(DESIGNER_SUFFIX, StringComparison.OrdinalIgnoreCase)
                ? assetPath.Substring(0, assetPath.Length - ".Designer".Length) : assetPath;
            designerPath = userPath.Substring(0, userPath.Length - ".cs".Length) + DESIGNER_SUFFIX;
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(userPath);
            if (script == default) return false;
            Type type = script.GetClass();
            return type != null && (typeof(UIElement).IsAssignableFrom(type) || typeof(UIComponent).IsAssignableFrom(type));
        }

        /// <summary>只允许删除模板空类，并确认类型没有被其它 Prefab 或源码使用。</summary>
        private static bool IsSafeToDelete(string userPath)
        {
            string source = File.ReadAllText(UIKitPanelCodeLayout.ToAbsolutePath(userPath));
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(userPath);
            Type type = script == default ? null : script.GetClass();
            if (type == null || HasUserCode(source, type.Name)) return false;
            string fullName = type.FullName;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = UIKitPanelCodeLayout.ToAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == default) continue;
                foreach (UIElement owner in prefab.GetComponentsInChildren<UIElement>(true))
                {
                    if (owner == default || owner.GetType().FullName != fullName) continue;
                    AbstractBind bind = owner.GetComponent<AbstractBind>();
                    if (bind != default && (bind.Bind == BindType.Element || bind.Bind == BindType.Component)) return false;
                }
            }
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!UIKitPanelCodeLayout.IsUnityAssetPath(path)) continue;
                string assetPath = UIKitPanelCodeLayout.ToAssetPath(path);
                if (!assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || assetPath == userPath
                    || assetPath.EndsWith(DESIGNER_SUFFIX, StringComparison.OrdinalIgnoreCase)) continue;
                string absolutePath = UIKitPanelCodeLayout.ToAbsolutePath(assetPath);
                if (!File.Exists(absolutePath)) continue;
                string candidate = File.ReadAllText(absolutePath);
                if (candidate.IndexOf(fullName, StringComparison.Ordinal) >= 0) return false;
            }
            return true;
        }

        /// <summary>判断用户脚本类体是否包含模板之外的业务成员。</summary>
        private static bool HasUserCode(string source, string typeName)
        {
            int declaration = source.IndexOf("class " + typeName, StringComparison.Ordinal);
            int open = declaration < 0 ? -1 : source.IndexOf('{', declaration);
            int close = source.LastIndexOf('}');
            if (open < 0 || close <= open) return true;
            string body = Regex.Replace(source.Substring(open + 1, close - open - 1), @"//.*$", string.Empty, RegexOptions.Multiline);
            body = Regex.Replace(body, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return !string.IsNullOrWhiteSpace(body);
        }

        /// <summary>登记待删除文件并去重。</summary>
        private static void AddDeletion(string path, List<string> deletions)
        {
            if (!File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(path))) return;
            for (var index = 0; index < deletions.Count; index++)
                if (string.Equals(deletions[index], path, StringComparison.OrdinalIgnoreCase)) return;
            deletions.Add(path);
        }
    }
}
#endif
