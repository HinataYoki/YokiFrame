namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 串行化共享固定 telemetry 段名的 Godot FileBridge Host 测试，避免并行 Host 覆盖彼此的会话帧。
/// </summary>
[CollectionDefinition(NAME, DisableParallelization = true)]
public sealed class GodotFileBridgeHostCollection
{
    /// <summary>Godot FileBridge Host 测试 collection 的稳定名称。</summary>
    public const string NAME = "GodotFileBridgeHost";
}
