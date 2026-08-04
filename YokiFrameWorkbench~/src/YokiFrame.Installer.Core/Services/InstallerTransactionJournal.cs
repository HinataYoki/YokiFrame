using System.Text.Json;
using YokiFrame.Installer.Core.IO;

namespace YokiFrame.Installer.Core.Services;

/// <summary>
/// 描述 Installer 事务在进程外仍可识别的稳定阶段。
/// </summary>
internal enum InstallerTransactionPhase
{
    /// <summary>事务根已创建，尚未完成 staging。</summary>
    Prepared,

    /// <summary>staging 已通过完整投影和 hash 校验。</summary>
    StagingVerified,

    /// <summary>旧正式目标已移动到备份区。</summary>
    ExistingTargetBackedUp,

    /// <summary>新目标已成为正式目录。</summary>
    TargetCommitted,

    /// <summary>至少一个 add-on 外部项目 owner 文件已提交。</summary>
    ProjectFilesCommitted,

    /// <summary>正式目标已完成 post-verify，可以只清理事务目录。</summary>
    PostVerified,

    /// <summary>自动恢复无法安全完成，需要保留证据并阻止下一次写入。</summary>
    RecoveryRequired
}

/// <summary>
/// 保存一个 Installer 事务的路径、原始目标状态和持久 checkpoint。
/// </summary>
internal sealed class InstallerTransactionJournal
{
    private const int SCHEMA_VERSION = 1;
    private const string JOURNAL_FILE_NAME = "journal.json";
    private const string TRANSACTIONS_DIRECTORY_NAME = "transactions";

    private InstallerTransactionJournal(
        string projectRoot,
        string journalRoot,
        string journalPath,
        InstallerTransactionJournalRecord record)
    {
        ProjectRoot = projectRoot;
        JournalRoot = journalRoot;
        JournalPath = journalPath;
        Record = record;
    }

    /// <summary>
    /// 获取事务所属的规范化项目根。
    /// </summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// 获取 journal 和恢复辅助文件所在目录。
    /// </summary>
    public string JournalRoot { get; }

    /// <summary>
    /// 获取持久 journal 文件路径。
    /// </summary>
    public string JournalPath { get; }

    /// <summary>
    /// 获取当前持久记录。
    /// </summary>
    public InstallerTransactionJournalRecord Record { get; private set; }

    /// <summary>
    /// 创建并持久化一个新事务 journal；路径以项目相对值保存，避免跨机器泄露绝对路径。
    /// </summary>
    /// <param name="projectRoot">规范化项目根。</param>
    /// <param name="kind">事务类型，例如 `unity-package`。</param>
    /// <param name="transactionId">事务标识。</param>
    /// <param name="targetPath">正式目标路径。</param>
    /// <param name="stagingPath">staging 路径。</param>
    /// <param name="backupPath">备份路径。</param>
    /// <param name="targetOriginallyExists">事务开始时目标是否存在。</param>
    /// <param name="cleanupRoot">事务辅助目录；恢复完成后可安全删除。</param>
    /// <param name="projectFiles">需要随目录事务恢复的外部项目文件。</param>
    /// <returns>已经落盘的 journal。</returns>
    public static InstallerTransactionJournal Create(
        string projectRoot,
        string kind,
        string transactionId,
        string targetPath,
        string stagingPath,
        string backupPath,
        bool targetOriginallyExists,
        string? cleanupRoot = null,
        IReadOnlyList<InstallerProjectFileJournalEntry>? projectFiles = null)
    {
        var fullProjectRoot = InstallerPathGuard.RequireFullPath(projectRoot, nameof(projectRoot));
        var fullTargetPath = InstallerPathGuard.RequireFullPath(targetPath, nameof(targetPath));
        var fullStagingPath = InstallerPathGuard.RequireFullPath(stagingPath, nameof(stagingPath));
        var fullBackupPath = InstallerPathGuard.RequireFullPath(backupPath, nameof(backupPath));
        _ = InstallerPathGuard.CombineInside(fullProjectRoot, Path.GetRelativePath(fullProjectRoot, fullTargetPath));
        _ = InstallerPathGuard.CombineInside(fullProjectRoot, Path.GetRelativePath(fullProjectRoot, fullStagingPath));
        _ = InstallerPathGuard.CombineInside(fullProjectRoot, Path.GetRelativePath(fullProjectRoot, fullBackupPath));
        var cleanupRelativePath = cleanupRoot == null
            ? null
            : ToRelativePath(fullProjectRoot, InstallerPathGuard.RequireFullPath(cleanupRoot, nameof(cleanupRoot)));
        if (cleanupRelativePath != null)
        {
            _ = InstallerPathGuard.CombineInside(fullProjectRoot, cleanupRelativePath);
        }

        var transactionRoot = InstallerPathGuard.CombineInside(
            fullProjectRoot,
            ".yokiframe",
            "installer",
            TRANSACTIONS_DIRECTORY_NAME,
            kind,
            transactionId);
        var journalPath = InstallerPathGuard.CombineInside(transactionRoot, JOURNAL_FILE_NAME);
        Directory.CreateDirectory(transactionRoot);
        var record = new InstallerTransactionJournalRecord(
            SCHEMA_VERSION,
            kind,
            transactionId,
            ToRelativePath(fullProjectRoot, fullTargetPath),
            ToRelativePath(fullProjectRoot, fullStagingPath),
            ToRelativePath(fullProjectRoot, fullBackupPath),
            targetOriginallyExists,
            InstallerTransactionPhase.Prepared,
            cleanupRelativePath,
            NormalizeProjectFiles(fullProjectRoot, projectFiles));
        var journal = new InstallerTransactionJournal(fullProjectRoot, transactionRoot, journalPath, record);
        journal.Write();
        return journal;
    }

