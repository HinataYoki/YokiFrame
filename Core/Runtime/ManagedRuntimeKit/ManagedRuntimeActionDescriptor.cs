namespace YokiFrame
{
    /// <summary>
    /// 工作台可执行的托管运行时动作描述。
    /// </summary>
    public sealed class ManagedRuntimeActionDescriptor
    {
        public ManagedRuntimeActionDescriptor(
            string actionId,
            string displayName,
            string description,
            bool supported,
            bool requiresConfirmation,
            bool destructive)
        {
            ActionId = actionId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Supported = supported;
            RequiresConfirmation = requiresConfirmation;
            Destructive = destructive;
        }

        public string ActionId { get; private set; }

        public string DisplayName { get; private set; }

        public string Description { get; private set; }

        public bool Supported { get; private set; }

        public bool RequiresConfirmation { get; private set; }

        public bool Destructive { get; private set; }
    }
}
