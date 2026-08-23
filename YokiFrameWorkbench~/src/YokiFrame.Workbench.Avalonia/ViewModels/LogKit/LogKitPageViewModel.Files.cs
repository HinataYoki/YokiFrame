using System.Globalization;
using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 Editor/Player 文件来源选择、元数据和显式尾部读取生命周期。</summary>
public sealed partial class LogKitPageViewModel
{
    private CancellationTokenSource? mFilePreviewCancellation;
    private WorkbenchLogKitFileMetadata? mEditorFileMetadata;
    private WorkbenchLogKitFileMetadata? mPlayerFileMetadata;
    private WorkbenchLogKitFilePreview? mFilePreview;
    private string mLogDirectoryPath = string.Empty;
    private string mSelectedSource = MEMORY_SOURCE;
    private string mFileStatusText = WorkbenchI18nService.Instance.GetString("String.LogKit.FileNotReadYet", "尚未读取文件");
    private bool mIsFileLoading;

    /// <summary>获取或设置当前日志来源。</summary>
    public string SelectedSource
    {
        get => mSelectedSource;
        set
        {
            var normalized = NormalizeSource(value);
            if (!SetProperty(ref mSelectedSource, normalized))
            {
                return;
            }

            if (mFilePreview != null
                && !string.Equals(mFilePreview.Kind, normalized, StringComparison.OrdinalIgnoreCase))
            {
                mFilePreview = null;
            }

            NotifySelectedSourceProperties();
            QueueSelectedFilePreview();
        }
    }

    /// <summary>获取是否正在显示内存历史。</summary>
    public bool IsMemorySource => string.Equals(SelectedSource, MEMORY_SOURCE, StringComparison.Ordinal);
    /// <summary>获取是否正在显示 Editor 文件。</summary>
    public bool IsEditorSource => string.Equals(SelectedSource, EDITOR_SOURCE, StringComparison.Ordinal);
    /// <summary>获取是否正在显示 Player 文件。</summary>
    public bool IsPlayerSource => string.Equals(SelectedSource, PLAYER_SOURCE, StringComparison.Ordinal);
    /// <summary>获取是否正在显示文件尾部。</summary>
    public bool IsFileSource => !IsMemorySource;
    /// <summary>获取当前文件读取状态。</summary>
    public string FileStatusText
    {
        get => mFileStatusText;
        private set
        {
            if (SetProperty(ref mFileStatusText, value))
            {
                OnPropertyChanged(nameof(ActiveSourceStatusText));
            }
        }
    }
    /// <summary>获取是否正在异步读取文件尾部。</summary>
    public bool IsFileLoading { get => mIsFileLoading; private set => SetFileLoading(value); }
    /// <summary>获取当前来源计数。</summary>
    public string ActiveSourceCountText => IsMemorySource
        ? string.Format(GetString("String.LogKit.ItemsSuffixTemplate", "{0} 条"), VisibleHistoryCountText)
        : string.Format(
            GetString("String.LogKit.LinesSuffixTemplate", "{0} 行"),
            (mFilePreview?.LineCount ?? 0).ToString(CultureInfo.InvariantCulture));
    /// <summary>获取当前来源最近一次显式操作状态。</summary>
    public string ActiveSourceStatusText => IsMemorySource ? HistoryStatusText : FileStatusText;
    /// <summary>获取当前文件名。</summary>
    public string SelectedFileNameText => mFilePreview?.FileName
        ?? SelectedFileMetadata?.FileName
        ?? (IsEditorSource
            ? GetString("String.LogKit.EditorLogLabel", "Editor 日志")
            : GetString("String.LogKit.PlayerLogLabel", "Player 日志"));
    /// <summary>获取当前完整文件路径。</summary>
    public string SelectedFilePathText => mFilePreview?.Path ?? SelectedFileMetadata?.Path ?? "--";
    /// <summary>获取当前文件大小。</summary>
    public string SelectedFileSizeText => FormatBytes(mFilePreview?.SizeBytes ?? SelectedFileMetadata?.SizeBytes ?? 0L);
    /// <summary>获取当前文件修改时间。</summary>
    public string SelectedFileModifiedText => FormatModifiedTime(mFilePreview?.ModifiedUtc ?? SelectedFileMetadata?.ModifiedUtc ?? string.Empty);
    /// <summary>获取当前文件尾部文本。</summary>
    public string FilePreviewContent => mFilePreview?.Content ?? string.Empty;
    /// <summary>获取当前文件是否存在。</summary>
    public bool SelectedFileExists => mFilePreview?.Exists ?? SelectedFileMetadata?.Exists ?? false;
    /// <summary>获取是否存在可显示的文件内容。</summary>
    public bool HasFilePreviewContent => !string.IsNullOrEmpty(FilePreviewContent);
    /// <summary>获取是否应显示文件空状态。</summary>
    public bool IsFilePreviewEmpty => !IsFileLoading && !HasFilePreviewContent;
    /// <summary>获取文件预览是否只包含尾部片段。</summary>
    public bool IsFilePreviewTruncated => mFilePreview?.Truncated == true;
    /// <summary>获取文件预览实际传输通道。</summary>
    public string FilePreviewTransportText => mFilePreview?.Transport
        ?? GetString("String.LogKit.TransportOnDemand", "按需读取");
    /// <summary>获取文件空状态说明。</summary>
    public string FilePreviewEmptyText => SelectedFileExists
        ? GetString("String.LogKit.FilePreviewEmpty", "文件当前没有可显示内容")
        : GetString("String.LogKit.FileMissing", "日志文件尚不存在");