    /// <summary>
    /// 从项目 journal 根读取所有可恢复事务；格式损坏时阻止静默清理。
    /// </summary>
    /// <param name="projectRoot">目标项目根。</param>
    /// <returns>按事务目录名稳定排序的 journal。</returns>
    public static IReadOnlyList<InstallerTransactionJournal> ReadAll(string projectRoot)
    {
        var fullProjectRoot = InstallerPathGuard.RequireFullPath(projectRoot, nameof(projectRoot));
        var root = InstallerPathGuard.CombineInside(
            fullProjectRoot,
            ".yokiframe",
            "installer",
            TRANSACTIONS_DIRECTORY_NAME);
        if (!Directory.Exists(root))
        {
            return Array.Empty<InstallerTransactionJournal>();
        }

        List<InstallerTransactionJournal> journals = new();
        foreach (var kindRoot in Directory.EnumerateDirectories(root).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var transactionRoot in Directory.EnumerateDirectories(kindRoot).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                var journalPath = InstallerPathGuard.CombineInside(transactionRoot, JOURNAL_FILE_NAME);
                if (!File.Exists(journalPath))
                {
                    throw new InvalidDataException("Installer transaction journal is missing: " + journalPath);
                }

                journals.Add(Read(fullProjectRoot, transactionRoot, journalPath));
            }
        }

