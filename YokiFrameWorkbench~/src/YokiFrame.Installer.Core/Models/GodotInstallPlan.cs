namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述一次只读 Godot 安装计划及其已验证投影、路径和 owner 内容快照。
/// </summary>
public sealed class GodotInstallPlan
{
    /// <summary>
    /// 创建已完成全部只读验证的 Godot 安装计划。
    /// </summary>
    /// <param name="request">原始 typed 安装请求。</param>
    /// <param name="projection">已过滤的基础包投影。</param>
    /// <param name="packageUidSidecars">待加入最终投影的 UID sidecar。</param>
    /// <param name="sourcePackageRoot">规范化源包根。</param>
    /// <param name="projectRoot">规范化目标项目根。</param>
    /// <param name="addonRoot">正式受管 Godot add-on 根目录。</param>
    /// <param name="targetPackageRoot">正式受管包根。</param>
    /// <param name="projectFilePath">主 csproj 路径；空 Godot .NET 项目可由安装事务首次生成。</param>
    /// <param name="projectFileWasGenerated">本次计划是否需要首次生成主 csproj。</param>
    /// <param name="projectSettingsPath">project.godot 路径。</param>
    /// <param name="pluginConfigPath">plugin.cfg 路径。</param>
    /// <param name="pluginScriptPath">薄 C# EditorPlugin bootstrap 路径。</param>
    /// <param name="pluginScriptUidPath">EditorPlugin bootstrap UID 路径。</param>
    /// <param name="runtimeBootstrapPath">宿主项目薄 C# bootstrap 路径。</param>
    /// <param name="runtimeBootstrapUidPath">薄 C# bootstrap UID sidecar 路径。</param>
    /// <param name="projectFileContent">patch 后 csproj 内容。</param>
    /// <param name="projectSettingsContent">按 repair/enable 选项计算的 project.godot 内容。</param>
    /// <param name="pluginConfigContent">生成的 plugin.cfg 内容。</param>
    /// <param name="pluginScriptContent">生成的 EditorPlugin bootstrap 内容。</param>
    /// <param name="pluginScriptUidContent">保留或生成的 EditorPlugin bootstrap UID 内容。</param>
    /// <param name="runtimeBootstrapContent">生成的宿主项目薄 C# bootstrap 内容。</param>
    /// <param name="runtimeBootstrapUidContent">保留或生成的 bootstrap UID 内容。</param>
    internal GodotInstallPlan(
        GodotInstallRequest request,
        PackageProjection projection,
        IReadOnlyList<GodotUidSidecar> packageUidSidecars,
        string sourcePackageRoot,
        string projectRoot,
        string addonRoot,
        string targetPackageRoot,
        string projectFilePath,
        bool projectFileWasGenerated,
        string projectSettingsPath,
        string pluginConfigPath,
        string pluginScriptPath,
        string pluginScriptUidPath,
        string runtimeBootstrapPath,
        string runtimeBootstrapUidPath,
        string projectFileContent,
        string projectSettingsContent,
        string pluginConfigContent,
        string pluginScriptContent,
        string pluginScriptUidContent,
        string runtimeBootstrapContent,
        string runtimeBootstrapUidContent)
    {
        Request = request;
        Projection = projection;
        PackageUidSidecars = packageUidSidecars;
        SourcePackageRoot = sourcePackageRoot;
        ProjectRoot = projectRoot;
        AddonRoot = addonRoot;
        TargetPackageRoot = targetPackageRoot;
        ProjectFilePath = projectFilePath;
        ProjectFileWasGenerated = projectFileWasGenerated;
        ProjectSettingsPath = projectSettingsPath;
        PluginConfigPath = pluginConfigPath;
        PluginScriptPath = pluginScriptPath;
        PluginScriptUidPath = pluginScriptUidPath;
        RuntimeBootstrapPath = runtimeBootstrapPath;
        RuntimeBootstrapUidPath = runtimeBootstrapUidPath;
        ProjectFileContent = projectFileContent;
        ProjectSettingsContent = projectSettingsContent;
        PluginConfigContent = pluginConfigContent;
        PluginScriptContent = pluginScriptContent;
        PluginScriptUidContent = pluginScriptUidContent;
        RuntimeBootstrapContent = runtimeBootstrapContent;
        RuntimeBootstrapUidContent = runtimeBootstrapUidContent;
    }

