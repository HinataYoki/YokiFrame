#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>表示一次 Bind 扫描诊断的严重度。</summary>
    internal enum UIKitBindDiagnosticSeverity
    {
        /// <summary>不阻断生成的提示。</summary>
        Warning,

        /// <summary>必须阻断全部生成与回填的错误。</summary>
        Error,
    }

    /// <summary>描述一个带层级路径的 Bind 扫描诊断。</summary>
    internal sealed class UIKitBindDiagnostic
    {
        /// <summary>创建一个稳定诊断。</summary>
        internal UIKitBindDiagnostic(
            UIKitBindDiagnosticSeverity severity,
            string path,
            string message)
        {
            Severity = severity;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>获取严重度。</summary>
        internal UIKitBindDiagnosticSeverity Severity { get; }

        /// <summary>获取 Prefab 层级路径。</summary>
        internal string Path { get; }

        /// <summary>获取诊断消息。</summary>
        internal string Message { get; }
    }

    /// <summary>表示一个已经解析但尚未执行任何写入的 Bind 节点。</summary>
    internal sealed class UIKitBindNode
    {
        /// <summary>创建一个确定性 Bind 节点。</summary>
        internal UIKitBindNode(
            AbstractBind bind,
            IUIKitBindStrategy strategy,
            string path,
            string fieldName,
            string typeName,
            Object target,
            int order)
        {
            Bind = bind;
            Strategy = strategy;
            Path = path;
            FieldName = fieldName;
            TypeName = typeName;
            Target = target;
            Order = order;
        }

        /// <summary>获取来源 Bind 组件。</summary>
        internal AbstractBind Bind { get; }

        /// <summary>获取解析策略。</summary>
        internal IUIKitBindStrategy Strategy { get; }

        /// <summary>获取 Prefab 层级路径。</summary>
        internal string Path { get; }

        /// <summary>获取 owner 中的字段名称。</summary>
        internal string FieldName { get; }

        /// <summary>获取字段或生成类类型名。</summary>
        internal string TypeName { get; }

        /// <summary>获取无需编译即可回填的显式对象引用。</summary>
        internal Object Target { get; }

        /// <summary>获取同一 owner 内的稳定遍历顺序。</summary>
        internal int Order { get; }

        /// <summary>获取当前节点是否为兼容重复生成类型。</summary>
        internal bool IsRepeated { get; set; }

        /// <summary>获取当前节点建立的子绑定作用域。</summary>
        internal List<UIKitBindNode> Children { get; } = new();
    }

    /// <summary>保存完整扫描树与阻断诊断。</summary>
    internal sealed class UIKitBindScanResult
    {
        /// <summary>创建一个空扫描结果。</summary>
        internal UIKitBindScanResult(string rootName)
        {
            RootName = rootName ?? string.Empty;
        }

        /// <summary>获取扫描根名称。</summary>
        internal string RootName { get; }

        /// <summary>获取根 owner 直接或扁平化后的 Bind 节点。</summary>
        internal List<UIKitBindNode> Nodes { get; } = new();

        /// <summary>获取全部诊断。</summary>
        internal List<UIKitBindDiagnostic> Diagnostics { get; } = new();

        /// <summary>获取当前结果是否包含阻断错误。</summary>
        internal bool HasErrors
        {
            get
            {
                for (var index = 0; index < Diagnostics.Count; index++)
                {
                    if (Diagnostics[index].Severity == UIKitBindDiagnosticSeverity.Error)
                        return true;
                }

                return false;
            }
        }
    }
}
#endif
