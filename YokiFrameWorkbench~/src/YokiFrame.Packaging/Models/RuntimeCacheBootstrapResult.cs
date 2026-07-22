namespace YokiFrame.Packaging.Models;

/// <summary>
/// 描述项目级 Runtime bootstrap 的来源指纹、缓存目录和实际发布结果。
/// </summary>
public sealed class RuntimeCacheBootstrapResult
{
    /// <summary>
    /// 创建 Runtime bootstrap 结果。
    /// </summary>
    /// <param name="sourceFingerprint">参与构建的源码 SHA-256 指纹。</param>
    /// <param name="runtimeRoot">当前有效 Runtime 缓存根。</param>
    /// <param name="publishResult">已解析或刚刚发布的入口结果。</param>
    /// <param name="rebuilt">本次是否实际调用了 dotnet publish。</param>
    internal RuntimeCacheBootstrapResult(
        string sourceFingerprint,
        string runtimeRoot,
        RuntimePublishResult publishResult,
        bool rebuilt)
    {
        SourceFingerprint = sourceFingerprint;
        RuntimeRoot = runtimeRoot;
        PublishResult = publishResult;
        Rebuilt = rebuilt;
    }

    /// <summary>获取实际 Workbench 构建输入的 SHA-256 指纹。</summary>
    public string SourceFingerprint { get; }

    /// <summary>获取当前有效 Runtime 缓存根。</summary>
    public string RuntimeRoot { get; }

    /// <summary>获取当前平台 GUI、CLI 与 manifest 入口。</summary>
    public RuntimePublishResult PublishResult { get; }

    /// <summary>获取本次是否实际重建了 Runtime。</summary>
    public bool Rebuilt { get; }
}
