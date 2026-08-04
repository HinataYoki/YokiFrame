using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 通过项目级系统互斥量和当前用户本机管道协调 Workbench 单实例与窗口激活。
/// </summary>
public sealed class WorkbenchActivationCoordinator : IDisposable
{
    private const string ACTIVATION_MESSAGE = "activate";
    private const string ACTIVATION_ACKNOWLEDGED = "ack";
    private const string ACTIVATION_REJECTED = "nak";
    private const string PIPE_NAME_PREFIX = "yokiframe-workbench-";
    private const string MUTEX_NAME_SUFFIX = "-owner";
    private const int ACTIVATION_CONNECT_TIMEOUT_MS = 750;
    private const int ACTIVATION_RESPONSE_TIMEOUT_MS = 1500;
    private const int ACTIVATION_HANDLER_WAIT_ATTEMPTS = 20;
    private const int ACTIVATION_HANDLER_WAIT_DELAY_MS = 50;
    private const int COORDINATION_ATTEMPT_COUNT = 2;
    private const int COORDINATION_RETRY_DELAY_MS = 50;
    private static readonly object sOwnershipGate = new();
    private static readonly HashSet<string> sOwnedMutexNames = new(StringComparer.Ordinal);

    private readonly string mPipeName;
    private readonly Mutex? mInstanceMutex;
    private readonly CancellationTokenSource mLifetime = new();
    private readonly Task? mListenerTask;
    private bool mDisposed;

    /// <summary>
    /// 创建协调器，并在 owner 实例中立即启动本机激活监听。
    /// </summary>
    /// <param name="pipeName">按项目隔离的管道名。</param>
    /// <param name="instanceMutex">owner 持有的项目互斥量句柄；非 owner 为空。</param>
    /// <param name="activationRedirected">是否已把启动请求重定向到现有 owner。</param>
    /// <param name="coordinationDegraded">是否因现有 owner 无法确认而降级启动。</param>
    private WorkbenchActivationCoordinator(
        string pipeName,
        Mutex? instanceMutex,
        bool activationRedirected,
        bool coordinationDegraded)
    {
        mPipeName = pipeName;
        mInstanceMutex = instanceMutex;
        ActivationRedirected = activationRedirected;
        CoordinationDegraded = coordinationDegraded;
        mListenerTask = instanceMutex == null
            ? null
            : Task.Run(ListenForActivationAsync);
    }

    /// <summary>
    /// 当同项目后续进程请求显示已有 Workbench 时触发。
    /// </summary>
    public event EventHandler<WorkbenchActivationRequestEventArgs>? ActivationRequested;

    /// <summary>
    /// 获取当前协调器是否持有项目级单实例锁。
    /// </summary>
    public bool IsPrimaryInstance => mInstanceMutex != null;

    /// <summary>
    /// 获取当前启动是否已经通知已有 owner，调用方此时应结束进程。
    /// </summary>
    public bool ActivationRedirected { get; }

    /// <summary>
    /// 获取本次启动是否因已有 owner 不可响应而进入可观测的无协调降级模式。
    /// </summary>
    public bool CoordinationDegraded { get; }

    /// <summary>
    /// 为指定项目启动单实例协调；管道不可用时允许新实例继续启动，避免工具完全不可用。
    /// </summary>
    /// <param name="projectRoot">Workbench 对应项目根。</param>
    /// <returns>描述 owner 或重定向结果的协调器。</returns>
    public static WorkbenchActivationCoordinator Start(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        var fullProjectRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
        var pipeName = CreatePipeName(fullProjectRoot);
        for (var attempt = 0; attempt < COORDINATION_ATTEMPT_COUNT; attempt++)
        {
            var instanceMutex = TryCreateInstanceMutex(pipeName + MUTEX_NAME_SUFFIX);
            if (instanceMutex != null)
            {
                return CreatePrimary(pipeName, instanceMutex);
            }

            var sendResult = TrySendActivationRequest(pipeName);
            if (sendResult == ActivationSendResult.Acknowledged)
            {
                return new WorkbenchActivationCoordinator(
                    pipeName,
                    null,
                    activationRedirected: true,
                    coordinationDegraded: false);
            }

            if (sendResult == ActivationSendResult.Rejected)
            {
                break;
            }

            Thread.Sleep(COORDINATION_RETRY_DELAY_MS);
        }

        var takeoverMutex = TryCreateInstanceMutex(pipeName + MUTEX_NAME_SUFFIX);
        return takeoverMutex != null
            ? CreatePrimary(pipeName, takeoverMutex)
            : new WorkbenchActivationCoordinator(
                pipeName,
                null,
                activationRedirected: false,
                coordinationDegraded: true);
    }

