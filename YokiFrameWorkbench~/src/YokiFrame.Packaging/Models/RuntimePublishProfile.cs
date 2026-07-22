namespace YokiFrame.Packaging.Models;

/// <summary>
/// 描述当前宿主可生成的 WorkbenchRuntime profile 与入口布局。
/// </summary>
public sealed class RuntimePublishProfile
{
    /// <summary>
    /// 创建当前平台发布 profile。
    /// </summary>
    /// <param name="runtimeIdentifier">WorkbenchRuntime 平台目录标识。</param>
    /// <param name="dotnetRuntimeIdentifier">传给 dotnet publish 的 RID。</param>
    /// <param name="guiAppHostName">dotnet publish 生成的 GUI apphost 文件名。</param>
    /// <param name="cliAppHostName">dotnet publish 生成的 CLI apphost 文件名。</param>
    /// <param name="guiEntry">manifest 中的 GUI 相对入口。</param>
    /// <param name="cliEntry">manifest 中的 CLI 相对入口。</param>
    /// <param name="macAppBundleName">macOS app bundle 名；其它平台为空。</param>
    /// <param name="publishMode">GUI 发布方式。</param>
    internal RuntimePublishProfile(
        string runtimeIdentifier,
        string dotnetRuntimeIdentifier,
        string guiAppHostName,
        string cliAppHostName,
        string guiEntry,
        string cliEntry,
        string macAppBundleName,
        RuntimePublishMode publishMode)
    {
        RuntimeIdentifier = runtimeIdentifier;
        DotnetRuntimeIdentifier = dotnetRuntimeIdentifier;
        GuiAppHostName = guiAppHostName;
        CliAppHostName = cliAppHostName;
        GuiEntry = guiEntry;
        CliEntry = cliEntry;
        MacAppBundleName = macAppBundleName;
        PublishMode = publishMode;
    }

    /// <summary>
    /// 获取 WorkbenchRuntime 平台目录标识。
    /// </summary>
    public string RuntimeIdentifier { get; }

    /// <summary>
    /// 获取传给 dotnet publish 的 RID。
    /// </summary>
    public string DotnetRuntimeIdentifier { get; }

    /// <summary>
    /// 获取 GUI 项目发布产生的 apphost 文件名。
    /// </summary>
    public string GuiAppHostName { get; }

    /// <summary>
    /// 获取 CLI 项目发布产生的 apphost 文件名。
    /// </summary>
    public string CliAppHostName { get; }

    /// <summary>
    /// 获取 manifest 中的 GUI 相对入口。
    /// </summary>
    public string GuiEntry { get; }

    /// <summary>
    /// 获取 manifest 中的 CLI 相对入口。
    /// </summary>
    public string CliEntry { get; }

    /// <summary>
    /// 获取 macOS app bundle 名；非 macOS profile 为空。
    /// </summary>
    public string MacAppBundleName { get; }

    /// <summary>
    /// 获取 GUI 发布方式。
    /// </summary>
    public RuntimePublishMode PublishMode { get; }

    /// <summary>
    /// 获取当前 profile 是否同时发布共享 CLI。
    /// </summary>
    public bool PublishCli => !string.IsNullOrWhiteSpace(CliEntry);

    /// <summary>
    /// 获取 GUI 与 CLI 是否共用 framework-dependent 运行时文件；Native AOT 的两个入口各自独立。
    /// </summary>
    public bool SharedRuntime => PublishMode != RuntimePublishMode.NativeAot;

    /// <summary>
    /// 获取当前 profile 是否使用 self-contained 发布。
    /// </summary>
    public bool SelfContained => PublishMode == RuntimePublishMode.NativeAot;

    /// <summary>
    /// 获取 GUI 是否启用 ReadyToRun。
    /// </summary>
    public bool PublishReadyToRun => PublishMode == RuntimePublishMode.ReadyToRun;

    /// <summary>
    /// 获取当前 profile 是否启用 Native AOT。
    /// </summary>
    public bool PublishAot => PublishMode == RuntimePublishMode.NativeAot;
}
