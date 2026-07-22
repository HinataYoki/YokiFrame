using System.Globalization;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>
/// 描述单一 Avalonia 工具应用的启动模式和默认路径。
/// </summary>
public sealed class ToolStartupOptions
{
    private const string ParentWindowHandleOptionName = "parent-hwnd";
    private const int MAX_PACKAGE_ROOT_SEARCH_DEPTH = 10;

    /// <summary>
    /// 创建启动选项。
    /// </summary>
    /// <param name="mode">启动界面模式。</param>
    /// <param name="projectRoot">Workbench 访问的项目根。</param>
    /// <param name="sourcePackageRoot">Installer 默认源包根。</param>
    /// <param name="targetProjectRoot">Installer 默认目标项目根。</param>
    public ToolStartupOptions(string mode, string projectRoot, string sourcePackageRoot, string targetProjectRoot)
        : this(ParseMode(mode), projectRoot, sourcePackageRoot, targetProjectRoot, IntPtr.Zero)
    {
    }

    /// <summary>
    /// 创建启动选项，并携带宿主窗口句柄供 Workbench 嵌入。
    /// </summary>
    /// <param name="mode">启动界面模式。</param>
    /// <param name="projectRoot">Workbench 访问的项目根。</param>
    /// <param name="sourcePackageRoot">Installer 默认源包根。</param>
    /// <param name="targetProjectRoot">Installer 默认目标项目根。</param>
    /// <param name="parentWindowHandle">宿主窗口原生句柄；为 0 时不尝试嵌入。</param>
    public ToolStartupOptions(string mode, string projectRoot, string sourcePackageRoot, string targetProjectRoot, IntPtr parentWindowHandle)
        : this(ParseMode(mode), projectRoot, sourcePackageRoot, targetProjectRoot, parentWindowHandle)
    {
    }

    /// <summary>
    /// 创建启动选项。
    /// </summary>
    /// <param name="mode">启动界面模式。</param>
    /// <param name="projectRoot">Workbench 访问的项目根。</param>
    /// <param name="sourcePackageRoot">Installer 默认源包根。</param>
    /// <param name="targetProjectRoot">Installer 默认目标项目根。</param>
    public ToolStartupOptions(ToolStartupMode mode, string projectRoot, string sourcePackageRoot, string targetProjectRoot)
        : this(mode, projectRoot, sourcePackageRoot, targetProjectRoot, IntPtr.Zero)
    {
    }

    /// <summary>
    /// 创建启动选项，并携带宿主窗口句柄供 Workbench 嵌入。
    /// </summary>
    /// <param name="mode">启动界面模式。</param>
    /// <param name="projectRoot">Workbench 访问的项目根。</param>
    /// <param name="sourcePackageRoot">Installer 默认源包根。</param>
    /// <param name="targetProjectRoot">Installer 默认目标项目根。</param>
    /// <param name="parentWindowHandle">宿主窗口原生句柄；为 0 时不尝试嵌入。</param>
    public ToolStartupOptions(ToolStartupMode mode, string projectRoot, string sourcePackageRoot, string targetProjectRoot, IntPtr parentWindowHandle)
    {
        Mode = mode;
        ProjectRoot = Path.GetFullPath(projectRoot);
        SourcePackageRoot = Path.GetFullPath(sourcePackageRoot);
        TargetProjectRoot = Path.GetFullPath(targetProjectRoot);
        ParentWindowHandle = parentWindowHandle;
    }

    /// <summary>
    /// 获取启动界面模式。
    /// </summary>
    public ToolStartupMode Mode { get; }

    /// <summary>
    /// 获取 Workbench 访问的项目根。
    /// </summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// 获取 Installer 默认源包根。
    /// </summary>
    public string SourcePackageRoot { get; }

    /// <summary>
    /// 获取 Installer 默认目标项目根。
    /// </summary>
    public string TargetProjectRoot { get; }

    /// <summary>
    /// 获取宿主窗口原生句柄；为 0 时 Workbench 保持普通桌面窗口行为。
    /// </summary>
    public IntPtr ParentWindowHandle { get; }

