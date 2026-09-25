#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace YokiFrame
{
    /// <summary>把可确认归属的旧布局移动并入现有生成事务；删除候选由独立确认流程处理。</summary>
    internal static class UIKitGeneratedCodeMigration
    {
        private const string PANEL_FOLDER = "Panel";
        private const string COMPONENT_FOLDER = "Component";
        private const string ELEMENT_FOLDER = "Element";

        /// <summary>准备旧布局迁移，并返回本次生成仍需要保留的脚本路径。</summary>
        internal static HashSet<string> Prepare(UIKitPanelCodeLayout layout, List<UIKitBindNode> nodes,
            UIKitGeneratedOwnerKind? rootKind, string rootTypeName,
            Dictionary<string, string> relocations)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (relocations == null) throw new ArgumentNullException(nameof(relocations));
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
            return expected;
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

        /// <summary>把当前期望文件对应的旧布局路径登记为移动，不按目录包含关系猜测其它同名文件。</summary>
        private static void CollectLegacyMoves(UIKitPanelCodeLayout layout, HashSet<string> expected,
            Dictionary<string, string> relocations)
        {
            foreach (string destination in expected)
            {
                if (!destination.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
                string fileName = Path.GetFileName(destination);
                string legacy = GetLegacyPath(layout, destination, fileName);
                if (string.IsNullOrEmpty(legacy)) continue;
                MoveFileIfNeeded(legacy, destination, relocations);
            }
        }

        /// <summary>按目标路径的精确目录段计算唯一旧路径，避免兄弟 Component 的同名 Element 被一起搬走。</summary>
        private static string GetLegacyPath(UIKitPanelCodeLayout layout, string destination, string fileName)
        {
            string normalized = destination.Replace('\\', '/');
            string componentRoot = layout.ScriptFolder + "/" + COMPONENT_FOLDER + "/";
            string panelRoot = layout.ScriptFolder + "/" + PANEL_FOLDER + "/" + layout.PanelName + "/";
            if (normalized.StartsWith(componentRoot, StringComparison.Ordinal))
            {
                string relative = normalized.Substring(componentRoot.Length);
                int separator = relative.IndexOf('/');
                string ownerName = separator < 0 ? Path.GetFileNameWithoutExtension(fileName) : relative.Substring(0, separator);
                bool element = separator >= 0 && relative.Substring(separator + 1).StartsWith(ELEMENT_FOLDER + "/", StringComparison.Ordinal);
                return element
                    ? layout.ScriptFolder + "/UIComponent/" + ownerName + "/UIElement/" + fileName
                    : layout.ScriptFolder + "/UIComponent/" + fileName;
            }

            if (normalized.StartsWith(panelRoot + ELEMENT_FOLDER + "/", StringComparison.Ordinal))
                return layout.ScriptFolder + "/" + layout.PanelName + "/UIElement/" + fileName;
            if (string.Equals(destination, layout.PanelScriptPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(destination, layout.PanelDesignerPath, StringComparison.OrdinalIgnoreCase))
                return layout.ScriptFolder + "/" + layout.PanelName + "/" + fileName;
            return string.Empty;
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

    }
}
#endif
