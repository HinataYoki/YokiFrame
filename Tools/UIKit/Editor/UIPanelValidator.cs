#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UI 面板配置验证器 - 检测缺失绑定、断开引用、Canvas 配置问题
    /// </summary>
    public static partial class UIPanelValidator
    {
        #region 数据结构

        /// <summary>
        /// 验证结果
        /// </summary>
        public class ValidationResult
        {
            public GameObject Target;
            public readonly List<ValidationIssue> Issues = new(8);
            public bool HasErrors => GetErrorCount() > 0;
            public bool HasWarnings => GetWarningCount() > 0;
            
            public int GetErrorCount()
            {
                var count = 0;
                for (int i = 0; i < Issues.Count; i++)
                {
                    if (Issues[i].Severity == IssueSeverity.Error) count++;
                }
                return count;
            }
            
            public int GetWarningCount()
            {
                var count = 0;
                for (int i = 0; i < Issues.Count; i++)
                {
                    if (Issues[i].Severity == IssueSeverity.Warning) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 验证问题
        /// </summary>
        public class ValidationIssue
        {
            public IssueSeverity Severity;
            public IssueCategory Category;
            public string Message;
            public Object Context;
            public string FixSuggestion;
        }

        /// <summary>
        /// 问题严重程度
        /// </summary>
        public enum IssueSeverity
        {
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// 问题类别
        /// </summary>
        public enum IssueCategory
        {
            Binding,
            Reference,
            Canvas,
            Animation,
            Focus,
            Other
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 验证面板配置
        /// </summary>
        public static ValidationResult ValidatePanel(GameObject panelRoot)
        {
            var result = new ValidationResult { Target = panelRoot };
            
            if (panelRoot == null)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = IssueCategory.Other,
                    Message = "面板根对象为空"
                });
                return result;
            }
            
            // 执行各项验证
            ValidateBindings(panelRoot, result);
            ValidateReferences(panelRoot, result);
            ValidateCanvasConfiguration(panelRoot, result);
            ValidateAnimationConfiguration(panelRoot, result);
            ValidateFocusConfiguration(panelRoot, result);
            
            return result;
        }

        /// <summary>
        /// 验证场景中所有面板
        /// </summary>
        public static List<ValidationResult> ValidateAllPanelsInScene()
        {
            var results = new List<ValidationResult>();
            var panels = Object.FindObjectsByType<UIPanel>(FindObjectsSortMode.None);
            
            for (int i = 0; i < panels.Length; i++)
            {
                var result = ValidatePanel(panels[i].gameObject);
                if (result.Issues.Count > 0)
                {
                    results.Add(result);
                }
            }
            
            return results;
        }

        /// <summary>
        /// 验证预制体
        /// </summary>
        public static ValidationResult ValidatePrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                var result = new ValidationResult();
                result.Issues.Add(new ValidationIssue
                {
                    Severity = IssueSeverity.Error,
                    Category = IssueCategory.Other,
                    Message = $"无法加载预制体: {prefabPath}"
                });
                return result;
            }
            
            return ValidatePanel(prefab);
        }

        #endregion

        #region 辅助方法

        private static string GetPath(Transform target, Transform root)
        {
            if (target == null || root == null) return "";
            if (target == root) return target.name;
            
            var path = target.name;
            var current = target.parent;
            
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }

        private static string GetShortTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "";
            
            var lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
        }

        #endregion
    }
}
#endif
