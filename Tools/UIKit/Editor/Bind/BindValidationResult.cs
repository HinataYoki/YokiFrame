using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 绑定验证级别
    /// </summary>
    public enum BindValidationLevel
    {
        /// <summary>
        /// 信息提示
        /// </summary>
        Info,

        /// <summary>
        /// 警告（可继续生成）
        /// </summary>
        Warning,

        /// <summary>
        /// 错误（阻止生成）
        /// </summary>
        Error
    }

    /// <summary>
    /// 绑定验证结果 - 不可变结构体，用于传递验证信息
    /// </summary>
    public readonly struct BindValidationResult
    {
        /// <summary>
        /// 验证级别
        /// </summary>
        public readonly BindValidationLevel Level;

        /// <summary>
        /// 验证消息
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// 问题所在的 GameObject
        /// </summary>
        public readonly GameObject Target;

        /// <summary>
        /// 建议的修复方案
        /// </summary>
        public readonly string SuggestedFix;

        /// <summary>
        /// 验证规则标识（用于分类和过滤）
        /// </summary>
        public readonly string RuleId;

        /// <summary>
        /// 创建验证结果
        /// </summary>
        /// <param name="level">验证级别</param>
        /// <param name="message">验证消息</param>
        /// <param name="target">问题所在的 GameObject</param>
        /// <param name="suggestedFix">建议的修复方案</param>
        /// <param name="ruleId">验证规则标识</param>
        public BindValidationResult(
            BindValidationLevel level,
            string message,
            GameObject target = null,
            string suggestedFix = null,
            string ruleId = null)
        {
            Level = level;
            Message = message ?? string.Empty;
            Target = target;
            SuggestedFix = suggestedFix ?? string.Empty;
            RuleId = ruleId ?? string.Empty;
        }

        #region 工厂方法

        /// <summary>
        /// 创建错误级别的验证结果
        /// </summary>
        public static BindValidationResult Error(
            string message,
            GameObject target = null,
            string suggestedFix = null,
            string ruleId = null)
            => new(BindValidationLevel.Error, message, target, suggestedFix, ruleId);

        /// <summary>
        /// 创建警告级别的验证结果
        /// </summary>
        public static BindValidationResult Warning(
            string message,
            GameObject target = null,
            string suggestedFix = null,
            string ruleId = null)
            => new(BindValidationLevel.Warning, message, target, suggestedFix, ruleId);

        /// <summary>
        /// 创建信息级别的验证结果
        /// </summary>
        public static BindValidationResult Info(
            string message,
            GameObject target = null,
            string ruleId = null)
            => new(BindValidationLevel.Info, message, target, null, ruleId);

        #endregion

        /// <summary>
        /// 是否为错误级别
        /// </summary>
        public bool IsError => Level == BindValidationLevel.Error;

        /// <summary>
        /// 是否为警告级别
        /// </summary>
        public bool IsWarning => Level == BindValidationLevel.Warning;

        /// <summary>
        /// 是否有建议的修复方案
        /// </summary>
        public bool HasSuggestedFix => !string.IsNullOrEmpty(SuggestedFix);

        public override string ToString()
            => $"[{Level}] {Message}" + (Target != null ? $" @ {Target.name}" : string.Empty);
    }
}