    /// <summary>获取 Runtime 实际解析出的日志目录。</summary>
    public string LogDirectoryPathText => string.IsNullOrWhiteSpace(mLogDirectoryPath) ? "--" : mLogDirectoryPath;
    /// <summary>获取当前日志目录是否存在且可被系统文件管理器打开。</summary>
    public bool HasLogDirectory => !string.IsNullOrWhiteSpace(mLogDirectoryPath) && Directory.Exists(mLogDirectoryPath);

    /// <summary>获取当前来源对应的最新文件元数据。</summary>
    private WorkbenchLogKitFileMetadata? SelectedFileMetadata => IsEditorSource
        ? mEditorFileMetadata
        : mPlayerFileMetadata;

    /// <summary>应用 dashboard 或 telemetry 携带的轻量文件元数据，不读取文件内容。</summary>
    private void ApplyFileMetadata(WorkbenchLogKitFiles? files)
    {
        mLogDirectoryPath = files?.Directory ?? string.Empty;
        mEditorFileMetadata = files?.Editor;
        mPlayerFileMetadata = files?.Player;
        if (mFilePreview != null
            && !string.Equals(mFilePreview.Path, SelectedFileMetadata?.Path, StringComparison.Ordinal))
        {
            mFilePreview = null;
        }

        NotifyFileProperties();
    }

    /// <summary>注入平台目录打开回调；Workbench 使用系统默认文件管理器实现。</summary>
    /// <param name="openDirectoryAsync">打开已解析绝对目录的异步回调。</param>
    internal void SetOpenDirectoryHandler(Func<string, Task>? openDirectoryAsync)
    {
        mOpenDirectoryAsync = openDirectoryAsync;
        OpenDirectoryCommand.RaiseCanExecuteChanged();
    }

    /// <summary>调用平台回调打开当前 Runtime 日志目录。</summary>
    private async Task OpenDirectoryAsync()
    {
        if (!CanOpenDirectory())
        {
            return;
        }

        var path = mLogDirectoryPath;
        try
        {
            await mOpenDirectoryAsync!(path);
        }
        catch (Exception exception)
        {
            SetSettingsStatus(string.Format(
                GetString("String.LogKit.OpenDirectoryFailedTemplate", "打开日志目录失败: {0}"), exception.Message));
        }
    }

    /// <summary>判断当前是否具备平台回调且日志目录已经存在。</summary>
    private bool CanOpenDirectory()
    {
        return !mIsDisposed
            && mOpenDirectoryAsync != null
            && HasLogDirectory;
    }

    /// <summary>页面激活且已选择文件时发起一次按需读取；内存来源不产生文件 IO。</summary>
    private void QueueSelectedFilePreview()
    {
        RefreshFileCommand.RaiseCanExecuteChanged();
        if (mIsPageActive && IsFileSource && CanRefreshFile())
        {
            _ = RefreshSelectedFileAsync();
        }
    }

    /// <summary>取消旧文件请求后读取当前文件尾部，并拒绝晚到的旧来源结果。</summary>
    private async Task RefreshSelectedFileAsync()
    {
        if (mReadFileAsync == null || !CanRefreshFile())
        {
            return;
        }

        CancelFilePreview();
        var identity = CaptureIdentity();
        var kind = SelectedSource;
        CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(mIdentityCancellation.Token);
        mFilePreviewCancellation = cancellation;
        IsFileLoading = true;
        FileStatusText = string.Format(
            GetString("String.LogKit.ReadingTailTemplate", "正在读取 {0} 文件尾部..."),
            kind == EDITOR_SOURCE ? "Editor" : "Player");
        await ReadSelectedFileCoreAsync(identity, kind, cancellation);
    }

    /// <summary>执行文件读取并在 UI 身份仍匹配时提交结果。</summary>
    private async Task ReadSelectedFileCoreAsync(
        HostIdentity identity,
        string kind,
        CancellationTokenSource cancellation)
    {
        try
        {
            var preview = await mReadFileAsync!(identity.EngineId, kind, cancellation.Token);
            if (ReferenceEquals(mFilePreviewCancellation, cancellation)
                && MatchesIdentity(identity.EngineId, identity.SessionId, identity.Generation)
                && string.Equals(SelectedSource, kind, StringComparison.Ordinal))
            {
                ApplyFilePreview(preview);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(mFilePreviewCancellation, cancellation))
            {
                SetFileStatus(string.Format(
                    GetString("String.LogKit.ReadFailedTemplate", "文件读取失败: {0}"), exception.Message));
            }
        }
        finally
        {
            CompleteFilePreview(cancellation);
        }
    }