        return journals;
    }

    /// <summary>
    /// 推进事务阶段并原子替换 journal。
    /// </summary>
    /// <param name="phase">刚完成的稳定阶段。</param>
    public void Advance(InstallerTransactionPhase phase)
    {
        Record = Record with { Phase = phase };
        Write();
    }

    /// <summary>
    /// 记录一个外部项目 owner 文件已经提交，保留其原始存在性供崩溃恢复使用。
    /// </summary>
    /// <param name="targetPath">正式项目文件路径。</param>
    /// <param name="committed">是否已提交。</param>
    public void MarkProjectFileCommitted(string targetPath, bool committed = true)
    {
        var targetRelativePath = ToSafeRelativePath(ProjectRoot, targetPath);
        var found = false;
        var files = Record.ProjectFiles.ToArray();
        for (var index = 0; index < files.Length; index++)
        {
            if (!string.Equals(files[index].TargetRelativePath, targetRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            files[index] = files[index] with { Committed = committed };
            found = true;
            break;
        }
        if (!found)
        {
            throw new InvalidOperationException("Installer journal does not contain project file: " + targetRelativePath);
        }

        Record = Record with
        {
            ProjectFiles = files,
            Phase = InstallerTransactionPhase.ProjectFilesCommitted
        };
        Write();
    }

    /// <summary>
    /// 标记事务需要人工或更高层恢复，保留所有路径证据。
    /// </summary>
    public void MarkRecoveryRequired()
    {
        Advance(InstallerTransactionPhase.RecoveryRequired);
    }

    /// <summary>
    /// 删除已完成事务的 journal 目录；正式目标不在该目录中。
    /// </summary>
    public void Complete()
    {
        if (Directory.Exists(JournalRoot))
        {
            Directory.Delete(JournalRoot, recursive: true);
        }
    }

    /// <summary>
    /// 将项目相对路径解析回受守卫的绝对路径。
    /// </summary>
    /// <param name="relativePath">journal 中保存的正斜杠相对路径。</param>
    /// <returns>项目内绝对路径。</returns>
    public string ResolvePath(string relativePath)
    {
        return InstallerPathGuard.CombineInside(
            ProjectRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// 读取一个已存在 journal 并校验 schema、事务标识和路径字段。
    /// </summary>
    private static InstallerTransactionJournal Read(
        string projectRoot,
        string journalRoot,
        string journalPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(journalPath));
        var root = document.RootElement;
        var schemaVersion = root.GetProperty("schemaVersion").GetInt32();
        if (schemaVersion != SCHEMA_VERSION)
        {
            throw new InvalidDataException("Unsupported Installer transaction journal schema: " + schemaVersion);
        }

        var record = new InstallerTransactionJournalRecord(
            schemaVersion,
            root.GetProperty("kind").GetString() ?? throw new InvalidDataException("Installer journal kind is missing."),
            root.GetProperty("transactionId").GetString() ?? throw new InvalidDataException("Installer journal transactionId is missing."),
            root.GetProperty("targetRelativePath").GetString() ?? throw new InvalidDataException("Installer journal target path is missing."),
            root.GetProperty("stagingRelativePath").GetString() ?? throw new InvalidDataException("Installer journal staging path is missing."),
            root.GetProperty("backupRelativePath").GetString() ?? throw new InvalidDataException("Installer journal backup path is missing."),
            root.GetProperty("targetOriginallyExists").GetBoolean(),
            ParsePhase(root),
            root.TryGetProperty("cleanupRootRelativePath", out var cleanupRoot)
                && cleanupRoot.ValueKind != JsonValueKind.Null
                ? cleanupRoot.GetString()
                : null,
            ReadProjectFiles(root));
        if (!string.Equals(Path.GetFileName(journalRoot), record.TransactionId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Installer journal transactionId does not match its directory.");
        }

        var journal = new InstallerTransactionJournal(projectRoot, journalRoot, journalPath, record);
        _ = journal.ResolvePath(record.TargetRelativePath);
        _ = journal.ResolvePath(record.StagingRelativePath);
        _ = journal.ResolvePath(record.BackupRelativePath);
        if (record.CleanupRootRelativePath != null)
        {
            _ = journal.ResolvePath(record.CleanupRootRelativePath);
        }

        foreach (var projectFile in record.ProjectFiles)
        {
            _ = journal.ResolvePath(projectFile.TargetRelativePath);
            _ = journal.ResolvePath(projectFile.BackupRelativePath);
        }

        return journal;
    }

    /// <summary>
    /// 原子写入完整 JSON，避免恢复器读取半份 checkpoint。
    /// </summary>
    private void Write()
    {
        var temporaryPath = JournalPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", Record.SchemaVersion);
                writer.WriteString("kind", Record.Kind);
                writer.WriteString("transactionId", Record.TransactionId);
                writer.WriteString("targetRelativePath", Record.TargetRelativePath);
                writer.WriteString("stagingRelativePath", Record.StagingRelativePath);
                writer.WriteString("backupRelativePath", Record.BackupRelativePath);
                writer.WriteBoolean("targetOriginallyExists", Record.TargetOriginallyExists);
                writer.WriteString("phase", Record.Phase.ToString());
                if (Record.CleanupRootRelativePath == null)
                {
                    writer.WriteNull("cleanupRootRelativePath");
                }
                else
                {
                    writer.WriteString("cleanupRootRelativePath", Record.CleanupRootRelativePath);
                }

                writer.WriteStartArray("projectFiles");
                foreach (var file in Record.ProjectFiles)
                {
                    writer.WriteStartObject();
                    writer.WriteString("targetRelativePath", file.TargetRelativePath);
                    writer.WriteString("backupRelativePath", file.BackupRelativePath);
                    writer.WriteBoolean("originalExists", file.OriginalExists);
                    writer.WriteBoolean("committed", file.Committed);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, JournalPath, overwrite: true);
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
    /// 生成不含绝对路径的正斜杠相对路径。
    /// </summary>
    private static string ToRelativePath(string projectRoot, string path)
    {
        return Path.GetRelativePath(projectRoot, path).Replace('\\', '/');
    }

    /// <summary>
    /// 解析有限的事务阶段枚举，损坏时转为可诊断数据错误。
    /// </summary>
    private static InstallerTransactionPhase ParsePhase(JsonElement root)
    {
        var phaseText = root.GetProperty("phase").GetString();
        return Enum.TryParse(phaseText, ignoreCase: false, out InstallerTransactionPhase phase)
            ? phase
            : throw new InvalidDataException("Installer journal phase is invalid: " + phaseText);
    }

    /// <summary>
    /// 读取外部项目文件恢复项；旧 journal 缺少该字段时使用空集合。
    /// </summary>
    private static IReadOnlyList<InstallerProjectFileJournalEntry> ReadProjectFiles(JsonElement root)
    {
        if (!root.TryGetProperty("projectFiles", out var filesElement)
            || filesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<InstallerProjectFileJournalEntry>();
        }

        List<InstallerProjectFileJournalEntry> files = new();
        foreach (var element in filesElement.EnumerateArray())
        {
            files.Add(new InstallerProjectFileJournalEntry(
                element.GetProperty("targetRelativePath").GetString()
                    ?? throw new InvalidDataException("Installer journal project file target is missing."),
                element.GetProperty("backupRelativePath").GetString()
                    ?? throw new InvalidDataException("Installer journal project file backup is missing."),
                element.GetProperty("originalExists").GetBoolean(),
                element.GetProperty("committed").GetBoolean()));
        }

        return files;
    }

    /// <summary>
    /// 将项目文件绝对路径转换为受守卫的相对恢复记录。
    /// </summary>
    private static IReadOnlyList<InstallerProjectFileJournalEntry> NormalizeProjectFiles(
        string projectRoot,
        IReadOnlyList<InstallerProjectFileJournalEntry>? projectFiles)
    {
        if (projectFiles == null || projectFiles.Count == 0)
        {
            return Array.Empty<InstallerProjectFileJournalEntry>();
        }

        return projectFiles
            .Select(file => new InstallerProjectFileJournalEntry(
                ToSafeRelativePath(projectRoot, file.TargetRelativePath),
                ToSafeRelativePath(projectRoot, file.BackupRelativePath),
                file.OriginalExists,
                file.Committed))
            .ToArray();
    }

    /// <summary>
    /// 将绝对项目路径转换为相对值，并再次确认它没有逃出项目根。
    /// </summary>
    private static string ToSafeRelativePath(string projectRoot, string path)
    {
        var fullPath = InstallerPathGuard.RequireFullPath(path, nameof(path));
        var relativePath = ToRelativePath(projectRoot, fullPath);
        _ = InstallerPathGuard.CombineInside(
            projectRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        return relativePath;
    }
}

/// <summary>
/// Installer journal 的稳定磁盘记录。
/// </summary>
internal sealed record InstallerTransactionJournalRecord(
    int SchemaVersion,
    string Kind,
    string TransactionId,
    string TargetRelativePath,
    string StagingRelativePath,
    string BackupRelativePath,
    bool TargetOriginallyExists,
    InstallerTransactionPhase Phase,
    string? CleanupRootRelativePath,
    IReadOnlyList<InstallerProjectFileJournalEntry> ProjectFiles);

/// <summary>
/// 描述一个需要随 Godot add-on 一起恢复的外部项目文件。
/// </summary>
internal sealed record InstallerProjectFileJournalEntry(
    string TargetRelativePath,
    string BackupRelativePath,
    bool OriginalExists,
    bool Committed);