    /// <summary>
    /// 从命令行、当前目录和应用目录推断启动模式；只有明确传入 project 时进入 Workbench。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="currentDirectory">当前工作目录。</param>
    /// <param name="appBaseDirectory">应用程序集所在目录。</param>
    /// <returns>启动选项。</returns>
    public static ToolStartupOptions FromArgs(string[] args, string currentDirectory, string appBaseDirectory)
    {
        var projectRoot = ReadOption(args, "project");
        var parentWindowHandle = ParseParentWindowHandle(ReadOption(args, ParentWindowHandleOptionName));
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            var fullProjectRoot = Path.GetFullPath(projectRoot);
            var workbenchSourceRoot = ReadOption(args, "source");
            var workbenchDetectedPackageRoot = DetectPackageRoot(appBaseDirectory, currentDirectory);
            var workbenchResolvedSourceRoot = string.IsNullOrWhiteSpace(workbenchSourceRoot)
                ? workbenchDetectedPackageRoot ?? Path.Combine(fullProjectRoot, "Assets", "YokiFrame")
                : Path.GetFullPath(workbenchSourceRoot);
            return new ToolStartupOptions(
                ToolStartupMode.Workbench,
                fullProjectRoot,
                workbenchResolvedSourceRoot,
                fullProjectRoot,
                parentWindowHandle);
        }

