namespace YokiFrame
{
    /// <summary>
    /// 托管运行时工作流动作执行结果。
    /// </summary>
    public sealed class ManagedRuntimeActionResult
    {
        private ManagedRuntimeActionResult(
            bool success,
            string backendId,
            string actionId,
            string message,
            string dataJson,
            string errorCode)
        {
            Success = success;
            BackendId = backendId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            Message = message ?? string.Empty;
            DataJson = dataJson ?? string.Empty;
            ErrorCode = errorCode ?? string.Empty;
        }

        public bool Success { get; private set; }

        public string BackendId { get; private set; }

        public string ActionId { get; private set; }

        public string Message { get; private set; }

        public string DataJson { get; private set; }

        public string ErrorCode { get; private set; }

        public static ManagedRuntimeActionResult SuccessResult(
            string backendId,
            string actionId,
            string message,
            string dataJson)
        {
            return new ManagedRuntimeActionResult(true, backendId, actionId, message, dataJson, string.Empty);
        }

        public static ManagedRuntimeActionResult Failure(
            string backendId,
            string actionId,
            string message,
            string errorCode)
        {
            return new ManagedRuntimeActionResult(false, backendId, actionId, message, string.Empty, errorCode);
        }
    }
}
