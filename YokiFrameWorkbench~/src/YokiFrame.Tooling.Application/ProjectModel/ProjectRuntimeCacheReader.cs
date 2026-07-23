using System.Runtime.InteropServices;
using System.Text.Json;
using YokiFrame.RuntimeCache;

namespace YokiFrame.Tooling.Application.ProjectModel;

/// <summary>
/// 快速读取项目 `.yokiframe` 当前 Workbench Runtime 缓存；源码新版由 Workbench 后台检测。
/// </summary>
internal sealed class ProjectRuntimeCacheReader
{
    private const int LAYOUT_VERSION = 1;

    /// <summary>
    /// 解析当前项目的 Runtime 缓存状态；缺失、过期或损坏缓存以不可用状态返回，不阻断 Project Model 刷新。
    /// </summary>
    /// <param name="projectRoot">Unity 或 Godot 项目根。</param>
    /// <param name="packageRoot">当前 YokiFrame 源码包根。</param>
    /// <returns>当前宿主 profile 对应的缓存路径、入口和可用性。</returns>
    public ProjectRuntimeCacheState Read(string projectRoot, string packageRoot)
    {
        var pointerPath = YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(projectRoot);
        var runtimeIdentifier = ResolveCurrentRuntimeIdentifier();
        if (string.IsNullOrWhiteSpace(runtimeIdentifier) || !File.Exists(pointerPath))
        {
            return new ProjectRuntimeCacheState(pointerPath, string.Empty, string.Empty, runtimeIdentifier, string.Empty, string.Empty);
        }

        if (!TryReadPointer(pointerPath, out var sourceFingerprint))
        {
            return new ProjectRuntimeCacheState(pointerPath, string.Empty, string.Empty, runtimeIdentifier, string.Empty, string.Empty);
        }

        var runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(projectRoot, sourceFingerprint);
        var manifestPath = Path.Combine(runtimeRoot, "tool-manifest.json");
        if (!RuntimeManifestIntegrityValidator.TryResolveLaunchProfile(
                manifestPath,
                runtimeRoot,
                runtimeIdentifier,
                requireCli: false,
                out var profile,
                out _))
        {
            return new ProjectRuntimeCacheState(pointerPath, manifestPath, runtimeRoot, runtimeIdentifier, string.Empty, string.Empty);
        }

        return new ProjectRuntimeCacheState(
            pointerPath,
            manifestPath,
            runtimeRoot,
            runtimeIdentifier,
            profile.GuiPath,
            profile.CliPath);
    }

    /// <summary>
    /// 读取并验证项目 current.json 的最小布局字段。
    /// </summary>
    /// <param name="path">指针文件路径。</param>
    /// <param name="sourceFingerprint">解析出的源码指纹。</param>
    /// <returns>布局和指纹均有效时返回 true。</returns>
    private static bool TryReadPointer(string path, out string sourceFingerprint)
    {
        sourceFingerprint = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (!root.TryGetProperty("layoutVersion", out var layoutVersion)
                || !layoutVersion.TryGetInt32(out var version)
                || version != LAYOUT_VERSION
                || !root.TryGetProperty("sourceFingerprint", out var fingerprint)
                || fingerprint.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            sourceFingerprint = fingerprint.GetString() ?? string.Empty;
            _ = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(".", sourceFingerprint);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// 将当前主机映射为 bootstrap 实际生成的 profile；Windows x64 固定选择 Native AOT。
    /// </summary>
    /// <returns>受支持 profile；当前主机不支持时返回空。</returns>
    private static string ResolveCurrentRuntimeIdentifier()
    {
        if (OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            return "win-x64-aot";
        }

        if (OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            return "linux-x64";
        }

        if (OperatingSystem.IsMacOS())
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        }

        return string.Empty;
    }
}

/// <summary>
/// 描述当前源码指纹对应的项目 Runtime 缓存路径、入口和实际可用性。
/// </summary>
internal sealed class ProjectRuntimeCacheState
{
    /// <summary>
    /// 创建 Runtime 缓存读取结果。
    /// </summary>
    /// <param name="pointerPath">current.json 路径。</param>
    /// <param name="manifestPath">当前指纹 manifest 路径。</param>
    /// <param name="runtimeRoot">当前指纹 Runtime 根。</param>
    /// <param name="runtimeIdentifier">当前宿主 profile。</param>
    /// <param name="guiPath">已校验的 GUI 入口。</param>
    /// <param name="cliPath">已校验的 CLI 入口。</param>
    public ProjectRuntimeCacheState(
        string pointerPath,
        string manifestPath,
        string runtimeRoot,
        string runtimeIdentifier,
        string guiPath,
        string cliPath)
    {
        PointerPath = pointerPath;
        ManifestPath = manifestPath;
        RuntimeRoot = runtimeRoot;
        RuntimeIdentifier = runtimeIdentifier;
        GuiPath = guiPath;
        CliPath = cliPath;
    }

    /// <summary>获取项目 current.json 路径。</summary>
    public string PointerPath { get; }

    /// <summary>获取当前指纹 manifest 路径。</summary>
    public string ManifestPath { get; }

    /// <summary>获取当前指纹 Runtime 根。</summary>
    public string RuntimeRoot { get; }

    /// <summary>获取当前宿主 profile。</summary>
    public string RuntimeIdentifier { get; }

    /// <summary>获取已校验 GUI 入口；不可用时为空。</summary>
    public string GuiPath { get; }

    /// <summary>获取已校验 CLI 入口；不可用时为空。</summary>
    public string CliPath { get; }

    /// <summary>获取 GUI 是否可启动。</summary>
    public bool IsWorkbenchAvailable => !string.IsNullOrWhiteSpace(GuiPath) && File.Exists(GuiPath);

    /// <summary>获取 CLI 是否可执行。</summary>
    public bool IsCliAvailable => !string.IsNullOrWhiteSpace(CliPath) && File.Exists(CliPath);
}
