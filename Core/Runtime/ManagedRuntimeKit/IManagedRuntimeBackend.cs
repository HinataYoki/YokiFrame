namespace YokiFrame
{
    /// <summary>
    /// 托管运行时后端抽象。Unity、Godot、LeanCLR、CoreCLR 等具体运行时在宿主侧实现。
    /// </summary>
    public interface IManagedRuntimeBackend
    {
        string BackendId { get; }

        string DisplayName { get; }

        ManagedRuntimeAvailability Availability { get; }

        ManagedRuntimeCapabilities Capabilities { get; }

        ManagedRuntimeInfo GetInfo();

        ManagedRuntimeValidationResult Validate();
    }
}
