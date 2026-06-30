namespace YokiFrame
{
    public enum ManagedRuntimeSelectionStatus
    {
        Selected = 0,
        AlreadySelected = 1,
        NotInstalled = 2,
        Unavailable = 3,
        InvalidBackendId = 4
    }

    /// <summary>
    /// 选择托管运行时后端的结果。
    /// </summary>
    public sealed class ManagedRuntimeSelectionResult
    {
        private ManagedRuntimeSelectionResult(
            bool success,
            ManagedRuntimeSelectionStatus status,
            string backendId,
            string message)
        {
            Success = success;
            Status = status;
            BackendId = backendId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool Success { get; private set; }

        public ManagedRuntimeSelectionStatus Status { get; private set; }

        public string BackendId { get; private set; }

        public string Message { get; private set; }

        public static ManagedRuntimeSelectionResult SuccessResult(
            ManagedRuntimeSelectionStatus status,
            string backendId,
            string message)
        {
            return new ManagedRuntimeSelectionResult(true, status, backendId, message);
        }

        public static ManagedRuntimeSelectionResult Failure(
            ManagedRuntimeSelectionStatus status,
            string backendId,
            string message)
        {
            return new ManagedRuntimeSelectionResult(false, status, backendId, message);
        }
    }
}
