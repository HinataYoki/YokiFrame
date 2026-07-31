using System.Text.Json;
using YokiFrame.RuntimeCache;

namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 定义 Workbench Runtime 新版检测和显式构建边界，供窗口生命周期测试注入可控任务。
/// </summary>
internal interface IWorkbenchRuntimeUpdateService
{
    /// <summary>后台比对当前源码与运行版本指纹。</summary>
    Task<WorkbenchRuntimeUpdateCheck> CheckAsync(
        string sourcePackageRoot,
        string projectRoot,
        string runningFingerprint,
        CancellationToken cancellationToken);

    /// <summary>显式构建当前源码对应的新 Runtime。</summary>
    Task<string> RebuildAsync(
        string sourcePackageRoot,
        string projectRoot,
        CancellationToken cancellationToken);
}

/// <summary>
/// 在 Workbench 进程内后台比对源码指纹，并通过 Packaging 权威入口显式构建新 Runtime。
/// </summary>
internal sealed class WorkbenchRuntimeUpdateService : IWorkbenchRuntimeUpdateService
{
    private const string WORKBENCH_DIRECTORY_NAME = "YokiFrameWorkbench~";
    private const string WORKBENCH_SOURCE_DIRECTORY_NAME = "src";
    private const string PACKAGING_PROJECT_RELATIVE_PATH =
        "src/YokiFrame.Packaging/YokiFrame.Packaging.csproj";

    /// <summary>
    /// 在后台计算当前源码指纹，并与本进程启动时的 Runtime 指纹比较。
    /// </summary>
    /// <param name="sourcePackageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="projectRoot">当前宿主项目根。</param>
    /// <param name="runningFingerprint">当前 Workbench 进程对应的源码指纹。</param>
    /// <param name="cancellationToken">窗口生命周期取消令牌。</param>
    /// <returns>源码与运行版本指纹比较结果。</returns>
    public async Task<WorkbenchRuntimeUpdateCheck> CheckAsync(
        string sourcePackageRoot,
        string projectRoot,
        string runningFingerprint,
        CancellationToken cancellationToken)
    {
        var fullSourcePackageRoot = RequireDirectory(sourcePackageRoot, "YokiFrame 源码包");
        if (!HasWorkbenchSource(fullSourcePackageRoot))
        {
            // Godot 安装是受控源码投影，按约定排除 Workbench 源码；此时没有可比较的更新输入。
            return new WorkbenchRuntimeUpdateCheck(runningFingerprint, UpdateAvailable: false);
        }

        if (!string.IsNullOrWhiteSpace(runningFingerprint))
        {
            await Task.Run(
                () => RuntimeCachePruner.PruneObsolete(projectRoot, runningFingerprint),
                cancellationToken).ConfigureAwait(false);
        }

        var sourceFingerprint = await Task.Run(
            () => YokiFrameWorkbenchSourceFingerprint.Compute(fullSourcePackageRoot, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        return new WorkbenchRuntimeUpdateCheck(
            sourceFingerprint,
            !string.Equals(sourceFingerprint, runningFingerprint, StringComparison.Ordinal));
    }

    /// <summary>
    /// 显式调用 Packaging bootstrap 构建当前源码 Runtime，不在成功后自动关闭或重启 Workbench。
    /// </summary>
    /// <param name="sourcePackageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="projectRoot">当前宿主项目根。</param>
    /// <param name="cancellationToken">窗口生命周期取消令牌。</param>
    /// <returns>Packaging 进程完成后的新当前指纹。</returns>
    public async Task<string> RebuildAsync(
        string sourcePackageRoot,
        string projectRoot,
        CancellationToken cancellationToken)
    {
        var fullSourcePackageRoot = RequireDirectory(sourcePackageRoot, "YokiFrame 源码包");
        RequireWorkbenchSource(fullSourcePackageRoot);
        var fullProjectRoot = RequireDirectory(projectRoot, "项目目录");
        var packagingProjectPath = Path.Combine(
            fullSourcePackageRoot,
            WORKBENCH_DIRECTORY_NAME,
            PACKAGING_PROJECT_RELATIVE_PATH.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(packagingProjectPath))
        {
            throw new FileNotFoundException("YokiFrame 源码包缺少 Packaging 项目。", packagingProjectPath);
        }

        var startInfo = RuntimeBootstrapProcessRunner.CreateStartInfo(
            fullSourcePackageRoot,
            fullProjectRoot,
            packagingProjectPath,
            openInstaller: false);
        await RuntimeBootstrapProcessRunner.RunAsync(
            startInfo,
            "Workbench Runtime 构建失败",
            cancellationToken).ConfigureAwait(false);
        return ReadCurrentFingerprint(fullProjectRoot);
    }

    /// <summary>
    /// 读取启动时 `current.json` 指向的 Runtime 指纹；指针缺失或损坏时返回空文本。
    /// </summary>
    /// <param name="projectRoot">当前宿主项目根。</param>
    /// <returns>合法的当前源码指纹或空文本。</returns>
    internal static string ReadCurrentFingerprint(string projectRoot)
    {
        try
        {
            var pointerPath = YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(projectRoot);
            using var document = JsonDocument.Parse(File.ReadAllText(pointerPath));
            var root = document.RootElement;
            if (!root.TryGetProperty("sourceFingerprint", out var property)
                || property.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            var fingerprint = property.GetString() ?? string.Empty;
            _ = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(projectRoot, fingerprint);
            return fingerprint;
        }
        catch (Exception exception) when (exception is IOException or JsonException or ArgumentException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// 判断给定包根是否包含可用于重新计算 Workbench 指纹的源码目录。
    /// </summary>
    /// <param name="sourcePackageRoot">待检查的 YokiFrame 包根。</param>
    /// <returns>存在完整 Workbench 源码根时返回 true。</returns>
    internal static bool HasWorkbenchSource(string sourcePackageRoot)
    {
        return Directory.Exists(Path.Combine(
            sourcePackageRoot,
            WORKBENCH_DIRECTORY_NAME,
            WORKBENCH_SOURCE_DIRECTORY_NAME));
    }

    /// <summary>
    /// 验证包根包含 Workbench 源码，并为显式重建给出明确诊断。
    /// </summary>
    /// <param name="sourcePackageRoot">待检查的 YokiFrame 包根。</param>
    private static void RequireWorkbenchSource(string sourcePackageRoot)
    {
        if (!HasWorkbenchSource(sourcePackageRoot))
        {
            throw new InvalidDataException(
                "当前 YokiFrame 投影包不包含 Workbench 源码；请从完整源码包启动 Installer 后再构建 Runtime。");
        }
    }

    /// <summary>
    /// 验证业务目录存在并返回规范化绝对路径。
    /// </summary>
    private static string RequireDirectory(string path, string displayName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(displayName + "不能为空。", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        return Directory.Exists(fullPath)
            ? fullPath
            : throw new DirectoryNotFoundException(displayName + "不存在: " + fullPath);
    }
}

/// <summary>
/// 保存 Workbench 运行版本与当前源码指纹的后台比较结果。
/// </summary>
/// <param name="SourceFingerprint">当前源码构建输入指纹。</param>
/// <param name="UpdateAvailable">当前运行版本是否落后于源码。</param>
internal sealed record WorkbenchRuntimeUpdateCheck(string SourceFingerprint, bool UpdateAvailable);
