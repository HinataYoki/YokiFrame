using System.Collections.Specialized;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 TableKit 控制台日志的复制、清空和有界追加。</summary>
public sealed partial class TableKitPageViewModel
{
    /// <summary>复制控制台文本到系统剪贴板或给出降级提示。</summary>
    private async Task CopyConsoleAsync()
    {
        string text = string.Join(Environment.NewLine, ConsoleEntries.Select(entry => "[" + entry.Time + "] " + entry.Level + " " + entry.Message));
        if (mCopyTextAsync == null)
        {
            StatusDetailText = GetString(NoClipboardKey, "当前没有可用剪贴板服务，请直接选择控制台文本。");
            return;
        }

        await mCopyTextAsync(text);
        AppendConsole("SUCCESS", GetString(ConsoleCopiedKey, "控制台日志已复制到剪贴板。"), false);
    }

    /// <summary>清空本轮控制台日志。</summary>
    private void ClearConsole()
    {
        ConsoleEntries.Clear();
        IsConsoleExpanded = false;
        SetStatus(GetString(ConsoleClearedKey, "控制台已清空"));
    }

    /// <summary>响应控制台集合变化，只刷新摘要，不抢夺用户的抽屉展开状态。</summary>
    /// <param name="sender">控制台集合。</param>
    /// <param name="args">集合变化参数。</param>
    private void OnConsoleEntriesChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(IsConsoleEmpty));
        OnPropertyChanged(nameof(ConsoleCountText));
        OnPropertyChanged(nameof(ConsoleErrorCount));
        OnPropertyChanged(nameof(ConsoleSummaryText));
    }

    /// <summary>把多行 Luban 输出加入有界控制台集合。</summary>
    /// <param name="level">日志级别。</param>
    /// <param name="text">多行日志。</param>
    private void AppendConsoleLines(string level, string text)
    {
        foreach (string line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            AppendConsole(level, line.Trim(), false);
        }
    }

    /// <summary>加入一条控制台日志并限制集合长度。</summary>
    /// <param name="level">日志级别。</param>
    /// <param name="message">日志正文。</param>
    /// <param name="notify">是否立即设置操作状态。</param>
    private void AppendConsole(string level, string message, bool notify)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ConsoleEntries.Add(new TableKitConsoleEntryViewModel(DateTime.Now.ToString("HH:mm:ss"), level, message));
        while (ConsoleEntries.Count > 120)
        {
            ConsoleEntries.RemoveAt(0);
        }

        if (notify)
        {
            StatusDetailText = message;
        }
    }
}
