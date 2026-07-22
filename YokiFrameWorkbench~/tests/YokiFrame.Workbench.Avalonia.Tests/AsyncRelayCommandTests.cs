using System.Diagnostics;
using System.Globalization;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 验证 Avalonia 异步命令在执行期间阻止重复提交，并在完成后恢复状态。
/// </summary>
public sealed class AsyncRelayCommandTests
{
    /// <summary>
    /// 验证同一异步操作尚未完成时，后续执行不会再次调用委托。
    /// </summary>
    [Fact]
    public async Task ExecuteAsyncPreventsConcurrentInvocation()
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        AsyncRelayCommand command = new(async () =>
        {
            invocationCount++;
            await completion.Task;
        });

        var first = command.ExecuteAsync();
        var second = command.ExecuteAsync();

        Assert.False(command.CanExecute(null));
        Assert.Equal(1, invocationCount);
        Assert.True(second.IsCompletedSuccessfully);

        completion.SetResult();
        await first;

        Assert.True(command.CanExecute(null));
    }

    /// <summary>
    /// 验证外部条件变化后可以主动刷新命令启用状态。
    /// </summary>
    [Fact]
    public void RaiseCanExecuteChangedPublishesNotification()
    {
        var enabled = false;
        AsyncRelayCommand command = new(() => Task.CompletedTask, () => enabled);
        var notificationCount = 0;
        command.CanExecuteChanged += (_, _) => notificationCount++;

        enabled = true;
        command.RaiseCanExecuteChanged();

        Assert.True(command.CanExecute(null));
        Assert.Equal(1, notificationCount);
    }

    /// <summary>
    /// 验证 ICommand 的 fire-and-forget 入口记录委托异常，而不会把异常抛回 Avalonia UI 线程。
    /// </summary>
    [Fact]
    public async Task ExecuteEntryObservesDelegateFailureWithoutEscapingAsyncVoidBoundary()
    {
        const string marker = "async-command-boundary-test";
        TaskCompletionSource<string> logged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using TraceListener listener = new DelegateTraceListener(message =>
        {
            if (message.Contains(marker, StringComparison.Ordinal))
            {
                logged.TrySetResult(message);
            }
        });
        Trace.Listeners.Add(listener);
        try
        {
            AsyncRelayCommand command = new(async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException(marker);
            });

            command.Execute(null);
            Task completed = await Task.WhenAny(logged.Task, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.Same(logged.Task, completed);
            Assert.Contains(marker, await logged.Task);
            Assert.True(command.CanExecute(null));
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    /// <summary>
    /// 用最小 TraceListener 捕获异步命令边界的诊断文本，避免测试依赖控制台输出。
    /// </summary>
    private sealed class DelegateTraceListener(Action<string> onMessage) : TraceListener
    {
        /// <summary>接收无换行 Trace 写入。</summary>
        public override void Write(string? message)
        {
        }

        /// <summary>把 Trace 写入转发给测试断言。</summary>
        public override void WriteLine(string? message)
        {
            onMessage(message ?? string.Empty);
        }

        /// <summary>格式化 TraceEvent，兼容 Trace.TraceError 的参数化调用。</summary>
        public override void TraceEvent(
            TraceEventCache? eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string? format,
            params object?[]? args)
        {
            string message = args == null || string.IsNullOrEmpty(format)
                ? format ?? string.Empty
                : string.Format(CultureInfo.InvariantCulture, format, args);
            onMessage(message);
        }
    }
}
