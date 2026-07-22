using System.Globalization;
using YokiFrame.Tooling.Application.Models.LogKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels.LogKit;

/// <summary>把一条不可变内存日志投影为列表、筛选和详情共用的稳定行。</summary>
public sealed class LogKitHistoryRowViewModel
{
    private readonly string mSearchText;

    /// <summary>创建带稳定身份的内存日志行。</summary>
    /// <param name="identity">包含重复序号的稳定帧内身份。</param>
    /// <param name="entry">Application 已解析的日志记录。</param>
    public LogKitHistoryRowViewModel(string identity, WorkbenchLogKitHistoryEntry entry)
    {
        Identity = identity;
        Entry = entry;
        TimeText = FormatTimestamp(entry.TimestampUtc);
        LevelText = string.IsNullOrWhiteSpace(entry.Level) ? "--" : entry.Level;
        MessageText = string.IsNullOrWhiteSpace(entry.Message) ? "--" : entry.Message;
        ExceptionSummary = CreateExceptionSummary(entry);
        mSearchText = string.Join('\n', new[]
        {
            entry.Level,
            entry.Message,
            entry.Context,
            entry.ExceptionType,
            entry.ExceptionMessage,
            entry.StackTrace,
            entry.TimestampUtc
        });
    }

    /// <summary>获取帧间协调使用的稳定身份。</summary>
    public string Identity { get; }
    /// <summary>获取原始强类型日志记录。</summary>
    public WorkbenchLogKitHistoryEntry Entry { get; }
    /// <summary>获取本地时区的紧凑时间。</summary>
    public string TimeText { get; }
    /// <summary>获取等级显示文本。</summary>
    public string LevelText { get; }
    /// <summary>获取主消息。</summary>
    public string MessageText { get; }
    /// <summary>获取异常摘要；没有异常时为空。</summary>
    public string ExceptionSummary { get; }
    /// <summary>获取是否包含上下文。</summary>
    public bool HasContext => !string.IsNullOrWhiteSpace(Entry.Context);
    /// <summary>获取是否包含异常。</summary>
    public bool HasException => !string.IsNullOrWhiteSpace(ExceptionSummary) || !string.IsNullOrWhiteSpace(Entry.StackTrace);
    /// <summary>获取是否属于调试等级。</summary>
    public bool IsDebug => MatchesLevel("debug");
    /// <summary>获取是否属于普通信息等级。</summary>
    public bool IsInfo => MatchesLevel("info") || MatchesLevel("log");
    /// <summary>获取是否属于警告等级。</summary>
    public bool IsWarning => MatchesLevel("warning") || MatchesLevel("warn");
    /// <summary>获取是否属于错误或异常等级。</summary>
    public bool IsError => MatchesLevel("error") || MatchesLevel("fatal") || MatchesLevel("exception");

    /// <summary>判断当前行是否匹配搜索文本和等级筛选。</summary>
    /// <param name="searchText">不区分大小写的自由文本。</param>
    /// <param name="level">全部或具体日志等级。</param>
    /// <returns>应显示当前行时返回 true。</returns>
    public bool Matches(string searchText, string level)
    {
        var levelMatches = string.IsNullOrWhiteSpace(level)
            || string.Equals(level, "全部", StringComparison.Ordinal)
            || string.Equals(LevelText, level, StringComparison.OrdinalIgnoreCase)
            || (string.Equals(level, "Info", StringComparison.OrdinalIgnoreCase) && MatchesLevel("log"));
        return levelMatches
            && (string.IsNullOrWhiteSpace(searchText)
                || mSearchText.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>按不区分大小写规则判断等级。</summary>
    private bool MatchesLevel(string expected)
    {
        return string.Equals(Entry.Level, expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>把 ISO 时间转为本地紧凑时间，异常输入保留原文。</summary>
    private static string FormatTimestamp(string timestampUtc)
    {
        if (!DateTimeOffset.TryParse(timestampUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
        {
            return string.IsNullOrWhiteSpace(timestampUtc) ? "--" : timestampUtc;
        }

        return timestamp.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    /// <summary>合并异常类型和消息，避免列表行重复占用两列。</summary>
    private static string CreateExceptionSummary(WorkbenchLogKitHistoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.ExceptionType))
        {
            return entry.ExceptionMessage;
        }

        return string.IsNullOrWhiteSpace(entry.ExceptionMessage)
            ? entry.ExceptionType
            : entry.ExceptionType + ": " + entry.ExceptionMessage;
    }
}
