using System.Diagnostics;

namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 为 Installer 提供从选定源码包构建 Godot 项目 Runtime 的平台进程边界。
/// </summary>
internal interface IGodotRuntimeBootstrapper
{
    /// <summary>
    /// 使用选定源码包构建目标项目当前平台缓存，但不启动新的 Installer。
    /// </summary>
    /// <param name="sourcePackageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="targetProjectRoot">目标 Godot 项目根。</param>
    /// <param name="cancellationToken">用户取消当前构建时使用的令牌。</param>
    /// <returns>Runtime bootstrap 子进程完成任务。</returns>
    Task BootstrapAsync(
        string sourcePackageRoot,
        string targetProjectRoot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用选定源码包构建目标项目当前平台缓存，并启动该缓存中的 Installer。
    /// </summary>
    /// <param name="sourcePackageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="targetProjectRoot">目标 Godot 项目根。</param>
    /// <param name="cancellationToken">用户取消当前构建时使用的令牌。</param>
    /// <returns>Runtime bootstrap 子进程完成任务。</returns>
    Task BootstrapAndOpenInstallerAsync(
        string sourcePackageRoot,
        string targetProjectRoot,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 通过源码包内 Packaging 项目执行受控 Runtime bootstrap，避免 Avalonia 重新实现缓存和启动规则。
/// </summary>
internal sealed class GodotRuntimeBootstrapper : IGodotRuntimeBootstrapper
{
    private const string WORKBENCH_DIRECTORY_NAME = "YokiFrameWorkbench~";
    private const string PACKAGING_PROJECT_RELATIVE_PATH =
        "src/YokiFrame.Packaging/YokiFrame.Packaging.csproj";

    /// <summary>
    /// 使用 `dotnet run` 调用源码包的 Packaging 权威入口，成功后由 Packaging 启动新 Installer。
    /// </summary>
    /// <param name="sourcePackageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="targetProjectRoot">目标 Godot 项目根。</param>
    /// <param name="cancellationToken">用户取消当前构建时使用的令牌。</param>
    /// <returns>Runtime bootstrap 子进程完成任务。</returns>
    public async Task BootstrapAndOpenInstallerAsync(
        string sourcePackageRoot,
        string targetProjectRoot,
        CancellationToken cancellationToken = default)
    {
        await RunBootstrapAsync(
            sourcePackageRoot,
            targetProjectRoot,
            openInstaller: true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 使用选定源码包构建目标项目当前平台缓存，不重新打开 Installer。
    /// </summary>
    /// <param name="sourcePackageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="targetProjectRoot">目标 Godot 项目根。</param>
    /// <param name="cancellationToken">用户取消当前构建时使用的令牌。</param>
    /// <returns>Runtime bootstrap 子进程完成任务。</returns>
    public Task BootstrapAsync(
        string sourcePackageRoot,
        string targetProjectRoot,
        CancellationToken cancellationToken = default)
    {
        return RunBootstrapAsync(
            sourcePackageRoot,
            targetProjectRoot,
            openInstaller: false,
            cancellationToken);
    }

    /// <summary>
    /// 验证源码包、目标项目和 Packaging 项目后运行一次 Runtime bootstrap。
    /// </summary>
    /// <param name="sourcePackageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="targetProjectRoot">目标 Godot 项目根。</param>
    /// <param name="openInstaller">成功后是否启动新的 Installer。</param>
    /// <param name="cancellationToken">用户取消当前构建时使用的令牌。</param>
    /// <returns>Runtime bootstrap 子进程完成任务。</returns>
    private async Task RunBootstrapAsync(
        string sourcePackageRoot,
        string targetProjectRoot,
        bool openInstaller,
        CancellationToken cancellationToken)
    {
        var fullSourcePackageRoot = RequireDirectory(sourcePackageRoot, "YokiFrame 源目录");
        var fullTargetProjectRoot = RequireDirectory(targetProjectRoot, "Godot 项目目录");
        var packagingProjectPath = Path.Combine(
            fullSourcePackageRoot,
            WORKBENCH_DIRECTORY_NAME,
            PACKAGING_PROJECT_RELATIVE_PATH.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(packagingProjectPath))
        {
            throw new FileNotFoundException("YokiFrame 源码包缺少 Runtime bootstrap 所需的 Packaging 项目。", packagingProjectPath);
        }

        var startInfo = CreateStartInfo(
            fullSourcePackageRoot,
            fullTargetProjectRoot,
            packagingProjectPath,
            openInstaller);
        await RuntimeBootstrapProcessRunner.RunAsync(
            startInfo,
            "Godot Runtime 构建失败",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 创建不经 shell 解释的 Packaging 命令，确保带空格的包根和项目根不会改变参数边界。
    /// </summary>
    /// <param name="sourcePackageRoot">已规范化的 YokiFrame 源码包根。</param>
    /// <param name="targetProjectRoot">已规范化的 Godot 项目根。</param>
    /// <param name="packagingProjectPath">Packaging 项目完整路径。</param>
    /// <returns>可直接启动的 dotnet 进程配置。</returns>
    internal static ProcessStartInfo CreateStartInfo(
        string sourcePackageRoot,
        string targetProjectRoot,
        string packagingProjectPath)
    {
        return CreateStartInfo(
            sourcePackageRoot,
            targetProjectRoot,
            packagingProjectPath,
            openInstaller: true);
    }

    /// <summary>
    /// 创建带可选 Installer 启动开关的 Packaging 命令，供自动构建和手动恢复共用。
    /// </summary>
    /// <param name="sourcePackageRoot">已规范化的 YokiFrame 源码包根。</param>
    /// <param name="targetProjectRoot">已规范化的 Godot 项目根。</param>
    /// <param name="packagingProjectPath">Packaging 项目完整路径。</param>
    /// <param name="openInstaller">成功后是否启动新的 Installer。</param>
    /// <returns>可直接启动的 dotnet 进程配置。</returns>
    internal static ProcessStartInfo CreateStartInfo(
        string sourcePackageRoot,
        string targetProjectRoot,
        string packagingProjectPath,
        bool openInstaller)
    {
        return RuntimeBootstrapProcessRunner.CreateStartInfo(
            sourcePackageRoot,
            targetProjectRoot,
            packagingProjectPath,
            openInstaller);
    }

    /// <summary>
    /// 验证需要参与构建的目录存在，并统一返回绝对路径。
    /// </summary>
    /// <param name="path">用户选择的目录。</param>
    /// <param name="displayName">面向用户的目录名称。</param>
    /// <returns>已验证的完整目录路径。</returns>
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