    /// <summary>
    /// 释放监听和项目锁；锁由操作系统随句柄关闭立即释放。
    /// </summary>
    public void Dispose()
    {
        if (mDisposed)
        {
            return;
        }

        mDisposed = true;
        mLifetime.Cancel();
        WaitForListenerCompletion();
        if (mInstanceMutex != null)
        {
            try
            {
                mInstanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                Trace.TraceError("Workbench activation mutex was not owned during shutdown.");
            }
            finally
            {
                mInstanceMutex.Dispose();
                lock (sOwnershipGate)
                {
                    sOwnedMutexNames.Remove(mPipeName + MUTEX_NAME_SUFFIX);
                }
            }
        }
        mLifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 创建持有项目锁并监听激活请求的 owner 协调器。
    /// </summary>
    /// <param name="pipeName">按项目隔离的管道名。</param>
    /// <param name="instanceMutex">当前进程取得的 owner 互斥量。</param>
    /// <returns>已启动监听的 owner 协调器。</returns>
    private static WorkbenchActivationCoordinator CreatePrimary(string pipeName, Mutex instanceMutex)
    {
        return new WorkbenchActivationCoordinator(
            pipeName,
            instanceMutex,
            activationRedirected: false,
            coordinationDegraded: false);
    }

    /// <summary>
    /// 创建按项目哈希命名的系统互斥量并显式取得 ownership；已有 owner 或平台拒绝创建时返回空。
    /// </summary>
    /// <param name="mutexName">不暴露项目路径的互斥量名称。</param>
    /// <returns>首个实例持有的互斥量句柄；未取得 owner 资格时为空。</returns>
    private static Mutex? TryCreateInstanceMutex(string mutexName)
    {
        lock (sOwnershipGate)
        {
            // Named Mutex 在同一线程上允许递归 WaitOne；进程内 guard 保证测试宿主或嵌入式
            // Workbench 不会把递归 ownership 误判成第二个 primary 实例。
            if (sOwnedMutexNames.Contains(mutexName))
            {
                return null;
            }
        }

        try
        {
            Mutex mutex = new(initiallyOwned: false, mutexName, out _);
            var acquired = false;
            try
            {
                acquired = mutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                // Abandoned 表示前 owner 已退出；当前进程已经取得该 Mutex 的 ownership。
                acquired = true;
            }

            if (!acquired)
            {
                mutex.Dispose();
                return null;
            }

            lock (sOwnershipGate)
            {
                if (!sOwnedMutexNames.Add(mutexName))
                {
                    mutex.ReleaseMutex();
                    mutex.Dispose();
                    return null;
                }
            }

            return mutex;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// 规范化项目根并哈希为不暴露本地路径的当前用户管道名。
    /// </summary>
    /// <param name="projectRoot">规范化项目根。</param>
    /// <returns>稳定、短小的管道名。</returns>
    private static string CreatePipeName(string projectRoot)
    {
        var identity = OperatingSystem.IsWindows()
            ? projectRoot.ToUpperInvariant()
            : projectRoot;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return PIPE_NAME_PREFIX + Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
    }

    /// <summary>
    /// 在限定时间内向现有 owner 写入激活消息，并等待窗口明确确认。
    /// </summary>
    /// <param name="pipeName">目标项目管道名。</param>
    /// <returns>owner 的确认结果；通道不可用时返回 Unavailable。</returns>
    private static ActivationSendResult TrySendActivationRequest(string pipeName)
    {
        try
        {
            using NamedPipeClientStream client = new(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            client.Connect(ACTIVATION_CONNECT_TIMEOUT_MS);
            using StreamWriter writer = new(client, new UTF8Encoding(false), leaveOpen: true)
            {
                AutoFlush = true
            };
            writer.WriteLine(ACTIVATION_MESSAGE);
            using StreamReader reader = new(client, Encoding.UTF8, leaveOpen: true);
            using CancellationTokenSource responseTimeout = new(ACTIVATION_RESPONSE_TIMEOUT_MS);
            var response = reader
                .ReadLineAsync(responseTimeout.Token)
                .AsTask()
                .GetAwaiter()
                .GetResult();
            return string.Equals(response, ACTIVATION_ACKNOWLEDGED, StringComparison.Ordinal)
                ? ActivationSendResult.Acknowledged
                : ActivationSendResult.Rejected;
        }
        catch (IOException)
        {
            return ActivationSendResult.Unavailable;
        }
        catch (TimeoutException)
        {
            return ActivationSendResult.Unavailable;
        }
        catch (UnauthorizedAccessException)
        {
            return ActivationSendResult.Unavailable;
        }
        catch (OperationCanceledException)
        {
            return ActivationSendResult.Unavailable;
        }
    }

    /// <summary>
    /// 持续接受短连接激活消息，单次连接损坏不会终止后续监听。
    /// </summary>
    /// <returns>协调器释放后结束的监听任务。</returns>
    private async Task ListenForActivationAsync()
    {
        while (!mLifetime.IsCancellationRequested)
        {
            try
            {
                await ListenOnceAsync(mLifetime.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (mLifetime.IsCancellationRequested)
            {
                return;
            }
            catch (IOException) when (!mLifetime.IsCancellationRequested)
            {
                await DelayAfterListenerFailureAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 接受一个当前用户连接并只识别固定激活消息。
    /// </summary>
    /// <param name="cancellationToken">协调器生命周期令牌。</param>
    /// <returns>连接处理完成任务。</returns>
    private async Task ListenOnceAsync(CancellationToken cancellationToken)
    {
        using NamedPipeServerStream server = new(
            mPipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        using StreamReader reader = new(server, Encoding.UTF8, leaveOpen: true);
        var message = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        var accepted = false;
        if (string.Equals(message, ACTIVATION_MESSAGE, StringComparison.Ordinal))
        {
            accepted = await WaitForActivationHandlerAsync(cancellationToken).ConfigureAwait(false);
        }

        using StreamWriter writer = new(server, new UTF8Encoding(false), leaveOpen: false)
        {
            AutoFlush = true
        };
        writer.WriteLine(accepted ? ACTIVATION_ACKNOWLEDGED : ACTIVATION_REJECTED);
    }

    /// <summary>
    /// 短暂等待主窗口完成订阅，并只在接收者明确确认后返回成功。
    /// </summary>
    /// <param name="cancellationToken">协调器生命周期令牌。</param>
    /// <returns>窗口已接管激活请求时返回 true。</returns>
    private async Task<bool> WaitForActivationHandlerAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < ACTIVATION_HANDLER_WAIT_ATTEMPTS; attempt++)
        {
            var handler = ActivationRequested;
            if (handler != null)
            {
                WorkbenchActivationRequestEventArgs request = new();
                try
                {
                    handler.Invoke(this, request);
                }
                catch (Exception exception)
                {
                    Trace.TraceError("Workbench activation handler failed: {0}", exception);
                    return false;
                }

                return request.IsAccepted;
            }

            await Task.Delay(ACTIVATION_HANDLER_WAIT_DELAY_MS, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>
    /// 等待后台监听器真正退出并观察异常，再释放项目锁，避免新旧 owner 监听重叠。
    /// </summary>
    private void WaitForListenerCompletion()
    {
        if (mListenerTask == null)
        {
            return;
        }

        try
        {
            mListenerTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (mLifetime.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Trace.TraceError("Workbench activation listener failed during shutdown: {0}", exception);
        }
    }

    /// <summary>
    /// 监听器遇到瞬时 IO 失败后短暂退避，防止异常循环占满 CPU。
    /// </summary>
    /// <returns>退避完成或协调器取消时结束的任务。</returns>
    private async Task DelayAfterListenerFailureAsync()
    {
        try
        {
            await Task.Delay(50, mLifetime.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (mLifetime.IsCancellationRequested)
        {
        }
    }

    /// <summary>
    /// 表示向现有 owner 发送激活请求后的握手结果。
    /// </summary>
    private enum ActivationSendResult
    {
        Unavailable,
        Rejected,
        Acknowledged
    }
}