    /// <summary>获取原始 typed 安装请求。</summary>
    public GodotInstallRequest Request { get; }

    /// <summary>获取基础受控包投影。</summary>
    public PackageProjection Projection { get; }

    /// <summary>获取即将加入最终投影和 owner manifest 的 UID sidecar。</summary>
    public IReadOnlyList<GodotUidSidecar> PackageUidSidecars { get; }

    /// <summary>获取规范化源包根。</summary>
    public string SourcePackageRoot { get; }

    /// <summary>获取规范化目标项目根。</summary>
    public string ProjectRoot { get; }

    /// <summary>获取由 Installer 整目录替换的正式 Godot add-on 根。</summary>
    public string AddonRoot { get; }

    /// <summary>获取正式受管包根。</summary>
    public string TargetPackageRoot { get; }

    /// <summary>获取主 csproj 路径。</summary>
    public string ProjectFilePath { get; }

    /// <summary>
    /// 获取本次计划是否会在目标项目根首次生成主 csproj。
    /// </summary>
    public bool ProjectFileWasGenerated { get; }

    /// <summary>获取 project.godot 路径。</summary>
    public string ProjectSettingsPath { get; }

    /// <summary>获取 plugin.cfg 路径。</summary>
    public string PluginConfigPath { get; }

    /// <summary>获取薄 C# EditorPlugin bootstrap 路径。</summary>
    public string PluginScriptPath { get; }

    /// <summary>获取 EditorPlugin bootstrap UID 路径。</summary>
    public string PluginScriptUidPath { get; }

    /// <summary>获取升级时由 Installer 删除的旧 plugin.gd 路径。</summary>
    public string LegacyPluginScriptPath => Path.Combine(Path.GetDirectoryName(PluginConfigPath)!, "plugin.gd");

    /// <summary>获取升级时由 Installer 删除的旧 plugin.gd.uid 路径。</summary>
    public string LegacyPluginScriptUidPath => LegacyPluginScriptPath + ".uid";

    /// <summary>获取宿主项目薄 C# bootstrap 路径。</summary>
    public string RuntimeBootstrapPath { get; }

    /// <summary>获取薄 C# bootstrap UID sidecar 路径。</summary>
    public string RuntimeBootstrapUidPath { get; }

    /// <summary>获取是否维护 project.godot owner 项。</summary>
    public bool RepairProjectSettings => Request.RepairProjectSettings;

    /// <summary>获取 repair 开启时是否登记 editor plugin。</summary>
    public bool EnablePlugin => Request.EnablePlugin;

    /// <summary>获取安装前已验证的项目缓存 Runtime profile。</summary>
    public string RuntimeProfile => Request.RuntimeProfile;

    /// <summary>获取 legacy 包接管策略。</summary>
    public UnmanagedPackagePolicy UnmanagedPackagePolicy => Request.UnmanagedPackagePolicy;

    /// <summary>获取 patch 后 csproj 内容。</summary>
    internal string ProjectFileContent { get; }

    /// <summary>获取 repair 开启时待提交的 project.godot 内容。</summary>
    internal string ProjectSettingsContent { get; }

    /// <summary>获取生成的 plugin.cfg 内容。</summary>
    internal string PluginConfigContent { get; }

    /// <summary>获取生成的 EditorPlugin bootstrap 内容。</summary>
    internal string PluginScriptContent { get; }

    /// <summary>获取保留或生成的 EditorPlugin bootstrap UID 内容。</summary>
    internal string PluginScriptUidContent { get; }

    /// <summary>获取生成的宿主项目薄 C# bootstrap 内容。</summary>
    internal string RuntimeBootstrapContent { get; }

    /// <summary>获取保留或生成的 bootstrap UID 内容。</summary>
    internal string RuntimeBootstrapUidContent { get; }
}
