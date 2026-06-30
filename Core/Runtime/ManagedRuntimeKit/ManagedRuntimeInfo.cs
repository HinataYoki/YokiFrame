namespace YokiFrame
{
    /// <summary>
    /// 当前托管运行时后端状态快照。
    /// </summary>
    public sealed class ManagedRuntimeInfo
    {
        public ManagedRuntimeInfo(
            string backendId,
            string displayName,
            string hostName,
            string targetName,
            string executionMode,
            ManagedRuntimeAvailability availability,
            ManagedRuntimeCapabilities capabilities,
            string description)
        {
            BackendId = backendId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            HostName = hostName ?? string.Empty;
            TargetName = targetName ?? string.Empty;
            ExecutionMode = executionMode ?? string.Empty;
            Availability = availability;
            Capabilities = capabilities;
            Description = description ?? string.Empty;
        }

        public string BackendId { get; private set; }

        public string DisplayName { get; private set; }

        public string HostName { get; private set; }

        public string TargetName { get; private set; }

        public string ExecutionMode { get; private set; }

        public ManagedRuntimeAvailability Availability { get; private set; }

        public ManagedRuntimeCapabilities Capabilities { get; private set; }

        public string Description { get; private set; }
    }
}
