using YokiFrame.Installer.Core.IO;
using System.Text;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 协调 Godot 源码投影、项目 Runtime 缓存门控、入口生成和完整 add-on 安装事务。
/// </summary>
public sealed class GodotInstallService
{
    private const string ADDON_DIRECTORY = "yokiframe";
    private const string PACKAGE_DIRECTORY = "YokiFrame";
    private const string EDITOR_BOOTSTRAP_FILE_NAME = "YokiFrameGodotEditorPlugin.cs";
    private const string EDITOR_BOOTSTRAP_RESOURCE_PATH =
        "res://addons/yokiframe/YokiFrameGodotEditorPlugin.cs";
    private const string RUNTIME_BOOTSTRAP_FILE_NAME = "YokiFrameGodotBootstrap.cs";
    private const string RUNTIME_BOOTSTRAP_RESOURCE_PATH =
        "res://addons/yokiframe/YokiFrameGodotBootstrap.cs";
    private const string DEFAULT_GODOT_SDK = "Godot.NET.Sdk/4.7.0";
    private const string DEFAULT_TARGET_FRAMEWORK = "net8.0";
    private const string DEFAULT_PROJECT_FILE_NAME = "GodotProject";

    private readonly GodotPackageProjectionBuilder mProjectionBuilder = new();
    private readonly GodotPluginEntryPointBuilder mPluginBuilder = new();
    private readonly GodotProjectFilePatcher mProjectFilePatcher = new();
    private readonly GodotProjectSettingsPatcher mProjectSettingsPatcher = new();
    private readonly GodotUidProjectionBuilder mUidProjectionBuilder = new();
    private readonly GodotUidSidecarBuilder mUidSidecarBuilder = new();
    private readonly ProjectKitReferenceScanner mKitReferenceScanner = new();
    private readonly RuntimeCacheBindingValidator mRuntimeCacheValidator = new();
    private readonly GodotAddonInstallTransactionService mTransactionService;

    /// <summary>
    /// 创建生产 Godot 安装服务，不启用故障注入。
    /// </summary>
    public GodotInstallService()
        : this(NoOpFaultInjector.Instance)
    {
    }

    /// <summary>
    /// 创建带提交检查点观察器的测试安装服务。
    /// </summary>
    /// <param name="faultInjector">内部测试故障注入器。</param>
    internal GodotInstallService(IGodotInstallFaultInjector faultInjector)
    {
        mTransactionService = new GodotAddonInstallTransactionService(faultInjector);
    }

    /// <summary>
    /// 只读验证项目、Runtime 缓存、投影、UID 和 owner 配置，并生成稳定安装计划。
    /// </summary>
    /// <param name="request">typed Godot 安装请求。</param>
    /// <returns>不修改目标项目的安装计划。</returns>
    public GodotInstallPlan CreatePlan(GodotInstallRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return PrepareInstall(request);
    }

    /// <summary>
    /// 使用兼容默认选项安装 Godot 投影；默认修复 project.godot 并登记 editor plugin。
    /// </summary>
    /// <param name="sourcePackageRoot">YokiFrame 完整源包根。</param>
    /// <param name="projectRoot">目标 Godot 项目根。</param>
    /// <param name="runtimeProfile">项目缓存中必须已手动生成的 Workbench Runtime profile。</param>
    /// <param name="policy">保留的调用兼容策略；Godot 更新始终整目录替换 add-on。</param>
    /// <returns>成功提交的 add-on、包路径和项目 owner 文件。</returns>
    public GodotInstallResult Execute(
        string sourcePackageRoot,
        string projectRoot,
        string runtimeProfile,
        UnmanagedPackagePolicy policy)
    {
        GodotInstallRequest request = new(
            sourcePackageRoot,
            projectRoot,
            runtimeProfile,
            repairProjectSettings: true,
            enablePlugin: true,
            policy);
        return Execute(request);
    }

    /// <summary>
    /// 重新执行全部只读门控后，整目录替换 `addons/yokiframe` 并在失败时恢复旧目录和项目 owner 文件。
    /// </summary>
    /// <param name="request">typed Godot 安装请求。</param>
    /// <returns>成功提交的稳定安装结果。</returns>
    public GodotInstallResult Execute(GodotInstallRequest request)
    {
        return mTransactionService.Execute(CreatePlan(request));
    }

