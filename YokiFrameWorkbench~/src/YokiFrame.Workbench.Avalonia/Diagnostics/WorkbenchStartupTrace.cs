using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace YokiFrame.Workbench.Avalonia.Diagnostics;

/// <summary>
/// 记录 Workbench 冷启动关键阶段；诊断失败时自动停用，避免影响窗口启动。
/// </summary>
internal static class WorkbenchStartupTrace
{
    private const string PRODUCT_DIRECTORY_NAME = "YokiFrame";
    private const string WORKBENCH_DIRECTORY_NAME = "Workbench";
    private const string STARTUP_DIRECTORY_NAME = "startup";

    private static readonly object SyncRoot = new();
    private static readonly long StartedTimestamp = Stopwatch.GetTimestamp();
    private static string? sTracePath;
    private static bool sDisabled;

    /// <summary>
    /// 根据已解析的工具模式初始化 trace 文件路径，不重复解析启动参数。
    /// </summary>
    /// <param name="options">已完成路径归一化的工具启动选项。</param>
    public static void Configure(ToolStartupOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (sDisabled)
        {
            return;
        }

        try
        {
            var traceRoot = ResolveTraceRoot(options);
            Directory.CreateDirectory(traceRoot);
            sTracePath = Path.Combine(
                traceRoot,
                "startup-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + ".jsonl");
            Mark("trace.configure");
        }
        catch (Exception)
        {
            // 启动诊断只是排查冷启动耗时的旁路能力，任何路径或权限异常都不能阻断 Workbench。
            sDisabled = true;
            sTracePath = null;
        }
    }

    /// <summary>
    /// 写入一个启动阶段标记；未完成配置或写入失败时自动停用。
    /// </summary>
    /// <param name="name">阶段名称。</param>
    public static void Mark(string name)
    {
        if (sDisabled || string.IsNullOrWhiteSpace(sTracePath))
        {
            return;
        }

        try
        {
            var line = CreateTraceLine(name);
            lock (SyncRoot)
            {
                File.AppendAllText(sTracePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (Exception)
        {
            // trace 写入失败只说明诊断不可用，主窗口启动必须继续。
            sDisabled = true;
            sTracePath = null;
        }
    }

    /// <summary>
    /// 根据工具模式选择诊断 owner；Workbench 属于项目，Installer 属于当前用户工具数据。
    /// </summary>
    /// <param name="options">已完成路径归一化的工具启动选项。</param>
    /// <returns>当前模式可写且不会污染发布 Runtime 的诊断目录。</returns>
    private static string ResolveTraceRoot(ToolStartupOptions options)
    {
        if (options.Mode == ToolStartupMode.Workbench)
        {
            return Path.Combine(options.ProjectRoot, ".yokiframe", "workbench");
        }

        var localDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localDataRoot))
        {
            localDataRoot = Path.GetTempPath();
        }

        return Path.Combine(
            localDataRoot,
            PRODUCT_DIRECTORY_NAME,
            WORKBENCH_DIRECTORY_NAME,
            STARTUP_DIRECTORY_NAME);
    }

    /// <summary>
    /// 创建单行 JSONL 诊断记录；手写 JSON 避免 Native AOT 下引入反射序列化成本。
    /// </summary>
    /// <param name="name">阶段名称。</param>
    /// <returns>JSONL 单行文本。</returns>
    private static string CreateTraceLine(string name)
    {
        var elapsedMilliseconds = Stopwatch.GetElapsedTime(StartedTimestamp).TotalMilliseconds;
        return "{\"event\":\""
            + EscapeJson(name)
            + "\",\"elapsedMs\":"
            + elapsedMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)
            + ",\"pid\":"
            + Environment.ProcessId.ToString(CultureInfo.InvariantCulture)
            + ",\"thread\":"
            + Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture)
            + "}";
    }

    /// <summary>
    /// 转义 JSON 字符串中的特殊字符；阶段名通常为常量，该方法只负责兜底。
    /// </summary>
    /// <param name="value">原始字符串。</param>
    /// <returns>可写入 JSON 字符串的文本。</returns>
    private static string EscapeJson(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
