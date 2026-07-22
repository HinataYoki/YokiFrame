namespace YokiFrame.Packaging.Models;

/// <summary>
/// 描述 WorkbenchRuntime 共享运行副本 manifest。
/// </summary>
public sealed class RuntimeManifest
{
    /// <summary>
    /// 创建运行副本 manifest。
    /// </summary>
    /// <param name="manifestVersion">manifest 版本。</param>
    /// <param name="layoutVersion">WorkbenchRuntime 布局版本。</param>
    /// <param name="product">产品名。</param>
    /// <param name="generatedAtUtc">生成时间。</param>
    /// <param name="runtimeRoot">运行副本根目录。</param>
    /// <param name="platforms">平台 manifest 列表。</param>
    public RuntimeManifest(
        int manifestVersion,
        int layoutVersion,
        string product,
        DateTimeOffset generatedAtUtc,
        string runtimeRoot,
        IReadOnlyList<RuntimePlatformManifest> platforms)
    {
        ManifestVersion = manifestVersion;
        LayoutVersion = layoutVersion;
        Product = product;
        GeneratedAtUtc = generatedAtUtc;
        RuntimeRoot = runtimeRoot;
        Platforms = platforms;
    }

    /// <summary>
    /// 获取 manifest 版本。
    /// </summary>
    public int ManifestVersion { get; }

    /// <summary>
    /// 获取 WorkbenchRuntime 布局版本；版本 2 表示平台同时声明 Workbench/Installer GUI 和 CLI 入口，二者是否共享运行时由平台记录表达。
    /// </summary>
    public int LayoutVersion { get; }

    /// <summary>
    /// 获取产品名。
    /// </summary>
    public string Product { get; }

    /// <summary>
    /// 获取生成时间。
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; }

    /// <summary>
    /// 获取运行副本根目录。
    /// </summary>
    public string RuntimeRoot { get; }

    /// <summary>
    /// 获取平台 manifest 列表。
    /// </summary>
    public IReadOnlyList<RuntimePlatformManifest> Platforms { get; }
}
