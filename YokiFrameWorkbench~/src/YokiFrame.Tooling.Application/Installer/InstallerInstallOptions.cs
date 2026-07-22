using System.Runtime.InteropServices;

namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 描述一次 Installer 检测、规划和执行共享的不可变输入。
/// </summary>
public sealed class InstallerInstallOptions
{
    /// <summary>
    /// 创建已按安装模式约束字段组合的选项。
    /// </summary>
    /// <param name="mode">安装模式。</param>
    /// <param name="sourcePackageRoot">本地源包根；Git 模式为空。</param>
    /// <param name="targetProjectRoot">目标项目根。</param>
    /// <param name="gitUrl">Unity Git URL；非 Git 模式为空。</param>
    /// <param name="godotOptions">Godot 项目选项；非 Godot 模式为空。</param>
    /// <param name="legacyPackagePolicy">legacy 包接管策略。</param>
    private InstallerInstallOptions(
        InstallerInstallMode mode,
        string? sourcePackageRoot,
        string targetProjectRoot,
        string? gitUrl,
        GodotInstallOptions? godotOptions,
        InstallerLegacyPackagePolicy legacyPackagePolicy)
    {
        Mode = mode;
        SourcePackageRoot = sourcePackageRoot;
        TargetProjectRoot = targetProjectRoot;
        GitUrl = gitUrl;
        GodotOptions = godotOptions;
        LegacyPackagePolicy = legacyPackagePolicy;
        RuntimeProfile = ResolveRuntimeProfile();
    }

    /// <summary>
    /// 获取安装模式。
    /// </summary>
    public InstallerInstallMode Mode { get; }

    /// <summary>
    /// 获取本地源包根；Unity Git 模式返回 null。
    /// </summary>
    public string? SourcePackageRoot { get; }

    /// <summary>
    /// 获取目标 Unity 或 Godot 项目根。
    /// </summary>
    public string TargetProjectRoot { get; }

    /// <summary>
    /// 获取 Unity Git URL；非 Git 模式返回 null。
    /// </summary>
    public string? GitUrl { get; }

    /// <summary>
    /// 获取 Godot 项目配置选项；非 Godot 模式返回 null。
    /// </summary>
    public GodotInstallOptions? GodotOptions { get; }

    /// <summary>
    /// 获取 legacy 包接管策略。
    /// </summary>
    public InstallerLegacyPackagePolicy LegacyPackagePolicy { get; }

    /// <summary>
    /// 获取本次本地投影保留的当前平台 Runtime profile。
    /// </summary>
    public string RuntimeProfile { get; }

    /// <summary>
    /// 创建 Unity 本地 embedded package 安装选项。
    /// </summary>
    /// <param name="sourcePackageRoot">本地 YokiFrame 包根。</param>
    /// <param name="targetProjectRoot">目标 Unity 项目根。</param>
    /// <param name="legacyPackagePolicy">legacy 包接管策略。</param>
    /// <returns>Unity 本地安装选项。</returns>
    public static InstallerInstallOptions CreateUnityLocal(
        string sourcePackageRoot,
        string targetProjectRoot,
        InstallerLegacyPackagePolicy legacyPackagePolicy)
    {
        return new InstallerInstallOptions(
            InstallerInstallMode.UnityLocal,
            RequireText(sourcePackageRoot, nameof(sourcePackageRoot)),
            RequireText(targetProjectRoot, nameof(targetProjectRoot)),
            null,
            null,
            legacyPackagePolicy);
    }

    /// <summary>
    /// 创建 Unity Git URL 安装选项。
    /// </summary>
    /// <param name="targetProjectRoot">目标 Unity 项目根。</param>
    /// <param name="gitUrl">可编辑的 YokiFrame Git URL。</param>
    /// <returns>Unity Git 安装选项。</returns>
    public static InstallerInstallOptions CreateUnityGit(string targetProjectRoot, string gitUrl)
    {
        return new InstallerInstallOptions(
            InstallerInstallMode.UnityGit,
            null,
            RequireText(targetProjectRoot, nameof(targetProjectRoot)),
            RequireText(gitUrl, nameof(gitUrl)),
            null,
            InstallerLegacyPackagePolicy.Reject);
    }

    /// <summary>
    /// 创建 Godot .NET 本地投影安装选项。
    /// </summary>
    /// <param name="sourcePackageRoot">本地 YokiFrame 包根。</param>
    /// <param name="targetProjectRoot">目标 Godot 项目根。</param>
    /// <param name="godotOptions">Godot 项目配置选项。</param>
    /// <param name="legacyPackagePolicy">legacy 包接管策略。</param>
    /// <returns>Godot 本地安装选项。</returns>
    public static InstallerInstallOptions CreateGodotLocal(
        string sourcePackageRoot,
        string targetProjectRoot,
        GodotInstallOptions godotOptions,
        InstallerLegacyPackagePolicy legacyPackagePolicy)
    {
        ArgumentNullException.ThrowIfNull(godotOptions);
        return new InstallerInstallOptions(
            InstallerInstallMode.GodotLocal,
            RequireText(sourcePackageRoot, nameof(sourcePackageRoot)),
            RequireText(targetProjectRoot, nameof(targetProjectRoot)),
            null,
            godotOptions,
            legacyPackagePolicy);
    }

    /// <summary>
    /// 验证公开选项中的必填文本，保留原始内容供 Core 做路径规范化。
    /// </summary>
    /// <param name="value">待验证文本。</param>
    /// <param name="parameterName">参数名称。</param>
    /// <returns>非空白原始文本。</returns>
    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Installer option cannot be empty.", parameterName);
        }

        return value;
    }

    /// <summary>
    /// 根据当前工具进程平台选择仓库支持的 Runtime profile。
    /// </summary>
    /// <returns>受支持的 Runtime profile 名称。</returns>
    private static string ResolveRuntimeProfile()
    {
        if (OperatingSystem.IsWindows())
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

        throw new PlatformNotSupportedException("YokiFrame Installer does not support the current Runtime profile.");
    }
}
