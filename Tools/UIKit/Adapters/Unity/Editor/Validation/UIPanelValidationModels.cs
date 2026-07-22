#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>UIKit 面板校验问题的严重度。</summary>
    public enum UIPanelValidationSeverity
    {
        /// <summary>仅用于提示，不影响生成。</summary>
        Info,

        /// <summary>建议修复但不一定阻断生成。</summary>
        Warning,

        /// <summary>配置无法按预期工作。</summary>
        Error
    }

    /// <summary>UIKit 面板校验问题所属类别。</summary>
    public enum UIPanelValidationCategory
    {
        /// <summary>Bind 与代码生成。</summary>
        Binding,

        /// <summary>资源或事件引用。</summary>
        Reference,

        /// <summary>Canvas 与 Raycast。</summary>
        Canvas,

        /// <summary>显示/隐藏动画。</summary>
        Animation,

        /// <summary>焦点与导航。</summary>
        Focus,

        /// <summary>其它结构问题。</summary>
        Other
    }

    /// <summary>描述一条可定位的 UIKit 面板校验问题。</summary>
    public sealed class UIPanelValidationIssue
    {
        /// <summary>创建校验问题。</summary>
        /// <param name="severity">问题严重度。</param>
        /// <param name="category">问题类别。</param>
        /// <param name="message">用户可见说明。</param>
        /// <param name="context">可选 Unity 对象上下文。</param>
        /// <param name="path">Prefab 层级路径。</param>
        /// <param name="fixSuggestion">修复建议。</param>
        public UIPanelValidationIssue(
            UIPanelValidationSeverity severity,
            UIPanelValidationCategory category,
            string message,
            Object context = null,
            string path = "",
            string fixSuggestion = "")
        {
            Severity = severity;
            Category = category;
            Message = message ?? string.Empty;
            Context = context;
            Path = path ?? string.Empty;
            FixSuggestion = fixSuggestion ?? string.Empty;
        }

        /// <summary>获取问题严重度。</summary>
        public UIPanelValidationSeverity Severity { get; }

        /// <summary>获取问题类别。</summary>
        public UIPanelValidationCategory Category { get; }

        /// <summary>获取用户可见说明。</summary>
        public string Message { get; }

        /// <summary>获取可定位 Unity 对象。</summary>
        public Object Context { get; }

        /// <summary>获取 Prefab 层级路径。</summary>
        public string Path { get; }

        /// <summary>获取修复建议。</summary>
        public string FixSuggestion { get; }
    }

    /// <summary>保存一次 UIKit 面板校验的完整结果。</summary>
    public sealed class UIPanelValidationResult
    {
        /// <summary>创建指定目标的空校验结果。</summary>
        /// <param name="target">校验目标。</param>
        public UIPanelValidationResult(GameObject target)
        {
            Target = target;
        }

        /// <summary>获取校验目标。</summary>
        public GameObject Target { get; }

        /// <summary>获取全部问题，顺序与规则执行顺序一致。</summary>
        public List<UIPanelValidationIssue> Issues { get; } = new();

        /// <summary>获取是否包含错误。</summary>
        public bool HasErrors => Count(UIPanelValidationSeverity.Error) > 0;

        /// <summary>获取是否包含警告。</summary>
        public bool HasWarnings => Count(UIPanelValidationSeverity.Warning) > 0;

        /// <summary>统计指定严重度问题数量。</summary>
        /// <param name="severity">目标严重度。</param>
        /// <returns>问题数量。</returns>
        public int Count(UIPanelValidationSeverity severity)
        {
            int count = 0;
            for (var index = 0; index < Issues.Count; index++)
            {
                if (Issues[index].Severity == severity) count++;
            }

            return count;
        }
    }
}
#endif