    /// <summary>提交一份按需文件预览并刷新所有派生字段。</summary>
    private void ApplyFilePreview(WorkbenchLogKitFilePreview preview)
    {
        mFilePreview = preview;
        SetFileStatus(!string.IsNullOrWhiteSpace(preview.ErrorMessage)
            ? string.Format(
                GetString("String.LogKit.ReadFailedTemplate", "文件读取失败: {0}"), preview.ErrorMessage)
            : (preview.Exists
                ? (preview.Truncated
                    ? GetString("String.LogKit.ReadTailDone", "已读取文件尾部")
                    : GetString("String.LogKit.ReadFullDone", "已读取完整文件"))
                : GetString("String.LogKit.FileNotFoundShort", "文件尚不存在")));
        NotifyFileProperties();
    }

    /// <summary>只结束仍为当前请求的 loading 状态并释放取消源。</summary>
    private void CompleteFilePreview(CancellationTokenSource cancellation)
    {
        if (ReferenceEquals(mFilePreviewCancellation, cancellation))
        {
            mFilePreviewCancellation = null;
            IsFileLoading = false;
        }

        cancellation.Dispose();
    }

    /// <summary>取消当前文件读取并阻止其结果覆盖后续来源。</summary>
    private void CancelFilePreview()
    {
        var cancellation = mFilePreviewCancellation;
        mFilePreviewCancellation = null;
        if (cancellation != null)
        {
            cancellation.Cancel();
        }

        IsFileLoading = false;
    }

    /// <summary>判断当前文件来源是否允许调用 Application 按需读取。</summary>
    private bool CanRefreshFile()
    {
        return !mIsDisposed
            && mIsPageActive
            && IsFileSource
            && SupportsFilePreview
            && mReadFileAsync != null
            && !string.IsNullOrWhiteSpace(EngineId);
    }

    /// <summary>统一限制来源为 memory、editor 或 player。</summary>
    private static string NormalizeSource(string? source)
    {
        return source?.Trim().ToLowerInvariant() switch
        {
            EDITOR_SOURCE => EDITOR_SOURCE,
            PLAYER_SOURCE => PLAYER_SOURCE,
            _ => MEMORY_SOURCE
        };
    }

    /// <summary>通知来源切换影响的区域和命令。</summary>
    private void NotifySelectedSourceProperties()
    {
        OnPropertyChanged(nameof(IsMemorySource));
        OnPropertyChanged(nameof(IsEditorSource));
        OnPropertyChanged(nameof(IsPlayerSource));
        OnPropertyChanged(nameof(IsFileSource));
        OnPropertyChanged(nameof(ActiveSourceCountText));
        OnPropertyChanged(nameof(ActiveSourceStatusText));
        NotifyFileProperties();
        RefreshFileCommand.RaiseCanExecuteChanged();
    }

    /// <summary>通知文件摘要、内容和空状态派生属性。</summary>
    private void NotifyFileProperties()
    {
        OnPropertyChanged(nameof(SelectedFileNameText));
        OnPropertyChanged(nameof(SelectedFilePathText));
        OnPropertyChanged(nameof(SelectedFileSizeText));
        OnPropertyChanged(nameof(SelectedFileModifiedText));
        OnPropertyChanged(nameof(FilePreviewContent));
        OnPropertyChanged(nameof(SelectedFileExists));
        OnPropertyChanged(nameof(HasFilePreviewContent));
        OnPropertyChanged(nameof(IsFilePreviewEmpty));
        OnPropertyChanged(nameof(IsFilePreviewTruncated));
        OnPropertyChanged(nameof(FilePreviewTransportText));
        OnPropertyChanged(nameof(FilePreviewEmptyText));
        OnPropertyChanged(nameof(LogDirectoryPathText));
        OnPropertyChanged(nameof(HasLogDirectory));
        OnPropertyChanged(nameof(ActiveSourceCountText));
        OpenDirectoryCommand.RaiseCanExecuteChanged();
    }

    /// <summary>更新 loading 状态和空状态。</summary>
    private void SetFileLoading(bool value)
    {
        if (SetProperty(ref mIsFileLoading, value, nameof(IsFileLoading)))
        {
            OnPropertyChanged(nameof(IsFilePreviewEmpty));
            RefreshFileCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>写入文件读取状态文本的集中入口。</summary>
    /// <param name="text">新的状态文本。</param>
    private void SetFileStatus(string text)
    {
        FileStatusText = text;
    }

    /// <summary>格式化文件字节数。</summary>
    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024L) return bytes + " B";
        if (bytes < 1024L * 1024L) return (bytes / 1024d).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
        return (bytes / (1024d * 1024d)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
    }

    /// <summary>把 UTC 修改时间转成本地紧凑显示，无法解析时保留原值。</summary>
    private static string FormatModifiedTime(string modifiedUtc)
    {
        return DateTimeOffset.TryParse(modifiedUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var modified)
            ? modified.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : (string.IsNullOrWhiteSpace(modifiedUtc) ? "--" : modifiedUtc);
    }
}
