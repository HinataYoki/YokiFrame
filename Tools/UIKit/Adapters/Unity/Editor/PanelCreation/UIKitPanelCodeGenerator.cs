#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 使用 CodeGenKit 生成 UIKit 用户脚本和 Designer partial，并保证用户脚本不被覆盖。
    /// </summary>
    internal static class UIKitPanelCodeGenerator
    {
        /// <summary>
        /// 生成当前布局需要的全部源码；本阶段只在内存中构建，不写入磁盘。
        /// </summary>
        internal static Dictionary<string, string> BuildSources(
            UIKitPanelCodeLayout layout,
            UIKitBindScanResult scan)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (scan == null) throw new ArgumentNullException(nameof(scan));
            if (scan.HasErrors) throw CreateDiagnosticException(scan);

            IUIKitCodeTemplate template = UIKitCodeTemplateRegistry.Require(layout.CodeTemplate);
            Dictionary<string, string> sources = new(StringComparer.OrdinalIgnoreCase);
            AddIfMissing(
                sources,
                layout.PanelScriptPath,
                ApplyTemplate(
                    template,
                    UIKitCodeTemplatePart.PanelUser,
                    CreateTemplateContext(layout, layout.PanelName, "Panel", string.Empty),
                    BuildPanelUserSource(layout)));
            AddSource(
                sources,
                layout.PanelDesignerPath,
                ApplyTemplate(
                    template,
                    UIKitCodeTemplatePart.PanelDesigner,
                    CreateTemplateContext(layout, layout.PanelName, "Panel", string.Empty),
                    BuildPanelDesignerSource(layout, scan.Nodes)));
            AddNodeSources(layout, scan.Nodes, sources, template);
            return sources;
        }

        /// <summary>
        /// 只生成当前 UIElement/UIComponent owner Designer，并递归补齐其内部生成类型。
        /// </summary>
        internal static Dictionary<string, string> BuildOwnerSources(
            UIKitPanelCodeLayout layout,
            UIKitBindScanResult scan,
            UIKitGeneratedOwnerKind ownerKind,
            Type ownerType,
            string designerPath)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (scan == null) throw new ArgumentNullException(nameof(scan));
            if (scan.HasErrors) throw CreateDiagnosticException(scan);
            ValidateGeneratedOwnerType(ownerKind, ownerType);
            string assetPath = RequireDesignerPath(designerPath);
            string namespaceName = CodeGenKit.RequireQualifiedName(
                ownerType.Namespace,
                nameof(ownerType.Namespace));
            string typeName = CodeGenKit.RequireIdentifier(ownerType.Name, nameof(ownerType.Name));

            IUIKitCodeTemplate template = UIKitCodeTemplateRegistry.Require(layout.CodeTemplate);
            Dictionary<string, string> sources = new(StringComparer.OrdinalIgnoreCase);
            AddSource(
                sources,
                assetPath,
                ApplyTemplate(
                    template,
                    UIKitCodeTemplatePart.BindingDesigner,
                    CreateTemplateContext(layout, typeName, ownerKind.ToString(), ownerKind.ToString()),
                    BuildBindingOwnerDesignerSource(layout, namespaceName, typeName, scan.Nodes)));
            AddNodeSources(layout, scan.Nodes, sources, template);
            return sources;
        }

        /// <summary>以文件集事务提交生成源码，任一文件失败时恢复本次已修改文件。</summary>
        internal static bool CommitSources(
            Dictionary<string, string> sources)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            List<SourceFileSnapshot> snapshots = CaptureSnapshots(sources);
            bool changed = false;
            try
            {
                for (var index = 0; index < snapshots.Count; index++)
                {
                    SourceFileSnapshot snapshot = snapshots[index];
                    CodeGenerationFileResult result = CodeGenKit.WriteTextToFile(snapshot.AbsolutePath, snapshot.Source);
                    changed |= result != CodeGenerationFileResult.Unchanged;
                }

                return changed;
            }
            catch (Exception exception)
            {
                try
                {
                    RestoreSnapshots(snapshots);
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException("UIKit 代码生成失败且回滚也失败。", exception, rollbackException);
                }

                throw;
            }
        }

        /// <summary>按稳定路径顺序读取本次提交涉及的文件旧内容。</summary>
        private static List<SourceFileSnapshot> CaptureSnapshots(Dictionary<string, string> sources)
        {
            List<string> paths = new(sources.Keys);
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            List<SourceFileSnapshot> snapshots = new(paths.Count);
            for (var index = 0; index < paths.Count; index++)
            {
                string assetPath = paths[index];
                string absolutePath = UIKitPanelCodeLayout.ToAbsolutePath(assetPath);
                bool existed = File.Exists(absolutePath);
                snapshots.Add(new SourceFileSnapshot(
                    absolutePath,
                    sources[assetPath],
                    existed,
                    existed ? File.ReadAllText(absolutePath) : string.Empty));
            }

            return snapshots;
        }

        /// <summary>恢复文件集事务前的存在状态和原始文本。</summary>
        private static void RestoreSnapshots(List<SourceFileSnapshot> snapshots)
        {
            for (var index = snapshots.Count - 1; index >= 0; index--)
            {
                SourceFileSnapshot snapshot = snapshots[index];
                if (snapshot.Existed)
                {
                    CodeGenKit.WriteTextToFile(snapshot.AbsolutePath, snapshot.OriginalSource);
                    continue;
                }

                if (File.Exists(snapshot.AbsolutePath)) File.Delete(snapshot.AbsolutePath);
            }
        }

        /// <summary>记录一个待提交源码文件的旧状态和新文本。</summary>
        private sealed class SourceFileSnapshot
        {
            /// <summary>创建文件事务快照。</summary>
            internal SourceFileSnapshot(string absolutePath, string source, bool existed, string originalSource)
            {
                AbsolutePath = absolutePath;
                Source = source;
                Existed = existed;
                OriginalSource = originalSource;
            }

            internal string AbsolutePath { get; }
            internal string Source { get; }
            internal bool Existed { get; }
            internal string OriginalSource { get; }
        }

        /// <summary>为每个 UIKit 生成文件统一导入 Unity UI 与框架常用命名空间。</summary>
        /// <param name="build">在标准 using 之后构建文件声明的回调。</param>
        /// <returns>使用稳定换行和缩进生成的完整 C# 源码。</returns>
        private static string BuildSource(Action<RootCode> build)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            return CodeGenKit.GenerateToString(root =>
            {
                root.Using("UnityEngine")
                    .Using("UnityEngine.UI")
                    .Using("YokiFrame")
                    .EmptyLine();
                build(root);
            });
        }

        /// <summary>生成面板用户脚本，包含数据类型和最小生命周期模板。</summary>
        private static string BuildPanelUserSource(UIKitPanelCodeLayout layout)
        {
            return BuildSource(root => root.Namespace(layout.ScriptNamespace, scope =>
            {
                scope.Class(layout.PanelName + "Data", "IUIData", false, false, data =>
                    data.AsSealed().WithAttribute("System.Serializable"));
                scope.EmptyLine();
                scope.Class(layout.PanelName, "UIPanel", true, false, panel =>
                {
                    panel.ProtectedOverrideVoid("OnInit", method => method
                        .WithParameter("IUIData", "data", "null")
                        .WithBody(body => body.Custom(
                            "mData = data as " + layout.PanelName + "Data ?? new " + layout.PanelName + "Data();")));
                    if (!string.Equals(
                            layout.CodeTemplate,
                            UIKitPanelGenerationRequest.MINIMAL_TEMPLATE,
                            StringComparison.Ordinal))
                    {
                        panel.EmptyLine();
                        panel.ProtectedOverrideVoid("OnOpen", method => method
                            .WithParameter("IUIData", "data", "null")
                            .WithBody(body => body.Custom(
                                "mData = data as " + layout.PanelName + "Data ?? mData;")));
                        panel.EmptyLine();
                        panel.ProtectedOverrideVoid("OnShow", default);
                        panel.EmptyLine();
                        panel.ProtectedOverrideVoid("OnHide", default);
                    }

                    panel.EmptyLine();
                    panel.ProtectedOverrideVoid("OnClose", default);
                });
            }));
        }

        /// <summary>生成面板 Designer 字段、数据属性和清理钩子。</summary>
        private static string BuildPanelDesignerSource(
            UIKitPanelCodeLayout layout,
            List<UIKitBindNode> nodes)
        {
            return BuildSource(root => root.Namespace(layout.ScriptNamespace, scope =>
                scope.Class(layout.PanelName, default, true, false, panel =>
                {
                    AppendFields(panel, nodes, layout);
                    panel.Field(layout.PanelName + "Data", "mData", field =>
                        field.WithAccess(AccessModifier.Private).WithAttribute("SerializeField"));
                    panel.EmptyLine();
                    panel.Property(layout.PanelName + "Data", "Data", property => property
                        .WithAccess(AccessModifier.Public)
                        .WithExpressionBody("mData"));
                    panel.EmptyLine();
                    panel.ProtectedOverrideVoid("ClearUIComponents", method => method
                        .WithBody(body => AppendClearFields(body, nodes)));
                })));
        }

        /// <summary>递归生成 Element/Component 用户脚本和 Designer 文件。</summary>
        private static void AddNodeSources(
            UIKitPanelCodeLayout layout,
            List<UIKitBindNode> nodes,
            Dictionary<string, string> sources,
            IUIKitCodeTemplate template)
        {
            for (var index = 0; index < nodes.Count; index++)
            {
                UIKitBindNode node = nodes[index];
                if (node.Strategy.OutputKind == UIKitBindOutputKind.Element
                    || node.Strategy.OutputKind == UIKitBindOutputKind.Component)
                {
                    string userPath = node.Strategy.OutputKind == UIKitBindOutputKind.Element
                        ? layout.GetElementPath(node.TypeName, false)
                        : layout.GetComponentPath(node.TypeName, false);
                    string designerPath = node.Strategy.OutputKind == UIKitBindOutputKind.Element
                        ? layout.GetElementPath(node.TypeName, true)
                        : layout.GetComponentPath(node.TypeName, true);
                    AddIfMissing(
                        sources,
                        userPath,
                        ApplyTemplate(
                            template,
                            UIKitCodeTemplatePart.BindingUser,
                            CreateTemplateContext(
                                layout,
                                node.TypeName,
                                node.Strategy.OutputKind.ToString(),
                                node.Strategy.OutputKind.ToString()),
                            BuildGeneratedUserSource(layout, node)));
                    AddSource(
                        sources,
                        designerPath,
                        ApplyTemplate(
                            template,
                            UIKitCodeTemplatePart.BindingDesigner,
                            CreateTemplateContext(
                                layout,
                                node.TypeName,
                                node.Strategy.OutputKind.ToString(),
                                node.Strategy.OutputKind.ToString()),
                            BuildGeneratedDesignerSource(layout, node)));
                    AddNodeSources(layout, node.Children, sources, template);
                }
                else
                {
                    AddNodeSources(layout, node.Children, sources, template);
                }
            }
        }

        /// <summary>生成一个 Element 或 Component 的用户 partial。</summary>
        private static string BuildGeneratedUserSource(
            UIKitPanelCodeLayout layout,
            UIKitBindNode node)
        {
            string namespaceName = node.Strategy.OutputKind == UIKitBindOutputKind.Element
                ? layout.GetElementNamespace()
                : layout.ScriptNamespace;
            string parent = node.Strategy.OutputKind == UIKitBindOutputKind.Element
                ? "UIElement"
                : "UIComponent";
            return BuildSource(root => root.Namespace(namespaceName, scope =>
                scope.Class(node.TypeName, parent, true, false, default)));
        }

        /// <summary>生成一个 Element 或 Component 的 Designer partial。</summary>
        private static string BuildGeneratedDesignerSource(
            UIKitPanelCodeLayout layout,
            UIKitBindNode node)
        {
            string namespaceName = node.Strategy.OutputKind == UIKitBindOutputKind.Element
                ? layout.GetElementNamespace()
                : layout.ScriptNamespace;
            return BuildBindingOwnerDesignerSource(
                layout,
                namespaceName,
                node.TypeName,
                node.Children);
        }

        /// <summary>生成一个已经存在的绑定 owner partial Designer。</summary>
        private static string BuildBindingOwnerDesignerSource(
            UIKitPanelCodeLayout layout,
            string namespaceName,
            string typeName,
            List<UIKitBindNode> nodes)
        {
            return BuildSource(root => root.Namespace(namespaceName, scope =>
                scope.Class(typeName, default, true, false, generated =>
                {
                    AppendFields(generated, nodes, layout);
                    generated.EmptyLine();
                    generated.VoidMethod("Clear", method => method
                        .WithBody(body => AppendClearFields(body, nodes)));
                })));
        }

        /// <summary>创建不泄漏内部扫描节点的项目模板上下文。</summary>
        /// <param name="layout">当前代码布局。</param>
        /// <param name="ownerTypeName">当前文件 owner 类型名。</param>
        /// <param name="ownerKind">Panel、Element 或 Component。</param>
        /// <param name="bindingKind">当前绑定输出类型。</param>
        /// <returns>供模板转换器使用的公开上下文。</returns>
        private static UIKitCodeTemplateContext CreateTemplateContext(
            UIKitPanelCodeLayout layout,
            string ownerTypeName,
            string ownerKind,
            string bindingKind)
        {
            return new UIKitCodeTemplateContext(
                layout.PanelName,
                layout.ScriptNamespace,
                ownerTypeName,
                ownerKind,
                bindingKind);
        }

        /// <summary>调用项目模板转换源码并规范化换行，仍由外层事务负责落盘。</summary>
        /// <param name="template">当前模板。</param>
        /// <param name="part">源码文件角色。</param>
        /// <param name="context">模板上下文。</param>
        /// <param name="source">CodeGenKit 默认源码。</param>
        /// <returns>转换后的完整源码。</returns>
        private static string ApplyTemplate(
            IUIKitCodeTemplate template,
            UIKitCodeTemplatePart part,
            UIKitCodeTemplateContext context,
            string source)
        {
            string transformed = template.Transform(part, context, source);
            if (string.IsNullOrWhiteSpace(transformed))
            {
                throw new InvalidOperationException(
                    "UIKit 代码模板返回了空源码: " + template.Name + "/" + part);
            }

            return transformed.Replace("\r\n", "\n").Replace('\r', '\n');
        }

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

        /// <summary>验证独立 Designer 位于 Assets 且使用固定文件后缀。</summary>
        private static string RequireDesignerPath(string designerPath)
        {
            string normalized = string.IsNullOrWhiteSpace(designerPath)
                ? string.Empty
                : designerPath.Trim().Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal)
                || !normalized.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
                || normalized.IndexOf("../", StringComparison.Ordinal) >= 0)
                throw new ArgumentException("Designer 路径必须位于 Assets: " + designerPath);
            return normalized;
        }

        /// <summary>把节点字段写入 owner class，重复生成类型不创建第二个引用字段。</summary>
        private static void AppendFields(
            ICodeScope scope,
            List<UIKitBindNode> nodes,
            UIKitPanelCodeLayout layout)
        {
            for (var index = 0; index < nodes.Count; index++)
            {
                UIKitBindNode node = nodes[index];
                if (node.IsRepeated || node.Strategy.OutputKind == UIKitBindOutputKind.Marker)
                    continue;
                string typeName = GetFieldType(layout, node);
                if (string.IsNullOrWhiteSpace(typeName)) continue;
                scope.Field(typeName, node.FieldName, field => field.WithAccess(AccessModifier.Public));
            }
        }

        /// <summary>生成 owner 清理方法中的字段归零语句。</summary>
        private static void AppendClearFields(ICodeScope scope, List<UIKitBindNode> nodes)
        {
            for (var index = 0; index < nodes.Count; index++)
            {
                UIKitBindNode node = nodes[index];
                if (!node.IsRepeated && node.Strategy.OutputKind != UIKitBindOutputKind.Marker)
                    scope.Custom(node.FieldName + " = default;");
            }
        }

        /// <summary>按绑定输出形态计算 C# 字段类型表达式；项目命名空间使用普通限定名，不添加 global:: 前缀。</summary>
        private static string GetFieldType(UIKitPanelCodeLayout layout, UIKitBindNode node)
        {
            switch (node.Strategy.OutputKind)
            {
                case UIKitBindOutputKind.Member:
                    return node.TypeName;
                case UIKitBindOutputKind.Element:
                    return layout.GetElementNamespace() + "." + node.TypeName;
                case UIKitBindOutputKind.Component:
                    return layout.ScriptNamespace + "." + node.TypeName;
                default:
                    return string.Empty;
            }
        }

        /// <summary>只为不存在的用户脚本添加源，保留用户已有 partial 内容。</summary>
        private static void AddIfMissing(
            Dictionary<string, string> sources,
            string assetPath,
            string source)
        {
            if (!File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(assetPath)))
                AddSource(sources, assetPath, source);
        }

        /// <summary>添加生成源并拒绝同一路径产生不同内容。</summary>
        private static void AddSource(
            Dictionary<string, string> sources,
            string assetPath,
            string source)
        {
            if (sources.TryGetValue(assetPath, out string existing)
                && !string.Equals(existing, source, StringComparison.Ordinal))
                throw new InvalidOperationException("多个 Bind 生成类型映射到同一文件: " + assetPath);
            sources[assetPath] = source;
        }

        /// <summary>把扫描错误转换为包含路径的异常。</summary>
        private static InvalidOperationException CreateDiagnosticException(UIKitBindScanResult scan)
        {
            List<string> errors = new();
            for (var index = 0; index < scan.Diagnostics.Count; index++)
            {
                UIKitBindDiagnostic diagnostic = scan.Diagnostics[index];
                if (diagnostic.Severity == UIKitBindDiagnosticSeverity.Error)
                    errors.Add(diagnostic.Path + ": " + diagnostic.Message);
            }

            return new InvalidOperationException("UIKit Bind 扫描失败: " + string.Join(" | ", errors));
        }
    }
}
#endif