    /// <summary>
    /// 在主项目 Tools 构建结束后重新维护 project.godot owner 项，抵御 Godot 在扫描失败或重载竞态中移除插件登记。
    /// </summary>
    /// <param name="plan">已完成 add-on、主项目和初始 project.godot 提交的安装计划。</param>
    /// <remarks>
    /// 该方法只在计划允许修复设置且要求启用插件时写入；每次先重读最新文件，再通过结构化 patch 保留 Godot 或用户刚刚写入的非 owner 内容。
    /// </remarks>
    public void EnsurePluginEnabled(GodotInstallPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.RepairProjectSettings || !plan.EnablePlugin)
        {
            return;
        }

        var currentSettings = File.ReadAllText(plan.ProjectSettingsPath);
        var repairedSettings = mProjectSettingsPatcher.Patch(currentSettings, enablePlugin: true);
        if (string.Equals(currentSettings, repairedSettings, StringComparison.Ordinal))
        {
            return;
        }

        WriteProjectSettingsAtomically(plan.ProjectSettingsPath, repairedSettings);
    }

    /// <summary>
    /// 完成全部只读校验、投影和 patch 计算，确保缺失 Runtime 或不兼容项目会在首次写入前拒绝。
    /// </summary>
    /// <param name="request">typed Godot 安装请求。</param>
    /// <returns>可进入完整 add-on 事务的稳定输入。</returns>
    private GodotInstallPlan PrepareInstall(GodotInstallRequest request)
    {
        var fullProjectRoot = RequireProjectRoot(request.ProjectRoot);
        var projectSettingsPath = RequireProjectSettings(fullProjectRoot);
        var projectSettings = File.ReadAllText(projectSettingsPath);
        var projectFile = ResolveProjectFile(fullProjectRoot, projectSettingsPath);
        var addonRoot = InstallerPathGuard.CombineInside(fullProjectRoot, "addons", ADDON_DIRECTORY);
        var targetPackageRoot = InstallerPathGuard.CombineInside(addonRoot, "package", PACKAGE_DIRECTORY);
        mRuntimeCacheValidator.Validate(fullProjectRoot, request.SourcePackageRoot, request.RuntimeProfile);
        var projection = mProjectionBuilder.Build(request.SourcePackageRoot, request.RuntimeProfile);
        RejectUnsupportedKitReferences(fullProjectRoot, projection.SourcePackageRoot);
        var packageUidSidecars = mUidProjectionBuilder.Build(projection, targetPackageRoot);
        var pluginConfigPath = InstallerPathGuard.CombineInside(addonRoot, "plugin.cfg");
        var pluginScriptPath = InstallerPathGuard.CombineInside(addonRoot, EDITOR_BOOTSTRAP_FILE_NAME);
        var pluginScriptUidPath = InstallerPathGuard.CombineInside(
            addonRoot,
            EDITOR_BOOTSTRAP_FILE_NAME + ".uid");
        var runtimeBootstrapPath = InstallerPathGuard.CombineInside(addonRoot, RUNTIME_BOOTSTRAP_FILE_NAME);
        var runtimeBootstrapUidPath = InstallerPathGuard.CombineInside(
            addonRoot,
            RUNTIME_BOOTSTRAP_FILE_NAME + ".uid");
        var pluginScriptUid = mUidSidecarBuilder.Build(
            EDITOR_BOOTSTRAP_FILE_NAME + ".uid",
            EDITOR_BOOTSTRAP_RESOURCE_PATH,
            pluginScriptUidPath);
        var runtimeBootstrapUid = mUidSidecarBuilder.Build(
            RUNTIME_BOOTSTRAP_FILE_NAME + ".uid",
            RUNTIME_BOOTSTRAP_RESOURCE_PATH,
            runtimeBootstrapUidPath);
        return new GodotInstallPlan(
            request,
            projection,
            packageUidSidecars,
            projection.SourcePackageRoot,
            fullProjectRoot,
            addonRoot,
            targetPackageRoot,
            projectFile.Path,
            projectFile.WasGenerated,
            projectSettingsPath,
            pluginConfigPath,
            pluginScriptPath,
            pluginScriptUidPath,
            runtimeBootstrapPath,
            runtimeBootstrapUidPath,
            mProjectFilePatcher.Patch(projectFile.Content),
            request.RepairProjectSettings
                ? mProjectSettingsPatcher.Patch(projectSettings, request.EnablePlugin)
                : projectSettings,
            mPluginBuilder.BuildPluginConfig(),
            mPluginBuilder.BuildEditorBootstrapScript(),
            pluginScriptUid.Content,
            mPluginBuilder.BuildRuntimeBootstrapScript(),
            runtimeBootstrapUid.Content);
    }

    /// <summary>
    /// 规范化并验证目标 Godot 项目根存在。
    /// </summary>
    /// <param name="projectRoot">输入项目根。</param>
    /// <returns>规范化绝对路径。</returns>
    private static string RequireProjectRoot(string projectRoot)
    {
        var fullProjectRoot = InstallerPathGuard.RequireFullPath(projectRoot, nameof(projectRoot));
        if (!Directory.Exists(fullProjectRoot))
        {
            throw new DirectoryNotFoundException("Godot project root was not found: " + fullProjectRoot);
        }

        return fullProjectRoot;
    }

    /// <summary>
    /// 以同目录临时文件、刷新到磁盘和原子替换提交最新 project.godot，避免重写过程中留下半份设置。
    /// </summary>
    /// <param name="targetPath">已通过项目路径守卫验证的 project.godot 路径。</param>
    /// <param name="content">结构化 patch 后的完整项目设置文本。</param>
    private static void WriteProjectSettingsAtomically(string targetPath, string content)
    {
        var temporaryPath = targetPath + ".yokiframe-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            using (StreamWriter writer = new(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <summary>
    /// 选择唯一顶层 csproj；空 Godot .NET 项目缺失时生成可供 Godot 编辑器继续维护的主项目文件。
    /// </summary>
    /// <param name="projectRoot">已验证的 Godot 项目根。</param>
    /// <param name="projectSettingsPath">project.godot 绝对路径。</param>
    /// <returns>主项目路径、内容和是否需要首次生成的解析结果。</returns>
    private static GodotProjectFileResolution ResolveProjectFile(string projectRoot, string projectSettingsPath)
    {
        var candidates = Directory.EnumerateFiles(projectRoot, "*", SearchOption.TopDirectoryOnly)
            .Where(static path => string.Equals(Path.GetExtension(path), ".csproj", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (candidates.Length > 1)
        {
            throw new InvalidDataException(
                "Godot .NET 项目必须且只能包含一个顶层 .csproj；当前发现 "
                + candidates.Length + " 个。");
        }

        if (candidates.Length == 1)
        {
            var projectPath = candidates[0];
            var projectContent = File.ReadAllText(projectPath);
            TargetProjectDetector.ValidateGodotProjectContent(projectContent, projectPath);
            return new GodotProjectFileResolution(projectPath, projectContent, WasGenerated: false);
        }

        if (!TargetProjectDetector.HasGodotDotNetEvidence(projectRoot, projectSettingsPath))
        {
            throw new InvalidDataException(
                "Godot 项目未发现主 .csproj，也未发现 Godot .NET 证据；YokiFrame 仅支持 Godot .NET 项目。");
        }

        TargetProjectDetector.ValidateGodotProjectFeatureVersion(projectSettingsPath);
        var generatedProjectPath = GetGeneratedProjectPath(projectRoot, projectSettingsPath);
        var generatedProjectContent = CreateGeneratedProjectContent();
        TargetProjectDetector.ValidateGodotProjectContent(generatedProjectContent, generatedProjectPath);
        return new GodotProjectFileResolution(generatedProjectPath, generatedProjectContent, WasGenerated: true);
    }

    /// <summary>
    /// 根据 project.godot 的 assembly_name 计算稳定的主项目文件路径，缺失时回退到项目目录名。
    /// </summary>
    /// <param name="projectRoot">Godot 项目根目录。</param>
    /// <param name="projectSettingsPath">project.godot 绝对路径。</param>
    /// <returns>受路径守卫约束的主 csproj 路径。</returns>
    private static string GetGeneratedProjectPath(string projectRoot, string projectSettingsPath)
    {
        var projectName = ReadGodotAssemblyName(projectSettingsPath);
        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = new DirectoryInfo(projectRoot).Name;
        }

        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeName = new string(projectName
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();
        if (safeName.Length == 0 || safeName is "." or "..")
        {
            safeName = DEFAULT_PROJECT_FILE_NAME;
        }

        return InstallerPathGuard.CombineInside(projectRoot, safeName + ".csproj");
    }

    /// <summary>
    /// 读取 project.godot [dotnet] section 中的 assembly_name，用于匹配 Godot 默认项目文件名。
    /// </summary>
    /// <param name="projectSettingsPath">project.godot 绝对路径。</param>
    /// <returns>未转义的 assembly_name；缺失时返回 null。</returns>
    private static string? ReadGodotAssemblyName(string projectSettingsPath)
    {
        var inDotNetSection = false;
        foreach (var rawLine in File.ReadLines(projectSettingsPath))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                inDotNetSection = string.Equals(line, "[dotnet]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inDotNetSection)
            {
                continue;
            }

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0
                || !string.Equals(line[..equalsIndex].Trim(), "project/assembly_name", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = line[(equalsIndex + 1)..].Trim();
            return value.Length >= 2 && value[0] == '"' && value[^1] == '"'
                ? value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal)
                : value;
        }

        return null;
    }

    /// <summary>
    /// 创建与 Godot 4.7 .NET 编辑器生成文件兼容的最小主项目 XML，后续由 owner patch 追加引用。
    /// </summary>
    /// <returns>待写入主 csproj 的完整 XML。</returns>
    private static string CreateGeneratedProjectContent()
    {
        return "<Project Sdk=\"" + DEFAULT_GODOT_SDK + "\">\n"
            + "  <PropertyGroup>\n"
            + "    <TargetFramework>" + DEFAULT_TARGET_FRAMEWORK + "</TargetFramework>\n"
            + "    <EnableDynamicLoading>true</EnableDynamicLoading>\n"
            + "    <DefineConstants>$(DefineConstants);GODOT</DefineConstants>\n"
            + "  </PropertyGroup>\n"
            + "</Project>\n";
    }

    /// <summary>
    /// 验证 project.godot 存在并返回受路径守卫约束的路径。
    /// </summary>
    /// <param name="projectRoot">已验证的项目根。</param>
    /// <returns>project.godot 完整路径。</returns>
    private static string RequireProjectSettings(string projectRoot)
    {
        var path = InstallerPathGuard.CombineInside(projectRoot, "project.godot");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Godot project.godot was not found.", path);
        }

        return path;
    }

    /// <summary>
    /// 在首次写入前拒绝用户脚本对当前发布包未提供 Kit API 的引用，并保留准确文件与行号。
    /// </summary>
    /// <param name="projectRoot">已验证的 Godot 项目根。</param>
    /// <param name="sourcePackageRoot">已验证的 YokiFrame 源包根。</param>
    private void RejectUnsupportedKitReferences(string projectRoot, string sourcePackageRoot)
    {
        var conflicts = mKitReferenceScanner.Scan(projectRoot, sourcePackageRoot);
        if (conflicts.Count > 0)
        {
            throw new UnsupportedKitReferenceException(conflicts);
        }
    }

    /// <summary>
    /// 生产环境无操作故障注入器。
    /// </summary>
    private sealed class NoOpFaultInjector : IGodotInstallFaultInjector
    {
        /// <summary>获取共享无操作实例。</summary>
        public static NoOpFaultInjector Instance { get; } = new();

        /// <summary>
        /// 忽略生产环境的事务检查点。
        /// </summary>
        /// <param name="checkpoint">刚完成的稳定边界。</param>
        public void OnCheckpoint(GodotInstallCheckpoint checkpoint)
        {
        }
    }

    /// <summary>
    /// 保存主项目文件解析结果，区分已有项目与安装事务将首次生成的项目。
    /// </summary>
    private sealed record GodotProjectFileResolution(string Path, string Content, bool WasGenerated);
}
