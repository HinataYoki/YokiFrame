using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame.Tooling.Application.Models.Luban;

namespace YokiFrame.Tooling.Application.Services.Luban;

/// <summary>通过 Luban 的 JSON data target 生成受限预览，供多个 Workbench Kit 只读消费。</summary>
public sealed class LubanJsonPreviewService
{
    private const int MAX_PREVIEW_FILE_COUNT = 32;
    private const long MAX_PREVIEW_FILE_BYTES = 512 * 1024;
    private static readonly string[] sPreviewCollectionKeys = { "data", "items", "rows", "list" };
    private static readonly JsonSerializerOptions sPreviewJsonOptions = new() { WriteIndented = true };
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> sPreviewDirectoryLocks = new(GetPathComparer());
    private readonly LubanCommandRunner mCommandRunner;

    /// <summary>创建使用默认 Luban 进程执行器的预览服务。</summary>
    public LubanJsonPreviewService() : this(new LubanCommandRunner())
    {
    }

    /// <summary>创建复用指定进程执行器的预览服务。</summary>
    /// <param name="commandRunner">负责安全启动和取消 Luban 进程的执行器。</param>
    public LubanJsonPreviewService(LubanCommandRunner commandRunner)
    {
        mCommandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
    }

    /// <summary>执行目标的 JSON 导出，并在有限预算内读取所有生成表。</summary>
    /// <param name="options">Luban 工具、配置和 target 参数。</param>
    /// <param name="previewDirectory">位于项目 Temp 下的独占临时输出目录。</param>
    /// <param name="cancellationToken">取消时终止 Luban 进程树。</param>
    /// <returns>退出码、日志、临时目录和解析后的 JSON 表预览。</returns>
    public async Task<LubanJsonPreviewResult> GenerateAsync(
        LubanToolOptions options,
        string previewDirectory,
        CancellationToken cancellationToken = default)
    {
        return await GenerateLockedAsync(
            options,
            previewDirectory,
            readPreviewTables: true,
            static result => result,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>在预览目录锁持有期间让领域服务读取原始 JSON，避免大型领域表受 UI 预览预算或并发清理影响。</summary>
    /// <typeparam name="TResult">领域读取完成后返回的结果类型。</typeparam>
    /// <param name="options">Luban 工具、配置和 target 参数。</param>
    /// <param name="previewDirectory">位于项目 Temp 下的独占临时输出目录。</param>
    /// <param name="reader">仅在 Luban 已完成后调用的同步目录读取器。</param>
    /// <param name="cancellationToken">取消时终止 Luban 子进程。</param>
    /// <returns>由领域读取器投影出的结果。</returns>
    internal async Task<TResult> GenerateAndReadDirectoryAsync<TResult>(
        LubanToolOptions options,
        string previewDirectory,
        Func<LubanJsonPreviewResult, TResult> reader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return await GenerateLockedAsync(options, previewDirectory, readPreviewTables: false, reader, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>在同一预览目录的独占锁内执行 Luban，并按调用方需要决定是否构造受限 UI 预览。</summary>
    /// <typeparam name="TResult">命令结果或目录读取结果。</typeparam>
    /// <param name="options">Luban 工具、配置和 target 参数。</param>
    /// <param name="previewDirectory">位于项目 Temp 下的独占临时输出目录。</param>
    /// <param name="readPreviewTables">是否读取并格式化受限 UI 预览。</param>
    /// <param name="reader">在锁内投影结果的读取器。</param>
    /// <param name="cancellationToken">取消时终止 Luban 子进程。</param>
    /// <returns>调用方需要的结果。</returns>
    private async Task<TResult> GenerateLockedAsync<TResult>(
        LubanToolOptions options,
        string previewDirectory,
        bool readPreviewTables,
        Func<LubanJsonPreviewResult, TResult> reader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        string fullPreviewDirectory = ValidatePreviewDirectory(options.ProjectRoot, previewDirectory);
        SemaphoreSlim directoryLock = sPreviewDirectoryLocks.GetOrAdd(fullPreviewDirectory, static _ => new SemaphoreSlim(1, 1));
        await directoryLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            LubanJsonPreviewResult result = await GenerateCoreAsync(
                options,
                fullPreviewDirectory,
                readPreviewTables,
                cancellationToken).ConfigureAwait(false);
            _ = ValidatePreviewDirectory(options.ProjectRoot, fullPreviewDirectory);
            return reader(result);
        }
        finally
        {
            directoryLock.Release();
        }
    }

    /// <summary>重建预览目录、运行 Luban，并按需要创建受限表预览；调用方负责持有目录锁。</summary>
    /// <param name="options">Luban 工具、配置和 target 参数。</param>
    /// <param name="previewDirectory">已完成 containment 校验的绝对临时目录。</param>
    /// <param name="readPreviewTables">是否读取并格式化 UI 预览表。</param>
    /// <param name="cancellationToken">取消时终止 Luban 子进程。</param>
    /// <returns>Luban 进程和可选预览表的结构化结果。</returns>
    private async Task<LubanJsonPreviewResult> GenerateCoreAsync(
        LubanToolOptions options,
        string previewDirectory,
        bool readPreviewTables,
        CancellationToken cancellationToken)
    {
        PreparePreviewDirectory(options.ProjectRoot, previewDirectory);
        IReadOnlyList<string> arguments = new[]
        {
            "-t", options.TargetName,
            "--conf", LubanPathResolver.ResolveConfigPath(options),
            "-d", "json",
            "-x", "outputDataDir=" + previewDirectory
        };
        LubanCommandResult command = await mCommandRunner.RunAsync(options, arguments, cancellationToken).ConfigureAwait(false);
        _ = ValidatePreviewDirectory(options.ProjectRoot, previewDirectory);
        if (!command.Succeeded)
        {
            return new LubanJsonPreviewResult
            {
                Succeeded = false,
                ExitCode = command.ExitCode,
                Log = command.Log,
                PreviewDirectory = previewDirectory,
                Diagnostics = new[] { "Luban JSON 预览失败，退出码: " + command.ExitCode }
            };
        }

        if (!readPreviewTables)
        {
            return new LubanJsonPreviewResult
            {
                Succeeded = true,
                ExitCode = command.ExitCode,
                Log = command.Log,
                PreviewDirectory = previewDirectory
            };
        }

        (IReadOnlyList<LubanJsonPreviewTable> tables, IReadOnlyList<string> diagnostics) = ReadPreviewTables(previewDirectory);
        StringBuilder log = new(command.Log);
        foreach (string diagnostic in diagnostics)
        {
            log.AppendLine(diagnostic);
        }

        return new LubanJsonPreviewResult
        {
            Succeeded = true,
            ExitCode = command.ExitCode,
            Log = log.ToString(),
            PreviewDirectory = previewDirectory,
            Tables = tables,
            Diagnostics = diagnostics
        };
    }

    /// <summary>读取预览目录中的有限 JSON 文件，超出预算时保留可诊断的跳过信息。</summary>
    /// <param name="previewDirectory">已经由 Luban 写入的临时输出目录。</param>
    /// <returns>可显示的 JSON 表和读取诊断。</returns>
    internal static (IReadOnlyList<LubanJsonPreviewTable> Tables, IReadOnlyList<string> Diagnostics) ReadPreviewTables(string previewDirectory)
    {
        List<LubanJsonPreviewTable> tables = new();
        List<string> diagnostics = new();
        int inspectedFileCount = 0;
        foreach (string path in Directory.EnumerateFiles(previewDirectory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(static value => Path.GetFileName(value), StringComparer.Ordinal))
        {
            if (inspectedFileCount >= MAX_PREVIEW_FILE_COUNT)
            {
                diagnostics.Add("Luban 预览最多读取 " + MAX_PREVIEW_FILE_COUNT + " 个 JSON 文件，剩余文件已跳过。");
                break;
            }

            inspectedFileCount++;
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                {
                    diagnostics.Add("Luban 预览已跳过符号链接文件: " + Path.GetFileName(path));
                    continue;
                }

                FileInfo fileInfo = new(path);
                if (fileInfo.Length > MAX_PREVIEW_FILE_BYTES)
                {
                    diagnostics.Add("Luban 预览已跳过过大的 JSON 文件: " + fileInfo.Name);
                    continue;
                }

                string json = File.ReadAllText(path);
                JsonNode? node = JsonNode.Parse(json);
                tables.Add(new LubanJsonPreviewTable
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    Count = CountPreviewRows(node),
                    PreviewJson = node?.ToJsonString(sPreviewJsonOptions) ?? json
                });
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                diagnostics.Add("Luban 无法读取预览文件 " + Path.GetFileName(path) + ": " + exception.Message);
            }
        }

        return (tables, diagnostics);
    }

    /// <summary>拒绝预览输出越出当前项目或进入非临时目录，避免清理过程触及作者文件。</summary>
    /// <param name="projectRoot">当前项目根目录。</param>
    /// <param name="previewDirectory">调用方请求的输出目录。</param>
    /// <returns>经过 containment 校验的绝对临时目录。</returns>
    internal static string ValidatePreviewDirectory(string projectRoot, string previewDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException("项目根不能为空。", nameof(projectRoot));
        }

        if (string.IsNullOrWhiteSpace(previewDirectory))
        {
            throw new ArgumentException("预览目录不能为空。", nameof(previewDirectory));
        }

        string root = Path.GetFullPath(projectRoot);
        string fullDirectory = Path.GetFullPath(Path.IsPathFullyQualified(previewDirectory)
            ? previewDirectory
            : Path.Combine(root, previewDirectory));
        string relativePath = Path.GetRelativePath(root, fullDirectory);
        bool outsideProject = relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathFullyQualified(relativePath);
        if (outsideProject)
        {
            throw new InvalidDataException("Luban 预览目录越出项目根。");
        }

        string normalizedRelativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        if (!normalizedRelativePath.StartsWith(
                "Temp" + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Luban 预览目录必须位于项目 Temp 的独占子目录中。");
        }

        EnsureNoReparsePoint(root, fullDirectory);
        return fullDirectory;
    }

    /// <summary>重建本次独占的临时目录，避免上次输出混入当前 JSON 投影。</summary>
    /// <param name="projectRoot">当前项目根，用于删除前重新校验路径链。</param>
    /// <param name="previewDirectory">已完成 containment 校验的目录。</param>
    private static void PreparePreviewDirectory(string projectRoot, string previewDirectory)
    {
        var fullPreviewDirectory = ValidatePreviewDirectory(projectRoot, previewDirectory);
        if (Directory.Exists(fullPreviewDirectory))
        {
            Directory.Delete(fullPreviewDirectory, true);
        }

        Directory.CreateDirectory(fullPreviewDirectory);
    }

    /// <summary>拒绝项目根到预览目录的现存组件包含符号链接、Junction 或其它重解析点。</summary>
    private static void EnsureNoReparsePoint(string root, string path)
    {
        var current = root;
        EnsurePathComponentIsNotReparsePoint(current);
        var relativePath = Path.GetRelativePath(root, path);
        foreach (var segment in relativePath.Split(
                     new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            EnsurePathComponentIsNotReparsePoint(current);
        }
    }

    /// <summary>校验单个现存路径组件不是重解析点，避免递归清理沿链接越出项目。</summary>
    private static void EnsurePathComponentIsNotReparsePoint(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path))
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Luban 预览目录不能包含符号链接或 Junction: " + path);
        }
    }

    /// <summary>按数组或常见集合字段推断 JSON 预览中的记录数。</summary>
    /// <param name="node">已解析的 JSON 根节点。</param>
    /// <returns>可用于 Workbench 表列表的记录数量。</returns>
    private static int CountPreviewRows(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            return array.Count;
        }

        if (node is not JsonObject objectNode)
        {
            return 0;
        }

        foreach (string key in sPreviewCollectionKeys)
        {
            if (objectNode[key] is JsonArray rows)
            {
                return rows.Count;
            }
        }

        return objectNode.Count;
    }

    /// <summary>返回与宿主文件系统一致的预览目录键比较器，避免 Windows 路径大小写形成平行锁。</summary>
    /// <returns>当前平台路径语义对应的字符串比较器。</returns>
    private static StringComparer GetPathComparer() => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
