#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 表示 CommandPolicy 对单条命令的允许或拒绝结果。
    /// </summary>
    public sealed class YokiFrameCommandPolicyDecision
    {
        /// <summary>
        /// 创建策略决策；外部应优先使用 <see cref="Allow"/> 或 <see cref="Reject"/>。
        /// </summary>
        /// <param name="isAllowed">是否允许命令继续执行。</param>
        /// <param name="kind">命令风险等级。</param>
        /// <param name="errorCode">拒绝错误码。</param>
        /// <param name="errorMessage">拒绝说明。</param>
        private YokiFrameCommandPolicyDecision(
            bool isAllowed,
            YokiFrameCommandKind kind,
            string errorCode,
            string errorMessage)
        {
            IsAllowed = isAllowed;
            Kind = kind;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// 获取是否允许命令继续执行。
        /// </summary>
        public bool IsAllowed { get; }

        /// <summary>
        /// 获取命令风险等级；拒绝未知命令时默认是只读。
        /// </summary>
        public YokiFrameCommandKind Kind { get; }

        /// <summary>
        /// 获取拒绝错误码；允许时为空字符串。
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// 获取拒绝说明；允许时为空字符串。
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 创建允许执行的策略结果。
        /// </summary>
        /// <param name="kind">命令风险等级。</param>
        /// <returns>允许执行的决策。</returns>
        public static YokiFrameCommandPolicyDecision Allow(YokiFrameCommandKind kind)
        {
            return new YokiFrameCommandPolicyDecision(true, kind, string.Empty, string.Empty);
        }

        /// <summary>
        /// 创建拒绝执行的策略结果。
        /// </summary>
        /// <param name="errorCode">拒绝错误码。</param>
        /// <param name="errorMessage">拒绝说明。</param>
        /// <returns>拒绝执行的决策。</returns>
        public static YokiFrameCommandPolicyDecision Reject(string errorCode, string errorMessage)
        {
            return new YokiFrameCommandPolicyDecision(false, YokiFrameCommandKind.ReadOnly, errorCode, errorMessage);
        }
    }
}
#endif
