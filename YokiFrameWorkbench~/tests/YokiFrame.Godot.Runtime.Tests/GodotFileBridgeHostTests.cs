using System.Text.Json.Nodes;
using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 验证 Godot Runtime FileBridge 的状态、命令终态、会话和退出释放行为。
/// </summary>
[Collection(GodotFileBridgeHostCollection.NAME)]
public sealed partial class GodotFileBridgeHostTests
{
    /// <summary>
    /// 验证启动后的 registry 会发布当前会话对应的本机 FastChannel endpoint，供工具侧优先尝试低延迟只读通道。
    /// </summary>
    [Fact]
    public void StartPublishesEnabledLocalFastChannelEndpoint()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");

        host.Start();

        var registry = GodotFileBridgeHostFixture.ReadObject(fixture.RegistryPath);
        var endpoints = registry["fastChannels"]?.AsArray();
        Assert.NotNull(endpoints);
        var endpoint = Assert.Single(endpoints!);
        Assert.Equal(1, endpoint?["protocolVersion"]?.GetValue<int>());
        Assert.Equal(GodotFileBridgeHostFixture.ENGINE_ID, endpoint?["engineId"]?.GetValue<string>());
        Assert.Equal(host.SessionId, endpoint?["sessionId"]?.GetValue<string>());
        Assert.Equal(host.Generation, endpoint?["generation"]?.GetValue<long>());
        Assert.True(endpoint?["enabled"]?.GetValue<bool>());

        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("namedPipe", endpoint?["transport"]?.GetValue<string>());
            Assert.True(YokiFrameSafeIdContract.IsSafeId(endpoint?["endpoint"]?.GetValue<string>() ?? string.Empty));
            return;
        }

        Assert.True(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS());
        Assert.Equal("unixDomainSocket", endpoint?["transport"]?.GetValue<string>());
        Assert.True(Path.IsPathFullyQualified(endpoint?["endpoint"]?.GetValue<string>() ?? string.Empty));
    }

    /// <summary>
    /// 验证 Unix Domain Socket 只允许当前用户读写，避免同机其它用户连接本机控制面。
    /// </summary>
    [Fact]
    public void StartRestrictsUnixSocketToCurrentUser()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.True(OperatingSystem.IsLinux() || OperatingSystem.IsMacOS());
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");

        host.Start();

        var endpoint = fixture.ReadFastChannelEndpoint();
        Assert.Equal("unixDomainSocket", endpoint["transport"]?.GetValue<string>());
        var socketPath = endpoint["endpoint"]?.GetValue<string>() ?? string.Empty;
        Assert.True(File.Exists(socketPath));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(socketPath));
    }

    /// <summary>
    /// 验证已发布 endpoint 的后台 listener 会校验当前会话 Hello 并返回匹配的 HelloAck。
    /// </summary>
    [Fact]
    public async Task FastChannelHandshakeAcknowledgesCurrentSession()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        var endpoint = fixture.ReadFastChannelEndpoint();
        using CancellationTokenSource cancellationSource = new(TimeSpan.FromSeconds(5));
        await using Stream channel = await fixture.ConnectFastChannelAsync(endpoint, cancellationSource.Token);
        JsonObject identity = new()
        {
            ["engineId"] = GodotFileBridgeHostFixture.ENGINE_ID,
            ["sessionId"] = host.SessionId,
            ["generation"] = host.Generation
        };

        await YokiFrameFastChannelFrameStream.WriteAsync(
            channel,
            new YokiFrameFastChannelFrame(
                YokiFrameFastChannelMessageKind.Hello,
                0,
                identity.ToJsonString()),
            cancellationSource.Token);
        var acknowledgement = await YokiFrameFastChannelFrameStream.ReadAsync(channel, cancellationSource.Token);

        Assert.Equal(YokiFrameFastChannelMessageKind.HelloAck, acknowledgement.MessageKind);
        var acknowledgementIdentity = System.Text.Json.Nodes.JsonNode.Parse(acknowledgement.PayloadJson)?.AsObject();
        Assert.Equal(GodotFileBridgeHostFixture.ENGINE_ID, acknowledgementIdentity?["engineId"]?.GetValue<string>());
        Assert.Equal(host.SessionId, acknowledgementIdentity?["sessionId"]?.GetValue<string>());
        Assert.Equal(host.Generation, acknowledgementIdentity?["generation"]?.GetValue<long>());
    }

    /// <summary>
    /// 验证后台 listener 只把 FastChannel Command 入队，主线程显式 drain 后才经现有 dispatcher 返回 System/ping。
    /// </summary>
    [Fact]
    public async Task FastChannelPingWaitsForExplicitMainThreadDrain()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        using CancellationTokenSource cancellationSource = new(TimeSpan.FromSeconds(5));
        await using Stream channel = await fixture.ConnectFastChannelAsync(
            fixture.ReadFastChannelEndpoint(),
            cancellationSource.Token);
        await GodotFileBridgeHostFixture.CompleteFastChannelHandshakeAsync(
            channel,
            host.SessionId,
            host.Generation,
            cancellationSource.Token);
        await YokiFrameFastChannelFrameStream.WriteAsync(
            channel,
            GodotFileBridgeHostFixture.CreateSystemFastChannelCommand("fast-ping-001", "ping"),
            cancellationSource.Token);
        var processed = await DrainUntilProcessedAsync(host, cancellationSource.Token);
        var response = await YokiFrameFastChannelFrameStream.ReadAsync(channel, cancellationSource.Token);
        Assert.Equal(1, processed);
        Assert.Equal(YokiFrameFastChannelMessageKind.Response, response.MessageKind);
        var envelope = JsonNode.Parse(response.PayloadJson)?.AsObject();
        Assert.Equal("Success", envelope?["status"]?.GetValue<string>());
        var result = JsonNode.Parse(envelope?["resultJson"]?.GetValue<string>() ?? "{}")?.AsObject();
        Assert.Equal("pong", result?["message"]?.GetValue<string>());
    }

    /// <summary>
    /// 验证已完成握手的 FastChannel 只加速两个无副作用 System 命令，其他 action 必须被拒绝且不得产生 FileBridge 终态文件。
    /// </summary>
    [Fact]
    public async Task FastChannelRejectsNonReadOnlyCommandWithoutExecuting()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        using CancellationTokenSource cancellationSource = new(TimeSpan.FromSeconds(5));
        await using Stream channel = await fixture.ConnectFastChannelAsync(
            fixture.ReadFastChannelEndpoint(),
            cancellationSource.Token);
        await GodotFileBridgeHostFixture.CompleteFastChannelHandshakeAsync(
            channel,
            host.SessionId,
            host.Generation,
            cancellationSource.Token);
        await YokiFrameFastChannelFrameStream.WriteAsync(
            channel,
            GodotFileBridgeHostFixture.CreateSystemFastChannelCommand("fast-reject-001", "unknown"),
            cancellationSource.Token);

        var processed = await DrainUntilProcessedAsync(host, cancellationSource.Token);
        var response = await YokiFrameFastChannelFrameStream.ReadAsync(channel, cancellationSource.Token);

        Assert.Equal(1, processed);
        Assert.Equal(YokiFrameFastChannelMessageKind.Error, response.MessageKind);
        var error = JsonNode.Parse(response.PayloadJson)?.AsObject();
        Assert.Equal("FastChannelCommandRejected", error?["code"]?.GetValue<string>());
        Assert.False(File.Exists(fixture.GetResponsePath("fast-reject-001")));
    }

    /// <summary>
    /// 验证非 System Kit 即使 action 名称为 ping，也不能绕过 FastChannel v1 的只读 System 白名单。
    /// </summary>
    [Fact]
    public async Task FastChannelRejectsNonSystemKitWithoutExecuting()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        using CancellationTokenSource cancellationSource = new(TimeSpan.FromSeconds(5));
        await using Stream channel = await fixture.ConnectFastChannelAsync(
            fixture.ReadFastChannelEndpoint(),
            cancellationSource.Token);
        await GodotFileBridgeHostFixture.CompleteFastChannelHandshakeAsync(
            channel,
            host.SessionId,
            host.Generation,
            cancellationSource.Token);
        await YokiFrameFastChannelFrameStream.WriteAsync(
            channel,
            GodotFileBridgeHostFixture.CreateFastChannelCommand("fast-reject-kit-001", "FsmKit", "ping"),
            cancellationSource.Token);

        var processed = await DrainUntilProcessedAsync(host, cancellationSource.Token);
        var response = await YokiFrameFastChannelFrameStream.ReadAsync(channel, cancellationSource.Token);

        Assert.Equal(1, processed);
        Assert.Equal(YokiFrameFastChannelMessageKind.Error, response.MessageKind);
        var error = JsonNode.Parse(response.PayloadJson)?.AsObject();
        Assert.Equal("FastChannelCommandRejected", error?["code"]?.GetValue<string>());
        Assert.False(File.Exists(fixture.GetResponsePath("fast-reject-kit-001")));
    }

    /// <summary>
    /// 验证 Host 停止会取消尚未主线程 drain 的 FastChannel 请求、释放连接，并清理自己创建的 Unix socket。
    /// </summary>
    [Fact]
    public async Task StopCancelsPendingFastChannelRequestAndReleasesLocalEndpoint()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        var endpoint = fixture.ReadFastChannelEndpoint();
        var socketPath = endpoint["transport"]?.GetValue<string>() == "unixDomainSocket"
            ? endpoint["endpoint"]?.GetValue<string>() ?? string.Empty
            : string.Empty;
        using CancellationTokenSource cancellationSource = new(TimeSpan.FromSeconds(5));
        await using Stream channel = await fixture.ConnectFastChannelAsync(endpoint, cancellationSource.Token);
        await GodotFileBridgeHostFixture.CompleteFastChannelHandshakeAsync(
            channel,
            host.SessionId,
            host.Generation,
            cancellationSource.Token);
        await YokiFrameFastChannelFrameStream.WriteAsync(
            channel,
            GodotFileBridgeHostFixture.CreateSystemFastChannelCommand("fast-stop-001", "ping"),
            cancellationSource.Token);
        var responseTask = YokiFrameFastChannelFrameStream.ReadAsync(channel, cancellationSource.Token);

        await Task.Delay(50, cancellationSource.Token);
        host.Stop();

        await Assert.ThrowsAnyAsync<Exception>(async () => await responseTask);
        Assert.False(File.Exists(fixture.RegistryPath));
        if (!string.IsNullOrEmpty(socketPath))
        {
            Assert.False(File.Exists(socketPath));
        }
    }

    /// <summary>
    /// 验证未发送 Hello 的静默客户端会在 listener 读取期限后释放唯一传输，后续客户端仍可握手且 endpoint 保持启用。
    /// </summary>
    [Fact]
    public async Task FastChannelSilentClientTimesOutBeforeNextClientHandshake()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        var endpoint = fixture.ReadFastChannelEndpoint();
        using CancellationTokenSource cancellationSource = new(TimeSpan.FromSeconds(6));
        await using Stream silentChannel = await fixture.ConnectFastChannelAsync(endpoint, cancellationSource.Token);
        await using Stream recoveredChannel = await fixture.ConnectFastChannelAsync(endpoint, cancellationSource.Token);

        await GodotFileBridgeHostFixture.CompleteFastChannelHandshakeAsync(
            recoveredChannel,
            host.SessionId,
            host.Generation,
            cancellationSource.Token);
        host.RefreshState();

        AssertFastChannelEnabled(fixture);
    }

    /// <summary>
    /// 验证单条损坏 frame 只结束该连接，不会把仍在 accept 的 listener 或 registry endpoint 误判为永久失败。
    /// </summary>
    [Fact]
    public async Task FastChannelMalformedFrameDoesNotDisableNextConnection()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        var endpoint = fixture.ReadFastChannelEndpoint();
        using CancellationTokenSource cancellationSource = new(TimeSpan.FromSeconds(5));
        await using (Stream malformedChannel = await fixture.ConnectFastChannelAsync(endpoint, cancellationSource.Token))
        {
            var invalidHeader = new byte[YokiFrameFastChannelContract.HEADER_SIZE];
            await malformedChannel.WriteAsync(invalidHeader, cancellationSource.Token);
            await malformedChannel.FlushAsync(cancellationSource.Token);
            var error = await YokiFrameFastChannelFrameStream.ReadAsync(malformedChannel, cancellationSource.Token);
            Assert.Equal(YokiFrameFastChannelMessageKind.Error, error.MessageKind);
        }

        host.RefreshState();
        AssertFastChannelEnabled(fixture);
        await using Stream recoveredChannel = await fixture.ConnectFastChannelAsync(endpoint, cancellationSource.Token);
        await GodotFileBridgeHostFixture.CompleteFastChannelHandshakeAsync(
            recoveredChannel,
            host.SessionId,
            host.Generation,
            cancellationSource.Token);
    }

    /// <summary>
    /// 验证启动会原子发布 engine registry、heartbeat 和四个 Kit snapshot，且不伪报 telemetry。
    /// </summary>
    [Fact]
    public void StartPublishesRegistryHeartbeatAndFourFallbackSnapshots()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");

        host.Start();

        Assert.True(host.IsRunning);
        var sessionId = host.SessionId;
        var generation = host.Generation;
        var sequence = host.Sequence;
        Assert.True(YokiFrameSafeIdContract.IsSafeId(sessionId));
        Assert.True(generation > 0);
        Assert.Equal(1, sequence);
        fixture.AssertPublishedState(sessionId, generation, sequence);
    }

    /// <summary>
    /// 验证显式刷新只递增 sequence，不改变当前 sessionId 和 generation。
    /// </summary>
    [Fact]
    public void RefreshStateAdvancesOnlySequence()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        var sessionId = host.SessionId;
        var generation = host.Generation;
        var firstSequence = host.Sequence;

        host.RefreshState();

        Assert.Equal(sessionId, host.SessionId);
        Assert.Equal(generation, host.Generation);
        Assert.Equal(firstSequence + 1, host.Sequence);
        fixture.AssertPublishedState(sessionId, generation, firstSequence + 1);
    }

    /// <summary>验证周期保活更新 heartbeat 和 Registry，但不会重写无变化 snapshot。</summary>
    [Fact]
    public void RefreshHeartbeatDoesNotRewriteStableRegistryOrSnapshots()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        var registryBefore = File.ReadAllText(fixture.RegistryPath);
        var snapshotPath = fixture.GetSnapshotPath("FsmKit");
        var snapshotBefore = File.ReadAllText(snapshotPath);
        var heartbeatBefore = File.ReadAllText(fixture.HeartbeatPath);

        host.RefreshHeartbeat();

        Assert.NotEqual(registryBefore, File.ReadAllText(fixture.RegistryPath));
        Assert.Equal(snapshotBefore, File.ReadAllText(snapshotPath));
        Assert.NotEqual(heartbeatBefore, File.ReadAllText(fixture.HeartbeatPath));
    }

    /// <summary>
    /// 验证 Windows Godot Runtime 在启动后为四个首批 Kit 发布与 FileBridge 状态一致的 committed telemetry 帧。
    /// </summary>
    [Fact]
    public void StartPublishesCommittedTelemetryForAllStateKitsOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");

        host.Start();

        foreach (var kit in new[] { "System", "EventKit", "FsmKit", "LogKit" })
        {
            fixture.AssertCommittedTelemetry(kit, host.SessionId, host.Generation, host.Sequence);
        }
    }

    /// <summary>
    /// 验证刷新会保留当前 generation 并把 telemetry 帧更新到与 heartbeat/snapshot 相同的新 sequence。
    /// </summary>
    [Fact]
    public void RefreshStateUpdatesCommittedTelemetrySequenceOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        var firstSequence = host.Sequence;

        host.RefreshState();

        Assert.Equal(firstSequence + 1, host.Sequence);
        foreach (var kit in new[] { "System", "EventKit", "FsmKit", "LogKit" })
        {
            fixture.AssertCommittedTelemetry(kit, host.SessionId, host.Generation, host.Sequence);
        }
    }

    /// <summary>
    /// 验证 Windows Host 停止时释放所有 telemetry map，避免新会话误读退出前的帧。
    /// </summary>
    [Fact]
    public void StopReleasesTelemetrySegmentsOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();

        host.Stop();

        foreach (var kit in new[] { "System", "EventKit", "FsmKit", "LogKit" })
        {
            GodotFileBridgeHostFixture.AssertTelemetryUnavailable(fixture.ProjectRoot, kit);
        }
    }

    /// <summary>
    /// 验证 System/ping 经 Runtime dispatcher 产生 terminal success response 并归档原命令。
    /// </summary>
    [Fact]
    public void PingCommandProducesTerminalResponseAndArchiveEvidence()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        var commandPath = fixture.WriteSystemCommand("ping-001", "ping");

        var processed = host.ProcessPendingCommands();

        Assert.Equal(1, processed);
        Assert.False(File.Exists(commandPath));
        Assert.True(File.Exists(Path.Combine(fixture.ArchiveRoot, "ping-001.json")));
        var response = GodotFileBridgeHostFixture.ReadObject(fixture.GetResponsePath("ping-001"));
        Assert.Equal("Success", response["status"]?.GetValue<string>());
        var result = System.Text.Json.Nodes.JsonNode.Parse(response["resultJson"]?.GetValue<string>() ?? "{}")?.AsObject();
        Assert.Equal("pong", result?["message"]?.GetValue<string>());
        Assert.Equal(GodotFileBridgeHostFixture.ENGINE_ID, result?["engineId"]?.GetValue<string>());
    }

    /// <summary>
    /// 验证无法解析的命令不会阻塞队列，而是保留 deadletter 诊断和原始请求证据。
    /// </summary>
    [Fact]
    public void MalformedCommandProducesDeadletterEvidence()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        var commandPath = fixture.WriteMalformedCommand("broken-001.json");

        var processed = host.ProcessPendingCommands();

        Assert.Equal(1, processed);
        Assert.False(File.Exists(commandPath));
        Assert.Single(Directory.EnumerateFiles(fixture.DeadletterRoot, "*-deadletter.json"));
        Assert.Single(Directory.EnumerateFiles(fixture.DeadletterRoot, "*-request.json"));
    }

    /// <summary>
    /// 验证跨会话遗留的 processing 命令会进入 Expired deadletter，而不会被 Host 自动重放。
    /// </summary>
    [Fact]
    public void ExpiredProcessingCommandIsDeadletteredWithoutReplay()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();

        var processingRoot = Path.Combine(fixture.CommandsRoot, "processing");
        var processingPath = Path.Combine(processingRoot, "expired-001.json");
        Directory.CreateDirectory(processingRoot);
        File.WriteAllText(processingPath, "{}");
        File.SetLastWriteTimeUtc(processingPath, DateTime.UtcNow.AddMinutes(-2));

        Assert.Equal(0, host.ProcessPendingCommands());
        Assert.False(File.Exists(processingPath));
        Assert.Empty(Directory.EnumerateFiles(fixture.ResultsRoot, "expired-001-response.json"));
        var infoPath = Assert.Single(Directory.EnumerateFiles(fixture.DeadletterRoot, "expired-001-deadletter.json"));
        var info = GodotFileBridgeHostFixture.ReadObject(infoPath);
        Assert.Equal("ProcessingExpired", info["errorCode"]?.GetValue<string>());
    }

    /// <summary>
    /// 验证退出删除活动 registry/heartbeat，重启创建新 session 和单调递增 generation。
    /// </summary>
    [Fact]
    public void StopReleasesActiveStateAndRestartCreatesNewSessionGeneration()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();
        var firstSession = host.SessionId;
        var firstGeneration = host.Generation;

        host.Stop();

        Assert.False(File.Exists(fixture.RegistryPath));
        Assert.False(File.Exists(fixture.HeartbeatPath));
        Assert.False(host.IsRunning);
        host.Start();
        Assert.NotEqual(firstSession, host.SessionId);
        Assert.True(host.Generation > firstGeneration);
        Assert.Equal(1, host.Sequence);
    }

    /// <summary>
    /// 验证活动 registry 清理因外部文件句柄失败时，Stop 仍释放 admission lease，避免后续 Host 被永久判定为已占用。
    /// </summary>
    [Fact]
    public void StopReleasesAdmissionLeaseWhenActiveStateCleanupFails()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost host = new(fixture.ProjectRoot, "4.7.0");
        host.Start();

        using (FileStream registryBlocker = new(
            fixture.RegistryPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read))
        {
            Assert.Throws<IOException>(() => host.Stop());
            Assert.False(host.IsRunning);
        }

        using GodotFileBridgeHost restartedHost = new(fixture.ProjectRoot, "4.7.0");
        restartedHost.Start();
        Assert.True(restartedHost.IsRunning);
    }

    /// <summary>
    /// 验证启动阶段 registry 写入失败时，Start 回滚也会释放已经取得的 admission lease。
    /// </summary>
    [Fact]
    public void StartFailureReleasesAdmissionLease()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        Directory.CreateDirectory(Path.GetDirectoryName(fixture.RegistryPath)!);
        File.WriteAllText(fixture.RegistryPath, "{\"sessionId\":\"old\",\"generation\":1}");
        using (FileStream registryBlocker = new(
            fixture.RegistryPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read))
        {
            using GodotFileBridgeHost failedHost = new(fixture.ProjectRoot, "4.7.0");
            Assert.Throws<IOException>(() => failedHost.Start());
            Assert.False(failedHost.IsRunning);
        }

        using GodotFileBridgeHost restartedHost = new(fixture.ProjectRoot, "4.7.0");
        restartedHost.Start();
        Assert.True(restartedHost.IsRunning);
    }

    /// <summary>
    /// 验证同一项目的第二个 Runtime Host 不得覆盖首个 Host 的 registry、heartbeat 或 listener。
    /// </summary>
    [Fact]
    public void SecondHostIsRejectedWithoutOverwritingFirstHostState()
    {
        using GodotFileBridgeHostFixture fixture = GodotFileBridgeHostFixture.Create();
        using GodotFileBridgeHost firstHost = new(fixture.ProjectRoot, "4.7.0");
        using GodotFileBridgeHost secondHost = new(fixture.ProjectRoot, "4.7.0");
        firstHost.Start();

        var firstSessionId = firstHost.SessionId;
        var firstGeneration = firstHost.Generation;
        var firstRegistry = GodotFileBridgeHostFixture.ReadObject(fixture.RegistryPath);

        var exception = Assert.Throws<YokiFrameHostAlreadyOwnedException>(() => secondHost.Start());

        Assert.Contains("godot-runtime", exception.Message, StringComparison.Ordinal);
        Assert.False(secondHost.IsRunning);
        var registry = GodotFileBridgeHostFixture.ReadObject(fixture.RegistryPath);
        Assert.Equal(firstSessionId, registry["sessionId"]?.GetValue<string>());
        Assert.Equal(firstGeneration, registry["generation"]?.GetValue<long>());
        Assert.Equal(firstRegistry["registeredAtUtc"]?.GetValue<string>(), registry["registeredAtUtc"]?.GetValue<string>());

        firstHost.Stop();
        secondHost.Start();
        Assert.NotEqual(firstSessionId, secondHost.SessionId);
        Assert.True(secondHost.Generation > firstGeneration);
    }

    /// <summary>
    /// 重试调用 Host 主线程 drain，直到后台 listener 已把请求送入有界队列。
    /// </summary>
    /// <param name="host">当前 Runtime Host。</param>
    /// <param name="cancellationToken">测试整体取消令牌。</param>
    /// <returns>首次处理到请求时的数量。</returns>
    private static async Task<int> DrainUntilProcessedAsync(
        GodotFileBridgeHost host,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var processed = host.ProcessPendingFastChannelRequests();
            if (processed > 0)
            {
                return processed;
            }

            await Task.Delay(10, cancellationToken);
        }

        return 0;
    }

    /// <summary>
    /// 验证当前 Host 重新写入 registry 后仍发布可连接的 FastChannel endpoint，而不是因单连接失败降级到 FileBridge-only。
    /// </summary>
    /// <param name="fixture">持有当前 Godot 项目与 registry 路径的隔离 fixture。</param>
    private static void AssertFastChannelEnabled(GodotFileBridgeHostFixture fixture)
    {
        var endpoint = fixture.ReadFastChannelEndpoint();
        Assert.True(endpoint["enabled"]?.GetValue<bool>());
        Assert.NotEqual("none", endpoint["transport"]?.GetValue<string>());
    }
}
