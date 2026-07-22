namespace YokiFrame.Packaging.Models;

/// <summary>
/// 描述 WorkbenchRuntime profile 中 GUI 与 CLI 的发布方式。
/// </summary>
public enum RuntimePublishMode
{
    /// <summary>
    /// Framework-dependent managed 发布。
    /// </summary>
    Managed,

    /// <summary>
    /// Framework-dependent ReadyToRun 发布。
    /// </summary>
    ReadyToRun,

    /// <summary>
    /// Self-contained Native AOT 发布；profile 内的 GUI 与 CLI 均使用 Native AOT。
    /// </summary>
    NativeAot
}
