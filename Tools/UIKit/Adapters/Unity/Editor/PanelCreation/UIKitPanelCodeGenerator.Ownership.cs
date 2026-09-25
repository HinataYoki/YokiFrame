#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace YokiFrame
{
    internal static partial class UIKitPanelCodeGenerator
    {
        /// <summary>校验独立 owner 类型与生成 kind 一致。</summary>
        private static void ValidateGeneratedOwnerType(
            UIKitGeneratedOwnerKind ownerKind,
            Type ownerType)
        {
            if (ownerType == null) throw new ArgumentNullException(nameof(ownerType));
            bool valid = ownerKind == UIKitGeneratedOwnerKind.Element
                ? typeof(UIElement).IsAssignableFrom(ownerType)
                    && !typeof(UIComponent).IsAssignableFrom(ownerType)
                : ownerKind == UIKitGeneratedOwnerKind.Component
                    && typeof(UIComponent).IsAssignableFrom(ownerType);
            if (!valid || ownerType.IsAbstract)
                throw new ArgumentException("生成 owner 类型与 kind 不匹配: " + ownerType.FullName);
        }

        /// <summary>验证独立 Designer 使用项目相对路径和固定文件后缀。</summary>
        private static string RequireDesignerPath(string designerPath)
        {
            string normalized = string.IsNullOrWhiteSpace(designerPath)
                ? string.Empty
                : designerPath.Trim().Replace('\\', '/');
            if (!UIKitPanelCodeLayout.IsUnityAssetPath(normalized)
                || !normalized.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Designer 路径必须是项目 Assets 或 Packages 下的相对路径: " + designerPath);
            return normalized;
        }

        /// <summary>普通生成必须保留已挂载脚本的类型、程序集与路径；显式转换只允许预检清单中的移动。</summary>
        private static void ValidateNodeOwnership(UIKitPanelCodeLayout layout, List<UIKitBindNode> nodes,
            Dictionary<string, string> relocations = null)
        {
            foreach (UIKitBindNode node in nodes)
            {
                UIKitBindOutputKind kind = node.Strategy.OutputKind;
                if (kind != UIKitBindOutputKind.Element && kind != UIKitBindOutputKind.Component)
                {
                    ValidateNodeOwnership(layout, node.Children, relocations);
                    continue;
                }
                string expected = kind == UIKitBindOutputKind.Element
                    ? layout.GetElementPath(node.TypeName, false) : layout.GetComponentPath(node.TypeName, false);
                string fullName = layout.GetFullTypeName(kind, node.TypeName);
                UIKitMountedOwner.RequireSingle(node.Bind, out UIElement owner);
                if (owner != default)
                {
                    string actual = UIKitGeneratedOwnerCodeService.GetScriptPath(owner);
                    bool moving = relocations != null && relocations.TryGetValue(actual, out string target)
                        && string.Equals(target, expected, StringComparison.OrdinalIgnoreCase);
                    bool sameIdentity = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                        && owner.GetType().FullName == fullName
                        && owner.GetType().Assembly.GetName().Name == layout.AssemblyName
                        && (owner is UIComponent) == (kind == UIKitBindOutputKind.Component);
                    if (!moving && !sameIdentity)
                        throw new InvalidOperationException("绑定已持有另一脚本身份: " + actual
                            + "；节点上的旧脚本不能确认是本次类型，请先处理重复挂载或使用显式转换。");
                }
                string legacy = GetLegacyOwnerPath(layout, kind, node.TypeName);
                ValidateTypeLocation(fullName, layout.AssemblyName, expected, legacy, relocations);
                ValidateNodeOwnership(layout.ForChildren(node), node.Children, relocations);
            }
        }

        /// <summary>按当前作用域计算唯一旧路径，Component 子 Element 使用所属 Component 名而不是空的 Panel 名。</summary>
        private static string GetLegacyOwnerPath(UIKitPanelCodeLayout layout, UIKitBindOutputKind kind, string typeName)
        {
            string fileName = typeName + ".cs";
            if (kind == UIKitBindOutputKind.Component)
                return layout.ScriptFolder + "/UIComponent/" + fileName;
            string ownerName = layout.ElementComponentName.Length == 0 ? layout.PanelName : layout.ElementComponentName;
            string ownerFolder = layout.ElementComponentName.Length == 0
                ? layout.PanelName
                : "UIComponent/" + ownerName;
            return layout.ScriptFolder + "/" + ownerFolder + "/UIElement/" + fileName;
        }

        /// <summary>阻断旧文件和其它位置的同名编译类型，避免换目录后补出第二份用户 partial。</summary>
        private static void ValidateTypeLocation(string fullName, string assembly, string expected, string legacy,
            Dictionary<string, string> relocations = null)
        {
            bool legacyMoving = relocations != null
                && relocations.TryGetValue(legacy.Replace('\\', '/'), out string legacyDestination)
                && string.Equals(legacyDestination, expected, StringComparison.OrdinalIgnoreCase);
            if (!legacyMoving && (File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(legacy))
                || File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(legacy + ".meta"))))
                throw new InvalidOperationException("发现旧布局脚本，但无法确认其归属: " + legacy);
            MonoScript destinationScript = AssetDatabase.LoadAssetAtPath<MonoScript>(expected);
            Type destinationType = destinationScript == default ? null : destinationScript.GetClass();
            if (destinationType != null && (destinationType.FullName != fullName
                || destinationType.Assembly.GetName().Name != assembly))
                throw new InvalidOperationException("目标脚本由其它类型持有，不会补写不匹配的 Designer: " + expected);
            foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                Type type = script.GetClass();
                if (type == null || type.FullName != fullName) continue;
                string actual = AssetDatabase.GetAssetPath(script);
                bool moving = relocations != null && relocations.TryGetValue(actual, out string destination)
                    && string.Equals(destination, expected, StringComparison.OrdinalIgnoreCase);
                if (!moving && (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                    || type.Assembly.GetName().Name != assembly))
                    throw new InvalidOperationException("生成类型已存在于其它路径或程序集，不会重复生成: " + fullName + " -> " + actual);
            }
        }
    }
}
#endif
