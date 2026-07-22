using System.Diagnostics;
using YokiFrame.Client;
using YokiFrame.Protocol.FileBridge;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>
/// 监听宿主 engine registry 与 heartbeat 的生命周期身份变化，并让 Client 立即失效旧快速通道。
/// </summary>
public sealed class EngineLifecycleMonitor : IDisposable
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(120);
    private readonly IYokiFrameClient mClient;
    private readonly object mSyncRoot = new();
    private readonly AsyncLocal<bool> mPublishingChanged = new();
    private FileSystemWatcher? mEngineWatcher;
    private FileSystemWatcher? mStatusWatcher;
    private Timer? mDebounceTimer;
    private string mEngineId = string.Empty;
    private string mIdentity = string.Empty;
    private Task mIdentityCheckTask = Task.CompletedTask;
    private bool mDisposed;

    /// <summary>
    /// 创建当前项目的宿主生命周期监视器。
    /// </summary>
    /// <param name="client">统一 YokiFrame Client。</param>
    public EngineLifecycleMonitor(IYokiFrameClient client)
    {
        mClient = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// 生命周期身份发生变化时触发；事件处理器不应执行阻塞 IO。
    /// </summary>
    public event EventHandler<EngineLifecycleChangedEventArgs>? Changed;

    /// <summary>
    /// 获取当前监视的 engine 标识。
    /// </summary>
    public string EngineId => mEngineId;

    /// <summary>
    /// 切换监视目标并建立 engine/status 两个目录的文件监听。
    /// </summary>
    /// <param name="engineId">目标 engine；为空时停止监听。</param>
    public void SetEngine(string? engineId)
    {
        var normalizedEngineId = engineId?.Trim() ?? string.Empty;
        lock (mSyncRoot)
        {
            ThrowIfDisposed();
            if (string.Equals(mEngineId, normalizedEngineId, StringComparison.Ordinal))
            {
                return;
            }

            StopWatchers();
            mEngineId = normalizedEngineId;
            mIdentity = ReadIdentity(normalizedEngineId);
            if (string.IsNullOrWhiteSpace(normalizedEngineId))
            {
                return;
            }

            var engineRoot = mClient.Paths.GetEngineRoot(normalizedEngineId);
            var statusRoot = Path.GetDirectoryName(mClient.Paths.GetHeartbeatPath(normalizedEngineId));
            mEngineWatcher = CreateWatcher(engineRoot, "engine.json");
            mStatusWatcher = CreateWatcher(statusRoot, "heartbeat.json");
        }
    }

    /// <summary>
    /// 释放文件监听和 debounce 定时器。
    /// </summary>
    public void Dispose()
    {
        Task identityCheckTask;
        lock (mSyncRoot)
        {
            if (mDisposed)
            {
                return;
            }

            mDisposed = true;
            StopWatchers();
            identityCheckTask = mIdentityCheckTask;
        }

        if (!mPublishingChanged.Value)
        {
            identityCheckTask.GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// 创建只关注目标协议文件的 watcher；目录尚未创建时等待后续兜底刷新发现。
    /// </summary>
    private FileSystemWatcher? CreateWatcher(string? directory, string filter)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        var watcher = new FileSystemWatcher(directory, filter)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };
        watcher.Changed += OnFileChanged;
        watcher.Created += OnFileChanged;
        watcher.Deleted += OnFileChanged;
        watcher.Renamed += OnFileRenamed;
        return watcher;
    }

    /// <summary>
    /// 合并原子写入产生的多次文件事件，避免重复读取和重复刷新。
    /// </summary>
    private void OnFileChanged(object sender, FileSystemEventArgs eventArgs)
    {
        ScheduleIdentityCheck();
    }

    /// <summary>
    /// 处理原子替换产生的重命名事件。
    /// </summary>
    private void OnFileRenamed(object sender, RenamedEventArgs eventArgs)
    {
        ScheduleIdentityCheck();
    }

    /// <summary>
    /// 安排短 debounce 检查，避免在宿主连续发布 registry/heartbeat 时读到中间状态。
    /// </summary>
    private void ScheduleIdentityCheck()
    {
        lock (mSyncRoot)
        {
            if (mDisposed)
            {
                return;
            }

            mDebounceTimer ??= new Timer(OnDebounceElapsed);
            mDebounceTimer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// 在后台线程读取最新身份，仅当生命周期字段变化时通知 Application。
    /// </summary>
    private void OnDebounceElapsed(object? state)
    {
        _ = QueueIdentityCheck();
    }

    /// <summary>
    /// 立即排队一次身份检查；供宿主兜底刷新与测试复用同一串行异常边界。
    /// </summary>
    /// <returns>检查及前序检查全部结束时完成的任务。</returns>
    internal Task CheckNowAsync()
    {
        return QueueIdentityCheck();
    }

    /// <summary>
    /// 把新检查串接到当前任务，防止 Timer 重入并发失效同一 Client 连接。
    /// </summary>
    /// <returns>当前排队后的完整任务链。</returns>
    private Task QueueIdentityCheck()
    {
        lock (mSyncRoot)
        {
            if (mDisposed)
            {
                return Task.CompletedTask;
            }

            mIdentityCheckTask = RunIdentityCheckSafelyAsync(mIdentityCheckTask);
            return mIdentityCheckTask;
        }
    }

    /// <summary>
    /// 等待前序检查后执行当前检查，并把后台异常转为可观测 Trace，避免 async void 终止进程。
    /// </summary>
    /// <param name="previousTask">同一监视器的前序检查。</param>
    private async Task RunIdentityCheckSafelyAsync(Task previousTask)
    {
        try
        {
            await previousTask.ConfigureAwait(false);
            await CheckIdentityChangeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Trace.TraceError("Engine lifecycle identity check failed: " + exception);
        }
    }

    /// <summary>
    /// 读取并失效变化后的宿主连接；只有失效成功且监视目标未切换时才提交身份和事件。
    /// </summary>
    private async Task CheckIdentityChangeAsync()
    {
        string engineId;
        string previousIdentity;
        lock (mSyncRoot)
        {
            if (mDisposed || string.IsNullOrWhiteSpace(mEngineId))
            {
                return;
            }

            engineId = mEngineId;
            previousIdentity = mIdentity;
        }

        var currentIdentity = ReadIdentity(engineId);
        if (string.Equals(previousIdentity, currentIdentity, StringComparison.Ordinal))
        {
            return;
        }

        await mClient.InvalidateFastChannelConnectionsAsync(engineId).ConfigureAwait(false);
        lock (mSyncRoot)
        {
            if (mDisposed || !string.Equals(mEngineId, engineId, StringComparison.Ordinal)
                || !string.Equals(mIdentity, previousIdentity, StringComparison.Ordinal))
            {
                return;
            }

            mIdentity = currentIdentity;
        }

        PublishChanged(new EngineLifecycleChangedEventArgs(engineId, previousIdentity, currentIdentity));
    }

    /// <summary>
    /// 逐个通知生命周期订阅者，单个 UI 订阅者失败不会阻断其它订阅者或后台任务链。
    /// </summary>
    /// <param name="eventArgs">已经提交的身份变化。</param>
    private void PublishChanged(EngineLifecycleChangedEventArgs eventArgs)
    {
        var handlers = Changed;
        if (handlers == null)
        {
            return;
        }

        mPublishingChanged.Value = true;
        try
        {
            foreach (EventHandler<EngineLifecycleChangedEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, eventArgs);
                }
                catch (Exception exception)
                {
                    Trace.TraceError("Engine lifecycle change subscriber failed: " + exception);
                }
            }
        }
        finally
        {
            mPublishingChanged.Value = false;
        }
    }

    /// <summary>
    /// 读取 registry/heartbeat 的生命周期身份；普通 heartbeat sequence 变化不会进入身份串。
    /// </summary>
    private string ReadIdentity(string engineId)
    {
        if (string.IsNullOrWhiteSpace(engineId))
        {
            return string.Empty;
        }

        try
        {
            var registry = mClient.ReadEngineEntries().FirstOrDefault(
                entry => string.Equals(entry.EngineId, engineId, StringComparison.Ordinal));
            if (registry == null)
            {
                return "missing";
            }

            var endpointIdentity = string.Join(
                ";",
                registry.FastChannels.Select(endpoint => string.Join(
                    ":",
                    endpoint.Transport,
                    endpoint.Endpoint,
                    endpoint.Enabled ? "1" : "0",
                    endpoint.SessionId,
                    endpoint.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            var heartbeat = mClient.ReadHeartbeat(engineId);
            return string.Join(
                "|",
                registry.SessionId,
                registry.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture),
                endpointIdentity,
                heartbeat?.SessionId ?? string.Empty,
                heartbeat?.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        }
        catch
        {
            return "unavailable";
        }
    }

    /// <summary>
    /// 停止当前 watcher 和 debounce timer，并解除事件引用。
    /// </summary>
    private void StopWatchers()
    {
        DisposeWatcher(mEngineWatcher);
        DisposeWatcher(mStatusWatcher);
        mEngineWatcher = null;
        mStatusWatcher = null;
        mDebounceTimer?.Dispose();
        mDebounceTimer = null;
    }

    /// <summary>
    /// 释放单个 watcher。
    /// </summary>
    private static void DisposeWatcher(FileSystemWatcher? watcher)
    {
        if (watcher == null)
        {
            return;
        }

        watcher.EnableRaisingEvents = false;
        watcher.Dispose();
    }

    /// <summary>
    /// 抛出对象已释放错误，阻止关闭后的新监听。
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (mDisposed)
        {
            throw new ObjectDisposedException(nameof(EngineLifecycleMonitor));
        }
    }
}

/// <summary>
/// 表示宿主生命周期身份发生变化的通知。
/// </summary>
public sealed class EngineLifecycleChangedEventArgs : EventArgs
{
    /// <summary>
    /// 创建生命周期变化通知。
    /// </summary>
    /// <param name="engineId">发生变化的 engine。</param>
    /// <param name="previousIdentity">变化前身份摘要。</param>
    /// <param name="currentIdentity">变化后身份摘要。</param>
    public EngineLifecycleChangedEventArgs(string engineId, string previousIdentity, string currentIdentity)
    {
        EngineId = engineId;
        PreviousIdentity = previousIdentity;
        CurrentIdentity = currentIdentity;
    }

    /// <summary>获取发生变化的 engine。</summary>
    public string EngineId { get; }

    /// <summary>获取变化前身份摘要。</summary>
    public string PreviousIdentity { get; }

    /// <summary>获取变化后身份摘要。</summary>
    public string CurrentIdentity { get; }
}
