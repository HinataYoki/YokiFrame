#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// UIPanelInspector - 验证逻辑
    /// </summary>
    public partial class UIPanelInspector
    {
        /// <summary>
        /// 获取验证结果的提示文本
        /// </summary>
        private string GetValidationTooltip(BindTreeNode node)
        {
            if (node.ValidationResults == null || node.ValidationResults.Count == 0)
                return string.Empty;
            
            var sb = new System.Text.StringBuilder();
            foreach (var result in node.ValidationResults)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(result.Level == BindValidationLevel.Error ? "[错误] " : "[警告] ");
                sb.Append(result.Message);
            }
            return sb.ToString();
        }
        
        /// <summary>
        /// 更新验证结果摘要
        /// </summary>
        private void UpdateValidationSummary(GameObject root)
        {
            if (mValidationSummaryLabel == null) return;
            
            var results = BindService.ValidateBindings(root);
            
            int errorCount = 0;
            int warningCount = 0;
            
            foreach (var result in results)
            {
                if (result.Level == BindValidationLevel.Error)
                    errorCount++;
                else if (result.Level == BindValidationLevel.Warning)
                    warningCount++;
            }

            if (errorCount == 0 && warningCount == 0)
            {
                mValidationSummaryLabel.text = "所有绑定配置有效";
                mValidationSummaryLabel.style.backgroundColor = new StyleColor(new Color(0.2f, 0.35f, 0.2f, 0.5f));
                mValidationSummaryLabel.style.color = new StyleColor(new Color(0.6f, 0.9f, 0.6f));
                mValidationSummaryLabel.style.borderLeftColor = new StyleColor(COLOR_ELEMENT);
            }
            else
            {
                var parts = new List<string>(2);
                if (errorCount > 0)
                    parts.Add($"{errorCount} 个错误");
                if (warningCount > 0)
                    parts.Add($"{warningCount} 个警告");
                
                mValidationSummaryLabel.text = $"{string.Join(", ", parts)}";
                mValidationSummaryLabel.style.backgroundColor = new StyleColor(new Color(0.4f, 0.2f, 0.2f, 0.5f));
                mValidationSummaryLabel.style.color = new StyleColor(new Color(0.95f, 0.7f, 0.7f));
                mValidationSummaryLabel.style.borderLeftColor = new StyleColor(COLOR_ERROR);
            }
            
            // 通用样式
            ApplyValidationSummaryStyle();
        }
        
        /// <summary>
        /// 应用验证摘要样式
        /// </summary>
        private void ApplyValidationSummaryStyle()
        {
            mValidationSummaryLabel.style.paddingTop = 8;
            mValidationSummaryLabel.style.paddingBottom = 8;
            mValidationSummaryLabel.style.paddingLeft = 10;
            mValidationSummaryLabel.style.paddingRight = 10;
            mValidationSummaryLabel.style.marginTop = 4;
            mValidationSummaryLabel.style.marginBottom = 4;
            mValidationSummaryLabel.style.borderTopLeftRadius = 4;
            mValidationSummaryLabel.style.borderTopRightRadius = 4;
            mValidationSummaryLabel.style.borderBottomLeftRadius = 4;
            mValidationSummaryLabel.style.borderBottomRightRadius = 4;
            mValidationSummaryLabel.style.borderLeftWidth = 3;
        }
    }
}
#endif
