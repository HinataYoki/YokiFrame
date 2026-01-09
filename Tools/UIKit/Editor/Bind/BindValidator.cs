#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// 绑定验证器 - 验证绑定配置的正确性
    /// </summary>
    public static partial class BindValidator
    {
        #region 常量

        /// <summary>
        /// 验证规则 ID
        /// </summary>
        public static class RuleIds
        {
            public const string INVALID_IDENTIFIER = "BIND001";
            public const string CSHARP_KEYWORD = "BIND002";
            public const string NAME_CONFLICT = "BIND003";
            public const string ELEMENT_UNDER_COMPONENT = "BIND004";
            public const string EMPTY_NAME = "BIND005";
            public const string MISSING_TYPE = "BIND006";
        }

        #endregion

        #region 标识符验证

        /// <summary>
        /// 检查名称是否为有效的 C# 标识符
        /// </summary>
        /// <param name="name">要检查的名称</param>
        /// <returns>是否有效</returns>
        public static bool IsValidIdentifier(string name) => 
            YokiFrameEditorUtility.IsValidCSharpIdentifier(name);

        /// <summary>
        /// 检查名称是否为 C# 关键字
        /// </summary>
        /// <param name="name">要检查的名称</param>
        /// <returns>是否为关键字</returns>
        public static bool IsCSharpKeyword(string name) => 
            YokiFrameEditorUtility.IsCSharpKeyword(name);

        /// <summary>
        /// 验证标识符并返回详细结果
        /// </summary>
        /// <param name="name">要验证的名称</param>
        /// <param name="target">关联的 GameObject</param>
        /// <returns>验证结果，如果有效则返回 null</returns>
        public static BindValidationResult? ValidateIdentifier(string name, GameObject target = null)
        {
            if (!YokiFrameEditorUtility.ValidateCSharpIdentifier(name, out var errorMessage, out var suggestion))
            {
                // 确定规则 ID
                string ruleId;
                if (string.IsNullOrEmpty(name))
                    ruleId = RuleIds.EMPTY_NAME;
                else if (YokiFrameEditorUtility.IsCSharpKeyword(name))
                    ruleId = RuleIds.CSHARP_KEYWORD;
                else
                    ruleId = RuleIds.INVALID_IDENTIFIER;

                return BindValidationResult.Error(errorMessage, target, suggestion, ruleId);
            }

            return null;
        }

        #endregion

        #region 统计方法

        /// <summary>
        /// 统计验证结果中的错误数量
        /// </summary>
        public static int CountErrors(List<BindValidationResult> results)
        {
            if (results == null) return 0;

            int count = 0;
            foreach (var result in results)
            {
                if (result.Level == BindValidationLevel.Error)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 统计验证结果中的警告数量
        /// </summary>
        public static int CountWarnings(List<BindValidationResult> results)
        {
            if (results == null) return 0;

            int count = 0;
            foreach (var result in results)
            {
                if (result.Level == BindValidationLevel.Warning)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 检查验证结果中是否有错误
        /// </summary>
        public static bool HasErrors(List<BindValidationResult> results)
        {
            if (results == null) return false;

            foreach (var result in results)
            {
                if (result.Level == BindValidationLevel.Error)
                    return true;
            }
            return false;
        }

        #endregion
    }
}
#endif