        var detectedPackageRoot = DetectPackageRoot(appBaseDirectory, currentDirectory);
        var targetRoot = ResolveInstallerTargetRoot(args, currentDirectory, detectedPackageRoot);
        var sourceRoot = ReadOption(args, "source");
        var resolvedSourceRoot = string.IsNullOrWhiteSpace(sourceRoot)
            ? detectedPackageRoot ?? Path.Combine(targetRoot, "Assets", "YokiFrame")
            : Path.GetFullPath(sourceRoot);
        return new ToolStartupOptions(ToolStartupMode.Installer, targetRoot, resolvedSourceRoot, targetRoot, parentWindowHandle);
    }

    /// <summary>
    /// 解析文本模式，供测试或未来外部配置复用。
    /// </summary>
    /// <param name="mode">模式文本。</param>
    /// <returns>启动模式。</returns>
    private static ToolStartupMode ParseMode(string mode)
    {
        return string.Equals(mode, "Workbench", StringComparison.OrdinalIgnoreCase)
            ? ToolStartupMode.Workbench
            : ToolStartupMode.Installer;
    }

    /// <summary>
    /// 解析宿主窗口句柄；无效输入返回 0，避免坏参数阻断 Installer 或 Workbench 启动。
    /// </summary>
    /// <param name="handleText">十进制或 `0x` 前缀十六进制句柄文本。</param>
    /// <returns>解析后的原生句柄；无效时返回 0。</returns>
    private static IntPtr ParseParentWindowHandle(string handleText)
    {
        if (string.IsNullOrWhiteSpace(handleText))
        {
            return IntPtr.Zero;
        }

        var normalizedHandleText = handleText.Trim();
        var numberStyle = NumberStyles.Integer;
        if (normalizedHandleText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalizedHandleText = normalizedHandleText[2..];
            numberStyle = NumberStyles.HexNumber;
        }

        return long.TryParse(normalizedHandleText, numberStyle, CultureInfo.InvariantCulture, out var parentWindowHandle)
            ? new IntPtr(parentWindowHandle)
            : IntPtr.Zero;
    }

    /// <summary>
    /// 解析 Installer 默认目标项目根；显式 target 优先，其次从包根回推项目根。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="currentDirectory">当前工作目录。</param>
    /// <param name="detectedPackageRoot">从应用目录识别出的包根。</param>
    /// <returns>目标项目根。</returns>
    private static string ResolveInstallerTargetRoot(string[] args, string currentDirectory, string? detectedPackageRoot)
    {
        var targetRoot = ReadOption(args, "target");
        if (!string.IsNullOrWhiteSpace(targetRoot))
        {
            return Path.GetFullPath(targetRoot);
        }

        return ResolveProjectRootFromPackageRoot(detectedPackageRoot) ?? Path.GetFullPath(currentDirectory);
    }

    /// <summary>
    /// 从应用目录或当前目录的有限祖先范围识别有效 YokiFrame 包根。
    /// </summary>
    /// <param name="candidateDirectories">应用程序集所在目录和当前工作目录。</param>
    /// <returns>包根；无法识别时返回 null。</returns>
    private static string? DetectPackageRoot(params string[] candidateDirectories)
    {
        foreach (var candidateDirectory in candidateDirectories)
        {
            if (string.IsNullOrWhiteSpace(candidateDirectory))
            {
                continue;
            }

            var packageRoot = FindPackageRootFromAncestor(candidateDirectory);
            if (packageRoot != null)
            {
                return packageRoot;
            }
        }

        return null;
    }

    /// <summary>
    /// 向上扫描不超过十层目录，同时识别直接包根和祖先下的 Assets/YokiFrame 布局。
    /// </summary>
    /// <param name="startDirectory">扫描起点。</param>
    /// <returns>有效包根；未找到时返回 null。</returns>
    private static string? FindPackageRootFromAncestor(string startDirectory)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        for (var depth = 0; current != null && depth <= MAX_PACKAGE_ROOT_SEARCH_DEPTH; depth++)
        {
            var packageRoot = TryResolvePackageRoot(current);
            if (packageRoot != null)
            {
                return packageRoot;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// 从一个候选祖先解析直接包根或 Assets/YokiFrame 子目录。
    /// </summary>
    /// <param name="candidateDirectory">待检查目录。</param>
    /// <returns>有效包根；不匹配时返回 null。</returns>
    private static string? TryResolvePackageRoot(DirectoryInfo candidateDirectory)
    {
        if (IsPackageRoot(candidateDirectory.FullName))
        {
            return candidateDirectory.FullName;
        }

        var assetsPackageRoot = Path.Combine(candidateDirectory.FullName, "Assets", "YokiFrame");
        return IsPackageRoot(assetsPackageRoot) ? assetsPackageRoot : null;
    }

    /// <summary>
    /// 判断目录是否满足 Installer.Core 认可的新版源包最小标识。
    /// </summary>
    /// <param name="packageRoot">候选包根路径。</param>
    /// <returns>存在包元数据与 Documentation~ 时返回 true。</returns>
    private static bool IsPackageRoot(string packageRoot)
    {
        return File.Exists(Path.Combine(packageRoot, "package.json"))
            && Directory.Exists(Path.Combine(packageRoot, "Documentation~"));
    }

    /// <summary>
    /// 从 `Assets/YokiFrame` 包根回推出 Unity 项目根。
    /// </summary>
    /// <param name="packageRoot">包根目录。</param>
    /// <returns>项目根；无法回推时返回 null。</returns>
    private static string? ResolveProjectRootFromPackageRoot(string? packageRoot)
    {
        if (string.IsNullOrWhiteSpace(packageRoot))
        {
            return null;
        }

        var package = new DirectoryInfo(Path.GetFullPath(packageRoot));
        if (string.Equals(package.Name, "YokiFrame", StringComparison.OrdinalIgnoreCase)
            && package.Parent != null
            && string.Equals(package.Parent.Name, "Assets", StringComparison.OrdinalIgnoreCase)
            && package.Parent.Parent != null)
        {
            return package.Parent.Parent.FullName;
        }

        return null;
    }

    /// <summary>
    /// 从参数数组中读取 `--name value` 或 `--name=value` 形式的选项。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <param name="name">选项名。</param>
    /// <returns>选项值；不存在时返回空字符串。</returns>
    private static string ReadOption(string[] args, string name)
    {
        var prefix = "--" + name + "=";
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return args[index][prefix.Length..];
            }

            if (string.Equals(args[index], "--" + name, StringComparison.OrdinalIgnoreCase) && index + 1 < args.Length)
            {
                return args[index + 1];
            }
        }

        return string.Empty;
    }

}
