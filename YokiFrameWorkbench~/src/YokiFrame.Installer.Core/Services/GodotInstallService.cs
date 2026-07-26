using YokiFrame.Installer.Core.IO;
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
    /// 完成全部只读校验、投影和 patch 计算，确保缺失 Runtime 或不兼容项目会在首次写入前拒绝。
    /// </summary>
    /// <param name="request">typed Godot 安装请求。</param>
    /// <returns>可进入完整 add-on 事务的稳定输入。</returns>
    private GodotInstallPlan PrepareInstall(GodotInstallRequest request)
    {
        var fullProjectRoot = RequireProjectRoot(request.ProjectRoot);
        var projectFilePath = FindSingleTopLevelProjectFile(fullProjectRoot);
        TargetProjectDetector.ValidateGodotProject(projectFilePath);
        var projectSettingsPath = RequireProjectSettings(fullProjectRoot);
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
        var projectSettings = File.ReadAllText(projectSettingsPath);
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
            projectFilePath,
            projectSettingsPath,
            pluginConfigPath,
            pluginScriptPath,
            pluginScriptUidPath,
            runtimeBootstrapPath,
            runtimeBootstrapUidPath,
            mProjectFilePatcher.Patch(File.ReadAllText(projectFilePath)),
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
    /// 选择唯一顶层 csproj，忽略嵌套项目并拒绝缺失或歧义。
    /// </summary>
    /// <param name="projectRoot">已验证的 Godot 项目根。</param>
    /// <returns>唯一顶层 Godot C# 项目文件。</returns>
    private static string FindSingleTopLevelProjectFile(string projectRoot)
    {
        var candidates = Directory.EnumerateFiles(projectRoot, "*", SearchOption.TopDirectoryOnly)
            .Where(static path => string.Equals(Path.GetExtension(path), ".csproj", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (candidates.Length != 1)
        {
            throw new InvalidDataException(
                "Godot project root must contain exactly one top-level csproj, but found " + candidates.Length + ".");
        }

        return candidates[0];
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
}
