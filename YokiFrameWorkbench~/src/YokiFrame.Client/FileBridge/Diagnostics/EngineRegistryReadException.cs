using YokiFrame.Protocol.FileBridge;

namespace YokiFrame.Client.FileBridge.Diagnostics;

/// <summary>
/// 表示 registry 目录中部分 engine.json 无法解析，但其它条目仍可继续使用。
/// </summary>
public sealed class EngineRegistryReadException : Exception
{
    /// <summary>
    /// 创建部分 registry 读取异常，并保留健康条目和坏文件证据。
    /// </summary>
    /// <param name="validEntries">已成功解析的 registry 条目。</param>
    /// <param name="invalidPaths">无法解析的 registry 文件路径。</param>
    /// <param name="message">聚合错误说明。</param>
    public EngineRegistryReadException(
        IReadOnlyList<EngineRegistryEntry> validEntries,
        IReadOnlyList<string> invalidPaths,
        string message)
        : base(message)
    {
        ValidEntries = validEntries.ToArray();
        InvalidPaths = invalidPaths.ToArray();
    }

    /// <summary>获取已经成功解析的 registry 条目。</summary>
    public IReadOnlyList<EngineRegistryEntry> ValidEntries { get; }

    /// <summary>获取无法解析的 registry 文件路径。</summary>
    public IReadOnlyList<string> InvalidPaths { get; }
}
