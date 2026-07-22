namespace YokiFrame.Packaging.Models;

/// <summary>
/// 描述当前平台 WorkbenchRuntime 发布完成后的正式入口。
/// </summary>
public sealed class RuntimePublishResult
{
    /// <summary>
    /// 创建发布结果。
    /// </summary>
    /// <param name="runtimeIdentifier">已发布平台标识。</param>
    /// <param name="publishRoot">正式平台目录。</param>
    /// <param name="guiPath">GUI 完整入口。</param>
    /// <param name="cliPath">CLI 完整入口。</param>
    /// <param name="manifestPath">manifest 完整路径。</param>
    internal RuntimePublishResult(
        string runtimeIdentifier,
        string publishRoot,
        string guiPath,
        string cliPath,
        string manifestPath)
    {
        RuntimeIdentifier = runtimeIdentifier;
        PublishRoot = publishRoot;
        GuiPath = guiPath;
        CliPath = cliPath;
        ManifestPath = manifestPath;
    }

    /// <summary>
    /// 获取已发布平台标识。
    /// </summary>
    public string RuntimeIdentifier { get; }

    /// <summary>
    /// 获取正式平台目录。
    /// </summary>
    public string PublishRoot { get; }

    /// <summary>
    /// 获取 GUI 完整入口。
    /// </summary>
    public string GuiPath { get; }

    /// <summary>
    /// 获取 CLI 完整入口。
    /// </summary>
    public string CliPath { get; }

    /// <summary>
    /// 获取 manifest 完整路径。
    /// </summary>
    public string ManifestPath { get; }
}
