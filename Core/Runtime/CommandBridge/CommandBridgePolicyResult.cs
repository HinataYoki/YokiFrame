namespace YokiFrame
{
    /// <summary>
    /// 命令桥策略检查结果。
    /// </summary>
    public sealed class CommandBridgePolicyResult
    {
        private CommandBridgePolicyResult(bool allowed, string errorCode, string message, bool recoverable)
        {
            Allowed = allowed;
            ErrorCode = string.IsNullOrEmpty(errorCode) ? "PolicyDenied" : errorCode;
            Message = string.IsNullOrEmpty(message) ? "Command rejected by policy" : message;
            Recoverable = recoverable;
        }

        /// <summary>
        /// 获取命令是否允许执行。
        /// </summary>
        public bool Allowed { get; }

        /// <summary>
        /// 获取拒绝执行时返回的错误码。
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// 获取拒绝执行时返回的错误信息。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 获取拒绝是否可恢复。
        /// </summary>
        public bool Recoverable { get; }

        /// <summary>
        /// 创建允许执行的策略结果。
        /// </summary>
        /// <returns>允许执行的策略结果。</returns>
        public static CommandBridgePolicyResult Allow()
        {
            return new CommandBridgePolicyResult(true, null, null, false);
        }

        /// <summary>
        /// 创建不可恢复的拒绝结果。
        /// </summary>
        /// <param name="errorCode">错误码。</param>
        /// <param name="message">错误信息。</param>
        /// <returns>拒绝执行的策略结果。</returns>
        public static CommandBridgePolicyResult Deny(string errorCode, string message)
        {
            return Deny(errorCode, message, false);
        }

        /// <summary>
        /// 创建拒绝执行的策略结果。
        /// </summary>
        /// <param name="errorCode">错误码。</param>
        /// <param name="message">错误信息。</param>
        /// <param name="recoverable">是否可恢复。</param>
        /// <returns>拒绝执行的策略结果。</returns>
        public static CommandBridgePolicyResult Deny(string errorCode, string message, bool recoverable)
        {
            return new CommandBridgePolicyResult(false, errorCode, message, recoverable);
        }
    }
}
