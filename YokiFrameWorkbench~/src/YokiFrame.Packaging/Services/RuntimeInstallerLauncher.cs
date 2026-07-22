using System.Diagnostics;
using YokiFrame.Packaging.Models;

namespace YokiFrame.Packaging.Services;

/// <summary>
/// 启动项目 Runtime 缓存中的 Avalonia Installer，并把当前源码包和目标项目作为显式输入传入。
/// </summary>
public sealed class RuntimeInstallerLauncher
{
    private readonly Action<ProcessStartInfo> mStartProcess;

    /// <summary>
    /// 创建使用系统进程启动器的 Runtime Installer 启动服务。
    /// </summary>
    public RuntimeInstallerLauncher()
        : this(StartProcess)
    {
    }

    /// <summary>
    /// 创建可由测试替换进程启动边界的 Runtime Installer 启动服务。
    /// </summary>
    /// <param name="startProcess">接收已验证启动参数的系统进程启动动作。</param>
    internal RuntimeInstallerLauncher(Action<ProcessStartInfo> startProcess)
    {
        mStartProcess = startProcess ?? throw new ArgumentNullException(nameof(startProcess));
    }

    /// <summary>
    /// 启动与刚完成 bootstrap 的缓存匹配的新 Installer，避免旧 Runtime 继续处理更新后的源码包。
    /// </summary>
    /// <param name="bootstrapResult">已发布或复用的当前平台 Runtime 结果。</param>
    /// <param name="sourcePackageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="targetProjectRoot">目标 Unity 或 Godot 项目根。</param>
    public void Launch(
        RuntimeCacheBootstrapResult bootstrapResult,
        string sourcePackageRoot,
        string targetProjectRoot)
    {
        ArgumentNullException.ThrowIfNull(bootstrapResult);
        var startInfo = CreateStartInfo(
            bootstrapResult.PublishResult.GuiPath,
            sourcePackageRoot,
            targetProjectRoot);
        mStartProcess(startInfo);
    }

    /// <summary>
    /// 构造不经 shell 解释的 GUI 进程参数，确保空格或特殊字符不会改变 source、target 语义。
    /// </summary>
    /// <param name="guiPath">当前 Runtime manifest 已验证的 GUI 入口。</param>
    /// <param name="sourcePackageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="targetProjectRoot">目标 Unity 或 Godot 项目根。</param>
    /// <returns>可交给系统进程 API 的启动配置。</returns>
    private static ProcessStartInfo CreateStartInfo(
        string guiPath,
        string sourcePackageRoot,
        string targetProjectRoot)
    {
        var fullGuiPath = RequireFile(guiPath, "Runtime Installer GUI");
        var fullSourcePackageRoot = RequireDirectory(sourcePackageRoot, "YokiFrame package root");
        var fullTargetProjectRoot = RequireDirectory(targetProjectRoot, "Target project root");
        ProcessStartInfo startInfo = new(fullGuiPath)
        {
            WorkingDirectory = fullTargetProjectRoot,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--source");
        startInfo.ArgumentList.Add(fullSourcePackageRoot);
        startInfo.ArgumentList.Add("--target");
        startInfo.ArgumentList.Add(fullTargetProjectRoot);
        return startInfo;
    }

    /// <summary>
    /// 验证目录存在并返回完整路径，避免把 Installer 启动到失效工作目录。
    /// </summary>
    /// <param name="path">待验证目录。</param>
    /// <param name="displayName">错误消息中的目录名称。</param>
    /// <returns>已验证的完整目录路径。</returns>
    private static string RequireDirectory(string path, string displayName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(displayName + " is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        return Directory.Exists(fullPath)
            ? fullPath
            : throw new DirectoryNotFoundException(displayName + " was not found: " + fullPath);
    }

    /// <summary>
    /// 验证 GUI 入口文件存在并返回完整路径，防止 bootstrap 成功后静默启动空路径。
    /// </summary>
    /// <param name="path">待验证 GUI 入口。</param>
    /// <param name="displayName">错误消息中的入口名称。</param>
    /// <returns>已验证的完整文件路径。</returns>
    private static string RequireFile(string path, string displayName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(displayName + " is required.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException(displayName + " was not found.", fullPath);
    }

    /// <summary>
    /// 使用系统进程 API 启动新 Installer；释放本地进程句柄不会结束子进程。
    /// </summary>
    /// <param name="startInfo">已经完成路径和参数校验的启动配置。</param>
    private static void StartProcess(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start the Runtime Installer.");
    }
}
