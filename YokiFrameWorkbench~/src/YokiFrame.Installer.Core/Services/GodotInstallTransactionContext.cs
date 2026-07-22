using YokiFrame.Installer.Core.IO;
using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 保存一次 Godot 整目录替换事务的受守卫路径、提交状态和诊断位置。
/// </summary>
internal sealed class GodotInstallTransactionContext
{
    /// <summary>
    /// 根据只读安装计划创建同项目卷内的 staging、备份、生成与诊断路径。
    /// </summary>
    /// <param name="plan">已经完成全部只读验证的 Godot 安装计划。</param>
    public GodotInstallTransactionContext(GodotInstallPlan plan)
    {
        TransactionId = Guid.NewGuid().ToString("N");
        AddonRoot = plan.AddonRoot;
        var installerRoot = InstallerPathGuard.CombineInside(plan.ProjectRoot, ".yokiframe", "installer");
        TransactionRoot = InstallerPathGuard.CombineInside(installerRoot, "godot", TransactionId);
        StagingAddonRoot = InstallerPathGuard.CombineInside(TransactionRoot, "staging", "addon", "yokiframe");
        BackupAddonRoot = InstallerPathGuard.CombineInside(TransactionRoot, "backup", "addon", "yokiframe");
        GeneratedPackageRoot = InstallerPathGuard.CombineInside(TransactionRoot, "generated-package");
        GeneratedAddonRoot = InstallerPathGuard.CombineInside(TransactionRoot, "generated-addon");
        DiagnosticEvidencePath = InstallerPathGuard.CombineInside(
            installerRoot,
            "diagnostics",
            TransactionId + ".json");
        ProjectFiles = CreateProjectFiles(plan, TransactionRoot);
    }

    /// <summary>获取本次事务的唯一标识。</summary>
    public string TransactionId { get; }

    /// <summary>获取事务根目录。</summary>
    public string TransactionRoot { get; }

    /// <summary>获取正式 `addons/yokiframe` 根目录。</summary>
    public string AddonRoot { get; }

    /// <summary>获取完整新 add-on 的 staging 目录。</summary>
    public string StagingAddonRoot { get; }

    /// <summary>获取完整旧 add-on 的备份目录。</summary>
    public string BackupAddonRoot { get; }

    /// <summary>获取包内 UID sidecar 的事务生成目录。</summary>
    public string GeneratedPackageRoot { get; }

    /// <summary>获取薄启动入口的事务生成目录。</summary>
    public string GeneratedAddonRoot { get; }

    /// <summary>获取失败诊断 JSON 路径。</summary>
    public string DiagnosticEvidencePath { get; }

    /// <summary>获取需要与 add-on 一起回滚的外部项目 owner 文件。</summary>
    public IReadOnlyList<GodotProjectFileTransactionEntry> ProjectFiles { get; }

    /// <summary>获取或设置事务开始时 add-on 是否已存在。</summary>
    public bool AddonOriginallyExists { get; set; }

    /// <summary>获取或设置旧 add-on 是否已移入备份区。</summary>
    public bool ExistingAddonBackedUp { get; set; }

    /// <summary>获取或设置 staging add-on 是否已成为正式目录。</summary>
    public bool AddonCommitted { get; set; }

    /// <summary>获取或设置最后完成的稳定提交检查点。</summary>
    public GodotInstallCheckpoint? Checkpoint { get; set; }

    /// <summary>
    /// 创建项目外部 owner 文件的 staging 与备份描述；插件入口已经包含在完整 add-on 投影中。
    /// </summary>
    /// <param name="plan">已完成只读验证的安装计划。</param>
    /// <param name="transactionRoot">同项目卷内事务根。</param>
    /// <returns>按稳定提交顺序排列的项目文件事务项。</returns>
    private static IReadOnlyList<GodotProjectFileTransactionEntry> CreateProjectFiles(
        GodotInstallPlan plan,
        string transactionRoot)
    {
        var stagingRoot = InstallerPathGuard.CombineInside(transactionRoot, "staging", "project-files");
        var backupRoot = InstallerPathGuard.CombineInside(transactionRoot, "backup", "project-files");
        List<GodotProjectFileTransactionEntry> files = new()
        {
            CreateEntry(
                "project.csproj",
                plan.ProjectFilePath,
                plan.ProjectFileContent,
                GodotInstallCheckpoint.ProjectFileCommitted,
                stagingRoot,
                backupRoot)
        };
        if (plan.RepairProjectSettings)
        {
            files.Add(CreateEntry(
                "project.godot",
                plan.ProjectSettingsPath,
                plan.ProjectSettingsContent,
                GodotInstallCheckpoint.ProjectSettingsCommitted,
                stagingRoot,
                backupRoot));
        }

        return files;
    }

    /// <summary>
    /// 创建一个项目 owner 文件的 staging、备份和提交描述。
    /// </summary>
    /// <param name="transactionName">事务内使用的稳定文件名。</param>
    /// <param name="targetPath">正式目标文件。</param>
    /// <param name="content">已预计算的完整文本。</param>
    /// <param name="checkpoint">提交后的稳定检查点。</param>
    /// <param name="stagingRoot">项目文件 staging 根。</param>
    /// <param name="backupRoot">项目文件备份根。</param>
    /// <returns>完整的项目文件事务项。</returns>
    private static GodotProjectFileTransactionEntry CreateEntry(
        string transactionName,
        string targetPath,
        string content,
        GodotInstallCheckpoint checkpoint,
        string stagingRoot,
        string backupRoot)
    {
        return new GodotProjectFileTransactionEntry(
            targetPath,
            InstallerPathGuard.CombineInside(stagingRoot, transactionName),
            InstallerPathGuard.CombineInside(backupRoot, transactionName),
            content,
            checkpoint);
    }
}

/// <summary>
/// 描述一个位于 add-on 根外、需要随 Godot 安装一起回滚的项目 owner 文件。
/// </summary>
internal sealed class GodotProjectFileTransactionEntry
{
    /// <summary>
    /// 创建项目 owner 文件事务项。
    /// </summary>
    /// <param name="targetPath">正式目标文件。</param>
    /// <param name="stagedPath">同项目卷内 staging 文件。</param>
    /// <param name="backupPath">原始文件备份。</param>
    /// <param name="content">待提交完整文本。</param>
    /// <param name="checkpoint">提交后的稳定检查点。</param>
    public GodotProjectFileTransactionEntry(
        string targetPath,
        string stagedPath,
        string backupPath,
        string content,
        GodotInstallCheckpoint checkpoint)
    {
        TargetPath = targetPath;
        StagedPath = stagedPath;
        BackupPath = backupPath;
        Content = content;
        Checkpoint = checkpoint;
    }

    /// <summary>获取正式目标文件路径。</summary>
    public string TargetPath { get; }

    /// <summary>获取 staging 文件路径。</summary>
    public string StagedPath { get; }

    /// <summary>获取原始文件备份路径。</summary>
    public string BackupPath { get; }

    /// <summary>获取待提交完整文本。</summary>
    public string Content { get; }

    /// <summary>获取提交后的稳定检查点。</summary>
    public GodotInstallCheckpoint Checkpoint { get; }

    /// <summary>获取或设置事务开始时目标文件是否存在。</summary>
    public bool OriginalExists { get; set; }

    /// <summary>获取或设置 staging 内容是否已经提交到正式路径。</summary>
    public bool Committed { get; set; }
}
