#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    internal static partial class UIKitPanelCodeGenerator
    {
        /// <summary>为尚未编译的 Bind owner 生成完整用户脚本和 Designer；复用递归生成与冲突校验。</summary>
        internal static Dictionary<string, string> BuildBindSources(UIKitPanelCodeLayout layout, AbstractBind bind,
            Dictionary<string, string> relocations = null)
        {
            UIKitGeneratedOwnerKind kind = bind.Bind == BindType.Element
                ? UIKitGeneratedOwnerKind.Element : UIKitGeneratedOwnerKind.Component;
            UIKitBindScanResult scan = UIKitBindScanner.ScanOwner(bind.gameObject, kind);
            if (scan.HasErrors) throw CreateDiagnosticException(scan);
            if (!UIKitBindStrategyRegistry.TryGet(bind, out IUIKitBindStrategy strategy, out string error)
                || !strategy.TryResolve(bind, out string typeName, out _, out error))
                throw new InvalidOperationException(error);
            CodeGenKit.RequireIdentifier(typeName, nameof(typeName));
            UIKitBindNode node = new(bind, strategy, bind.name, bind.Name, typeName, default, 0);
            node.Children.AddRange(scan.Nodes);
            relocations ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> expected = UIKitGeneratedCodeMigration.Prepare(layout, scan.Nodes, kind, typeName, relocations);
            UIKitGeneratedCodeCleanup.ConfirmAndDelete(GetCleanupRoot(layout, kind, typeName), expected, !Application.isBatchMode);
            ValidateNodeOwnership(layout, new List<UIKitBindNode> { node }, relocations);
            Dictionary<string, string> sources = new(StringComparer.OrdinalIgnoreCase);
            AddNodeSources(layout, new List<UIKitBindNode> { node }, sources,
                UIKitCodeTemplateRegistry.Require(layout.CodeTemplate));
            return sources;
        }

        /// <summary>把扫描错误转换为包含节点路径的异常，在提交任何生成文件前阻止错误绑定。</summary>
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
