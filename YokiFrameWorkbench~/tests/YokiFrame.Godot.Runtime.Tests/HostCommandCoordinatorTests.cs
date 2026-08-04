using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 验证 FileBridge Host 命令生命周期协调器不依赖具体宿主协议和路径实现。
/// </summary>
public sealed class HostCommandCoordinatorTests
{
    /// <summary>
    /// 验证协调器保持存储适配器提供的顺序，并在 response 后归档每个已 claim 命令。
    /// </summary>
    [Fact]
    public void ProcessPendingCommandsPreservesStoreOrderAndTerminalSequence()
    {
        FakeHostCommandStore store = new(new[] { "second", "first" });
        YokiFrameHostCommandCoordinator coordinator = new(
            store,
            path => new YokiFrameHostCommandExecution(path, "response-" + path),
            TimeSpan.FromMinutes(1),
            utcNow: () => new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc));

        int claimedCount = coordinator.ProcessPendingCommands();

        Assert.Equal(2, claimedCount);
        Assert.Equal(
            new[]
            {
                "write:second",
                "archive:second",
                "write:first",
                "archive:first"
            },
            store.Operations);
    }

    /// <summary>
    /// 验证单条执行异常只产生 CommandProcessingFailed deadletter，并继续消费后续命令。
    /// </summary>
    [Fact]
    public void ExecutorFailureDeadlettersCommandAndContinues()
    {
        FakeHostCommandStore store = new(new[] { "bad", "good" });
        YokiFrameHostCommandCoordinator coordinator = new(
            store,
            path => path == "bad"
                ? throw new InvalidOperationException("bad command")
                : new YokiFrameHostCommandExecution(path, "ok"),
            TimeSpan.FromMinutes(1));

        int claimedCount = coordinator.ProcessPendingCommands();

        Assert.Equal(2, claimedCount);
        Assert.Contains("deadletter:bad:CommandProcessingFailed", store.Operations);
        Assert.Contains("write:good", store.Operations);
        Assert.Contains("archive:good", store.Operations);
    }

    /// <summary>
    /// 验证 processing lease 过期命令进入 deadletter，且不会调用宿主 executor 重放 mutation。
    /// </summary>
    [Fact]
    public void ExpiredProcessingIsDeadletteredWithoutReplay()
    {
        FakeHostCommandStore store = new(Array.Empty<string>());
        store.ProcessingPaths.Add("expired");
        store.LastWriteTimes["expired"] = new DateTime(2026, 8, 1, 23, 0, 0, DateTimeKind.Utc);
        var executorCalls = 0;
        YokiFrameHostCommandCoordinator coordinator = new(
            store,
            path =>
            {
                executorCalls++;
                return new YokiFrameHostCommandExecution(path, "unexpected");
            },
            TimeSpan.FromMinutes(1),
            utcNow: () => new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc));

        int claimedCount = coordinator.ProcessPendingCommands();

        Assert.Equal(0, claimedCount);
        Assert.Equal(0, executorCalls);
        Assert.Contains("deadletter:expired:ProcessingExpired", store.Operations);
    }

    /// <summary>
    /// 验证同一个协调器在执行回调中被重入时立即返回零，不重复消费当前批次。
    /// </summary>
    [Fact]
    public void ReentrantProcessingReturnsZero()
    {
        FakeHostCommandStore store = new(new[] { "one" });
        YokiFrameHostCommandCoordinator coordinator = null!;
        var nestedResult = -1;
        coordinator = new YokiFrameHostCommandCoordinator(
            store,
            path =>
            {
                nestedResult = coordinator.ProcessPendingCommands();
                return new YokiFrameHostCommandExecution(path, "ok");
            },
            TimeSpan.FromMinutes(1));

        Assert.Equal(1, coordinator.ProcessPendingCommands());
        Assert.Equal(0, nestedResult);
    }

    /// <summary>
    /// 验证 commands 根目录不存在时不读取 processing，并执行宿主指定的缺失目录清理回调。
    /// </summary>
    [Fact]
    public void MissingPendingRootUsesStoreCleanupAndReturnsZero()
    {
        FakeHostCommandStore store = new(Array.Empty<string>())
        {
            PendingRootExists = false
        };
        YokiFrameHostCommandCoordinator coordinator = new(
            store,
            path => new YokiFrameHostCommandExecution(path, "unexpected"),
            TimeSpan.FromMinutes(1));

        Assert.Equal(0, coordinator.ProcessPendingCommands());
        Assert.True(store.PruneWhenPendingRootMissingCalled);
        Assert.Empty(store.Operations);
    }

    /// <summary>
    /// 验证 deadletter 写入失败只记录诊断，不阻塞同一批次的后续命令。
    /// </summary>
    [Fact]
    public void DeadletterFailureDoesNotStopFollowingCommands()
    {
        FakeHostCommandStore store = new(new[] { "bad", "good" })
        {
            ThrowOnDeadletter = true
        };
        var diagnostics = 0;
        YokiFrameHostCommandCoordinator coordinator = new(
            store,
            path => path == "bad"
                ? throw new InvalidOperationException("bad command")
                : new YokiFrameHostCommandExecution(path, "ok"),
            TimeSpan.FromMinutes(1),
            _ => diagnostics++);

        Assert.Equal(2, coordinator.ProcessPendingCommands());
        Assert.Contains("write:good", store.Operations);
        Assert.Contains("archive:good", store.Operations);
        Assert.True(diagnostics >= 2);
    }

    /// <summary>
    /// 验证 response 和 deadletter 同时失败时，processing 旁仍保留可持久读取的失败证据。
    /// </summary>
    [Fact]
    public void ResponseAndDeadletterFailureLeavesProcessingEvidence()
    {
        FakeHostCommandStore store = new(new[] { "one" })
        {
            ThrowOnWriteResponse = true,
            ThrowOnDeadletter = true
        };
        YokiFrameHostCommandCoordinator coordinator = new(
            store,
            path => new YokiFrameHostCommandExecution(path, "ok"),
            TimeSpan.FromMinutes(1));

        Assert.Equal(1, coordinator.ProcessPendingCommands());
        Assert.Contains("failure-evidence:one:CommandExecutionUnknown", store.Operations);
        Assert.Equal("CommandExecutionUnknown", store.ProcessingFailureEvidence["one"].ErrorCode);
        Assert.Contains("response store unavailable", store.ProcessingFailureEvidence["one"].ErrorMessage);
        Assert.Contains("Deadletter evidence write failed", store.ProcessingFailureEvidence["one"].ErrorMessage);
    }

    /// <summary>
    /// 验证 terminal response 已提交后 archive 失败不会再生成互相矛盾的失败 deadletter。
    /// </summary>
    [Fact]
    public void ArchiveFailureDoesNotRewriteCommittedResponseAsDeadletter()
    {
        FakeHostCommandStore store = new(new[] { "one" })
        {
            ThrowOnArchive = true
        };
        var diagnostics = 0;
        YokiFrameHostCommandCoordinator coordinator = new(
            store,
            path => new YokiFrameHostCommandExecution(path, "ok"),
            TimeSpan.FromMinutes(1),
            _ => diagnostics++);

        Assert.Equal(1, coordinator.ProcessPendingCommands());
        Assert.Contains("write:one", store.Operations);
        Assert.DoesNotContain(store.Operations, operation => operation.StartsWith("deadletter:", StringComparison.Ordinal));
        Assert.True(diagnostics >= 1);
        Assert.True(store.ArchiveRetryable);
    }

    /// <summary>
    /// 验证 response 写入失败使用 Unknown 证据，调用方不能把已执行命令误判为普通处理失败。
    /// </summary>
    [Fact]
    public void ResponseWriteFailureUsesUnknownOutcomeEvidence()
    {
        FakeHostCommandStore store = new(new[] { "one" })
        {
            ThrowOnWriteResponse = true
        };
        YokiFrameHostCommandCoordinator coordinator = new(
            store,
            path => new YokiFrameHostCommandExecution(path, "ok"),
            TimeSpan.FromMinutes(1));

        Assert.Equal(1, coordinator.ProcessPendingCommands());
        Assert.Contains("deadletter:one:CommandExecutionUnknown", store.Operations);
    }

    /// <summary>
    /// 验证命令数量预算生效，单轮不会消费整个 backlog。
    /// </summary>
    [Fact]
    public void BatchCommandLimitLeavesBacklogForNextPoll()
    {
        FakeHostCommandStore store = new(new[] { "one", "two", "three" });
        YokiFrameHostCommandCoordinator coordinator = new(
            store,
            path => new YokiFrameHostCommandExecution(path, "ok"),
            TimeSpan.FromMinutes(1),
            maxCommandsPerBatch: 2,
            maxBatchDuration: TimeSpan.FromMinutes(1));

        Assert.Equal(2, coordinator.ProcessPendingCommands());
        Assert.True(coordinator.LastBatchWasLimited);
        Assert.Equal("maxCommandsPerBatch", coordinator.LastBatchLimitReason);
    }

    /// <summary>
    /// 验证 processing lease 刷新失败只 deadletter 当前命令，并继续处理后续 backlog。
    /// </summary>
    [Fact]
    public void LeaseRefreshFailureDoesNotStopFollowingCommands()
    {
        FakeHostCommandStore store = new(new[] { "bad", "good" })
        {
            ThrowOnLeaseRefresh = true
        };
        YokiFrameHostCommandCoordinator coordinator = new(
            store,
            path => new YokiFrameHostCommandExecution(path, "ok"),
            TimeSpan.FromMinutes(1),
            maxBatchDuration: TimeSpan.FromMinutes(1));

        Assert.Equal(2, coordinator.ProcessPendingCommands());
        Assert.Contains("deadletter:bad:CommandProcessingFailed", store.Operations);
        Assert.Contains("write:good", store.Operations);
    }

    /// <summary>
    /// 验证过期请求在策略和 handler 之前被拒绝，不会执行潜在 mutation。
    /// </summary>
    [Fact]
    public void DispatcherRejectsExpiredRequestBeforeHandler()
    {
        RecordingCommandHandler handler = new();
        YokiFrameCommandDispatcher dispatcher = new(
            YokiFrameCommandPolicy.CreateDefault(),
            new IYokiFrameCommandHandler[] { handler });
        DateTimeOffset createdAtUtc = new(2026, 8, 2, 0, 0, 0, TimeSpan.Zero);
        YokiFrameCommandRequest request = new(
            "cli",
            "System",
            "ping",
            "{}",
            1000,
            0L,
            "expired-request",
            createdAtUtc);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            request,
            createdAtUtc.AddSeconds(2));

        Assert.False(result.IsSuccess);
        Assert.Equal("CommandExpired", result.ErrorCode);
        Assert.False(handler.WasInvoked);
    }

    /// <summary>
    /// 验证 requestId 和创建时间可以到达 handler，供幂等与审计逻辑使用。
    /// </summary>
    [Fact]
    public void DispatcherPreservesRequestContextForHandler()
    {
        RecordingCommandHandler handler = new();
        YokiFrameCommandDispatcher dispatcher = new(
            YokiFrameCommandPolicy.CreateDefault(),
            new IYokiFrameCommandHandler[] { handler });
        DateTimeOffset createdAtUtc = new(2026, 8, 2, 0, 0, 0, TimeSpan.Zero);
        YokiFrameCommandRequest request = new(
            "cli",
            "System",
            "ping",
            "{}",
            1000,
            0L,
            "context-request",
            createdAtUtc);

        YokiFrameCommandResult result = dispatcher.Dispatch(
            request,
            createdAtUtc.AddMilliseconds(500));

        Assert.True(result.IsSuccess);
        Assert.Equal("context-request", handler.RequestId);
        Assert.Equal(createdAtUtc, handler.CreatedAtUtc);
    }

    /// <summary>
    /// 提供不依赖文件系统的 HostCommandCoordinator 测试存储。
    /// </summary>
    private sealed class FakeHostCommandStore : IYokiFrameHostCommandStore
    {
        /// <summary>
        /// 创建 fake store。
        /// </summary>
        /// <param name="pendingPaths">待处理路径。</param>
        public FakeHostCommandStore(IEnumerable<string> pendingPaths)
        {
            PendingPaths = pendingPaths.ToArray();
        }

        /// <summary>获取 pending 路径。</summary>
        public IReadOnlyList<string> PendingPaths { get; }

        /// <summary>获取 processing 路径。</summary>
        public List<string> ProcessingPaths { get; } = new();

        /// <summary>获取路径最后写入时间。</summary>
        public Dictionary<string, DateTime> LastWriteTimes { get; } = new();

        /// <summary>获取生命周期操作记录。</summary>
        public List<string> Operations { get; } = new();

        /// <summary>获取或设置 pending 根目录是否存在。</summary>
        public bool PendingRootExists { get; set; } = true;

        /// <summary>获取缺失 pending 根目录清理是否执行。</summary>
        public bool PruneWhenPendingRootMissingCalled { get; private set; }

        /// <summary>获取是否模拟 deadletter 写入失败。</summary>
        public bool ThrowOnDeadletter { get; set; }

        /// <summary>获取 processing 旁失败证据。</summary>
        public Dictionary<string, (string ErrorCode, string ErrorMessage)> ProcessingFailureEvidence { get; }
            = new(StringComparer.Ordinal);

        /// <summary>获取是否模拟 response 写入失败。</summary>
        public bool ThrowOnWriteResponse { get; set; }

        /// <summary>获取是否模拟 archive 失败。</summary>
        public bool ThrowOnArchive { get; set; }

        /// <summary>获取是否模拟 processing lease 刷新失败。</summary>
        public bool ThrowOnLeaseRefresh { get; set; }

        /// <summary>获取 archive 是否可在下一轮重试。</summary>
        public bool ArchiveRetryable { get; private set; }

        /// <summary>准备 fake store。</summary>
        public void EnsureReady()
        {
        }

        /// <summary>读取 pending 路径。</summary>
        public IReadOnlyList<string> ReadPendingCommandPaths() => PendingPaths;

        /// <summary>读取 processing 路径。</summary>
        public IReadOnlyList<string> ReadProcessingCommandPaths() => ProcessingPaths;

        /// <summary>claim 一个 pending 路径。</summary>
        public YokiFrameFileBridgeClaimResult TryClaim(
            string pendingPath,
            out string claimedPath,
            out Exception storageException)
        {
            claimedPath = pendingPath;
            storageException = null!;
            return YokiFrameFileBridgeClaimResult.Claimed;
        }

        /// <summary>删除过期 marker。</summary>
        public void RemoveExpiredMarkers(DateTime cutoffUtc)
        {
            Operations.Add("remove-markers");
        }

        /// <summary>读取最后写入时间。</summary>
        public DateTime GetLastWriteTimeUtc(string path)
        {
            return LastWriteTimes.TryGetValue(path, out DateTime value)
                ? value
                : DateTime.UtcNow;
        }

        /// <summary>刷新 fake processing lease，不改变测试路径顺序。</summary>
        public void RefreshProcessingLease(string commandPath, DateTime claimedAtUtc)
        {
            if (ThrowOnLeaseRefresh && commandPath == "bad")
            {
                throw new IOException("lease store unavailable");
            }
        }

        /// <summary>判断 fake response 是否存在，供 archive 重试测试使用。</summary>
        public bool HasTerminalResponse(string commandPath)
        {
            return ArchiveRetryable && commandPath == "one";
        }

        /// <summary>写入 terminal response。</summary>
        public void WriteResponse(string requestId, string responseJson)
        {
            if (ThrowOnWriteResponse)
            {
                throw new IOException("response store unavailable");
            }

            Operations.Add("write:" + requestId);
        }

        /// <summary>归档已完成命令。</summary>
        public void Archive(string commandPath)
        {
            if (ThrowOnArchive)
            {
                ArchiveRetryable = true;
                throw new IOException("archive store unavailable");
            }

            Operations.Add("archive:" + commandPath);
        }

        /// <summary>记录 deadletter。</summary>
        public void MoveToDeadletter(string commandPath, string errorCode, string errorMessage)
        {
            if (ThrowOnDeadletter)
            {
                throw new IOException("deadletter store unavailable");
            }

            Operations.Add("deadletter:" + commandPath + ":" + errorCode);
        }

        /// <summary>在 processing 旁保存 deadletter 失败时的终态线索。</summary>
        public void WriteProcessingFailureEvidence(
            string commandPath,
            string errorCode,
            string errorMessage)
        {
            ProcessingFailureEvidence[commandPath] = (errorCode, errorMessage);
            Operations.Add("failure-evidence:" + commandPath + ":" + errorCode);
        }

        /// <summary>记录批次清理。</summary>
        public void PruneAfterBatch()
        {
        }

        /// <summary>记录缺失根目录清理。</summary>
        public void PruneWhenPendingRootMissing()
        {
            PruneWhenPendingRootMissingCalled = true;
        }
    }

    /// <summary>
    /// 记录 dispatcher 是否传递完整请求上下文的测试 handler。
    /// </summary>
    private sealed class RecordingCommandHandler : IYokiFrameCommandHandler
    {
        /// <summary>获取 handler 是否被调用。</summary>
        public bool WasInvoked { get; private set; }

        /// <summary>获取收到的 requestId。</summary>
        public string RequestId { get; private set; } = string.Empty;

        /// <summary>获取收到的创建时间。</summary>
        public DateTimeOffset CreatedAtUtc { get; private set; }

        /// <summary>匹配 System/ping。</summary>
        public bool CanHandle(YokiFrameCommandRequest request)
        {
            return request.Kit == "System" && request.Action == "ping";
        }

        /// <summary>记录上下文并返回成功结果。</summary>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            WasInvoked = true;
            RequestId = request.RequestId;
            CreatedAtUtc = request.CreatedAtUtc;
            return YokiFrameCommandResult.Success("{}");
        }
    }
}
