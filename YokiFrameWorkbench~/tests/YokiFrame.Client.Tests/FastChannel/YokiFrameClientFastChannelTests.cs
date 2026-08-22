using System.IO.Pipes;
using System.Reflection;
using System.Text.Json;
using YokiFrame.Client.FastChannel;
using YokiFrame.Client.FastChannel.IO;
using YokiFrame.Protocol.FastChannel;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.Tests.FastChannel;

/// <summary>
/// 覆盖统一 Client 对只读 System FastChannel 命令的连接复用与 registry 生命周期重连行为。
/// </summary>
public sealed class YokiFrameClientFastChannelTests
{
    private const string ENGINE_ID = "unity-editor";
    private const string SOURCE = "client-tests";
    private const int COMMAND_TIMEOUT_MS = 1000;
    private const int FAST_CHANNEL_OPERATION_TIMEOUT_MS = 750;
    private const string FAST_CHANNEL_METHOD_NAME = "SendFastChannelReadOnlySystemCommandAsync";

    /// <summary>
    /// 验证同一 registry endpoint 上连续发送 ping 和 bridge_status 时，Client 只完成一次 Hello 并复用该连接。
    /// </summary>
    [Fact]
    public async Task ReadOnlySystemCommandsReuseSingleHelloForUnchangedRegistryEndpoint()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var projectRoot = CreateProjectRoot();
        Task? serverTask = null;
        try
        {
            var endpoint = FastChannelEndpoint.CreateNamedPipe(ENGINE_ID, "session-a", 1, CreatePipeName());
            await WriteEngineRegistryAsync(projectRoot, endpoint);
            using YokiFrameClient client = new(projectRoot);
            var api = GetFastChannelReadOnlySystemCommandApi();
            var server = new NamedPipeReadOnlySystemHost(endpoint, new[] { "ping", "bridge_status" }, false, cancellationSource.Token);
            serverTask = server.ServeAsync();

            var pingResponse = await InvokeFastChannelReadOnlySystemCommandAsync(client, api, "ping", cancellationSource.Token);
            var statusResponse = await InvokeFastChannelReadOnlySystemCommandAsync(client, api, "bridge_status", cancellationSource.Token);

            await serverTask;
            Assert.Equal(1, server.HelloCount);
            AssertSuccessfulResponse(pingResponse, "ping");
            AssertSuccessfulResponse(statusResponse, "bridge_status");
        }
        finally
        {
            cancellationSource.Cancel();
            await DrainServerAfterCancellationAsync(serverTask);
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证 registry 改变 session、generation 和 Named Pipe endpoint 后，Client 释放旧连接并在新 endpoint 完成新的 Hello。
    /// </summary>
    [Fact]
    public async Task ReadOnlySystemCommandReconnectsAfterRegistrySessionGenerationAndEndpointChange()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var projectRoot = CreateProjectRoot();
        Task? firstServerTask = null;
        Task? secondServerTask = null;
        try
        {
            var firstEndpoint = FastChannelEndpoint.CreateNamedPipe(ENGINE_ID, "session-a", 1, CreatePipeName());
            await WriteEngineRegistryAsync(projectRoot, firstEndpoint);
            using YokiFrameClient client = new(projectRoot);
            var api = GetFastChannelReadOnlySystemCommandApi();
            var firstServer = new NamedPipeReadOnlySystemHost(firstEndpoint, new[] { "ping" }, true, cancellationSource.Token);
            firstServerTask = firstServer.ServeAsync();

            var pingResponse = await InvokeFastChannelReadOnlySystemCommandAsync(client, api, "ping", cancellationSource.Token);
            var secondEndpoint = FastChannelEndpoint.CreateNamedPipe(ENGINE_ID, "session-b", 2, CreatePipeName());
            await WriteEngineRegistryAsync(projectRoot, secondEndpoint);
            var secondServer = new NamedPipeReadOnlySystemHost(secondEndpoint, new[] { "bridge_status" }, false, cancellationSource.Token);
            secondServerTask = secondServer.ServeAsync();

            var statusResponse = await InvokeFastChannelReadOnlySystemCommandAsync(client, api, "bridge_status", cancellationSource.Token);

            await secondServerTask;
            await EnsureServerCompletesAsync(firstServerTask);
            Assert.Equal(1, firstServer.HelloCount);
            Assert.Equal(1, secondServer.HelloCount);
            AssertSuccessfulResponse(pingResponse, "ping");
            AssertSuccessfulResponse(statusResponse, "bridge_status");
        }
        finally
        {
            cancellationSource.Cancel();
            await DrainServerAfterCancellationAsync(firstServerTask);
            await DrainServerAfterCancellationAsync(secondServerTask);
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>验证并发 Dispose 会关闭已缓存连接，且生命周期结束后不能在同一 endpoint 上重新建连。</summary>
    [Fact]
    public async Task DisposeClosesCachedConnectionAndPreventsReconnection()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var projectRoot = CreateProjectRoot();
        Task? serverTask = null;
        var client = new YokiFrameClient(projectRoot);
        try
        {
            var endpoint = FastChannelEndpoint.CreateNamedPipe(ENGINE_ID, "session-dispose", 3, CreatePipeName());
            await WriteEngineRegistryAsync(projectRoot, endpoint);
            var server = new NamedPipeReadOnlySystemHost(endpoint, new[] { "ping" }, true, cancellationSource.Token);
            serverTask = server.ServeAsync();
            var response = await client.SendFastChannelReadOnlySystemCommandAsync(
                ENGINE_ID, "ping", SOURCE, COMMAND_TIMEOUT_MS, cancellationSource.Token);

            Parallel.For(0, 16, _ => client.Dispose());
            await serverTask.WaitAsync(TimeSpan.FromSeconds(2));

            AssertSuccessfulResponse(response, "ping");
            Assert.Equal(1, server.HelloCount);
            Assert.Throws<ObjectDisposedException>(() =>
            {
                _ = client.SendFastChannelReadOnlySystemCommandAsync(
                    ENGINE_ID, "ping", SOURCE, COMMAND_TIMEOUT_MS, CancellationToken.None);
            });
        }
        finally
        {
            client.Dispose();
            cancellationSource.Cancel();
            await DrainServerAfterCancellationAsync(serverTask);
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证 Host 返回损坏 Response JSON 时 Client 会把解析失败转换为可回退的协议异常，而不是将 JSON 实现细节泄漏给 Workbench。
    /// </summary>
    [Fact]
    public async Task ReadOnlySystemCommandConvertsMalformedResponseToProtocolError()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var projectRoot = CreateProjectRoot();
        Task? serverTask = null;
        try
        {
            var endpoint = FastChannelEndpoint.CreateNamedPipe(ENGINE_ID, "session-a", 1, CreatePipeName());
            await WriteEngineRegistryAsync(projectRoot, endpoint);
            using YokiFrameClient client = new(projectRoot);
            var api = GetFastChannelReadOnlySystemCommandApi();
            var server = new NamedPipeReadOnlySystemHost(
                endpoint,
                new[] { "ping" },
                false,
                cancellationSource.Token,
                true);
            serverTask = server.ServeAsync();

            var exception = await Assert.ThrowsAsync<YokiFrameProtocolException>(() =>
                InvokeFastChannelReadOnlySystemCommandAsync(client, api, "ping", cancellationSource.Token));

            Assert.Equal("FastChannelResponseInvalid", exception.Error.Code);
            await serverTask;
        }
        finally
        {
            cancellationSource.Cancel();
            await DrainServerAfterCancellationAsync(serverTask);
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证 FastChannel 的本地短期限不会把非法的 timeoutMs 写入协议；Host 收到的信封仍使用协议最小值。
    /// </summary>
    [Fact]
    public async Task ReadOnlySystemCommandKeepsWireTimeoutAtProtocolMinimumForShortOperation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var projectRoot = CreateProjectRoot();
        Task? serverTask = null;
        try
        {
            var endpoint = FastChannelEndpoint.CreateNamedPipe(ENGINE_ID, "session-short-timeout", 4, CreatePipeName());
            await WriteEngineRegistryAsync(projectRoot, endpoint);
            using YokiFrameClient client = new(projectRoot);
            var server = new NamedPipeReadOnlySystemHost(endpoint, new[] { "ping" }, false, cancellationSource.Token);
            serverTask = server.ServeAsync();

            var response = await client.SendFastChannelReadOnlySystemCommandAsync(
                ENGINE_ID,
                "ping",
                SOURCE,
                FAST_CHANNEL_OPERATION_TIMEOUT_MS,
                cancellationSource.Token);

            await serverTask;
            AssertSuccessfulResponse(response, "ping");
            Assert.Equal(CommandEnvelope.COMMAND_TIMEOUT_MIN_MS, server.LastEnvelopeTimeoutMs);
        }
        finally
        {
            cancellationSource.Cancel();
            await DrainServerAfterCancellationAsync(serverTask);
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证负数或零本地期限会立即拒绝，避免 .NET 将负数 CancelAfter 解释为无限等待。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ReadOnlySystemCommandRejectsNonPositiveOperationTimeout(int timeoutMs)
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            using YokiFrameClient client = new(projectRoot);
            var exception = await Assert.ThrowsAsync<YokiFrameProtocolException>(() =>
                client.SendFastChannelReadOnlySystemCommandAsync(
                    ENGINE_ID,
                    "ping",
                    SOURCE,
                    timeoutMs,
                    CancellationToken.None));

            Assert.Equal("InvalidTimeout", exception.Error.Code);
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 反射定位未来公开在统一 Client 边界上的 FastChannel 只读命令 API，并固定其参数和返回类型契约。
    /// </summary>
    /// <returns>与目标公开方法完全匹配的反射信息。</returns>
    private static MethodInfo GetFastChannelReadOnlySystemCommandApi()
    {
        var parameterTypes = new[]
        {
            typeof(string),
            typeof(string),
            typeof(string),
            typeof(int),
            typeof(CancellationToken)
        };
        var method = typeof(IFastChannelCommandTransport).GetMethod(FAST_CHANNEL_METHOD_NAME, parameterTypes);
        Assert.True(
            method != null,
            "IFastChannelCommandTransport 必须公开 Task<CommandResponse> SendFastChannelReadOnlySystemCommandAsync(string engineId, string action, string source, int timeoutMs, CancellationToken cancellationToken)。");
        Assert.Equal(typeof(Task<CommandResponse>), method!.ReturnType);
        return method;
    }

    /// <summary>
    /// 通过公开 Client API 调用只读 System FastChannel 命令，并保留真实异步异常和返回值供集成断言使用。
    /// </summary>
    /// <param name="client">统一 Client 实例。</param>
    /// <param name="api">已验证的目标公开 API。</param>
    /// <param name="action">允许走 FastChannel 的 System action。</param>
    /// <param name="cancellationToken">本次测试调用的取消令牌。</param>
    /// <returns>Host 返回并由 Client 解析后的命令响应。</returns>
    private static async Task<CommandResponse> InvokeFastChannelReadOnlySystemCommandAsync(
        IYokiFrameClient client,
        MethodInfo api,
        string action,
        CancellationToken cancellationToken)
    {
        var invocation = api.Invoke(client, new object?[]
        {
            ENGINE_ID,
            action,
            SOURCE,
            COMMAND_TIMEOUT_MS,
            cancellationToken
        });
        var commandTask = Assert.IsAssignableFrom<Task<CommandResponse>>(invocation);
        return await commandTask.ConfigureAwait(false);
    }

    /// <summary>
    /// 验证 Client 解析的 FastChannel terminal response 保留 Host 返回的核心字段。
    /// </summary>
    /// <param name="response">需要验证的命令响应。</param>
    /// <param name="action">服务端按该 action 构造的结果标识。</param>
    private static void AssertSuccessfulResponse(CommandResponse response, string action)
    {
        Assert.Equal(ENGINE_ID, response.EngineId);
        Assert.Equal("Success", response.Status);
        Assert.NotEmpty(response.RequestId);
        Assert.Equal("{\"action\":\"" + action + "\"}", response.ResultJson);
    }

    /// <summary>
    /// 向临时项目写入当前唯一 engine 的 registry，使 Client 必须从真实协议文件选择 FastChannel endpoint。
    /// </summary>
    /// <param name="projectRoot">临时项目根目录。</param>
    /// <param name="endpoint">当前 Host 对外发布的启用 endpoint。</param>
    /// <returns>registry 写入完成后的异步任务。</returns>
    private static async Task WriteEngineRegistryAsync(string projectRoot, FastChannelEndpoint endpoint)
    {
        var engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", endpoint.EngineId);
        Directory.CreateDirectory(engineRoot);
        EngineRegistryEntry registry = new()
        {
            ProtocolVersion = 2,
            EngineId = endpoint.EngineId,
            Engine = "Unity",
            SessionId = endpoint.SessionId,
            Generation = endpoint.Generation,
            FastChannels = new List<FastChannelEndpoint> { endpoint }
        };
        var registryPath = Path.Combine(engineRoot, YokiFrameFileBridgeLayout.ENGINE_REGISTRY_FILE_NAME);
        await File.WriteAllTextAsync(registryPath, registry.ToJson());
    }

    /// <summary>
    /// 等待预期的旧连接服务端结束；超时说明 Client 在 registry 生命周期变化后仍持有旧 FastChannel。
    /// </summary>
    /// <param name="serverTask">等待 Client 断开的旧 endpoint 服务端任务。</param>
    /// <returns>服务端结束后的异步任务。</returns>
    private static async Task EnsureServerCompletesAsync(Task serverTask)
    {
        var completedTask = await Task.WhenAny(serverTask, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.True(
            ReferenceEquals(serverTask, completedTask),
            "engine registry 的 sessionId、generation 或 endpoint 改变后，Client 必须释放旧 FastChannel 连接。");
        await serverTask;
    }

    /// <summary>
    /// 在测试提前失败时取消未完成的服务端任务，避免临时 Named Pipe 留在后台影响其它测试。
    /// </summary>
    /// <param name="serverTask">可能尚在等待连接或断开的服务端任务。</param>
    /// <returns>服务端已观察取消或正常结束后的异步任务。</returns>
    private static async Task DrainServerAfterCancellationAsync(Task? serverTask)
    {
        if (serverTask == null)
        {
            return;
        }

        try
        {
            await serverTask;
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    /// <summary>
    /// 创建唯一临时项目根目录，确保每个测试拥有独立 `.yokiframe` 协议目录。
    /// </summary>
    /// <returns>尚未创建的唯一临时项目根路径。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-client-fastchannel-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// 创建不与并行测试冲突且满足 SafeId 约束的 Named Pipe 名称。
    /// </summary>
    /// <returns>当前测试唯一的 Pipe 名称。</returns>
    private static string CreatePipeName()
    {
        return "YokiFrameClientFastChannelTests" + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 删除测试创建的临时项目目录；目录未创建时不执行删除。
    /// </summary>
    /// <param name="projectRoot">仅由当前测试生成的临时项目根目录。</param>
    private static void DeleteProjectRoot(string projectRoot)
    {
        if (Directory.Exists(projectRoot))
        {
            Directory.Delete(projectRoot, true);
        }
    }

    /// <summary>
    /// 为 Client 集成测试模拟单个 endpoint 的真实 Named Pipe Host，并记录 Hello 次数和命令顺序。
    /// </summary>
    private sealed class NamedPipeReadOnlySystemHost
    {
        private readonly FastChannelEndpoint mEndpoint;
        private readonly IReadOnlyList<string> mExpectedActions;
        private readonly bool mWaitForDisconnect;
        private readonly CancellationToken mCancellationToken;
        private readonly bool mSendMalformedResponse;
        private int mHelloCount;
        private int mLastEnvelopeTimeoutMs;

        /// <summary>
        /// 使用指定 endpoint、命令顺序和连接关闭期望创建测试 Host。
        /// </summary>
        /// <param name="endpoint">Host 发布给 registry 的 Named Pipe endpoint。</param>
        /// <param name="expectedActions">当前连接按顺序必须收到的 System action。</param>
        /// <param name="waitForDisconnect">命令完成后是否继续等待 Client 主动释放连接。</param>
        /// <param name="cancellationToken">测试整体超时或提前失败时的取消令牌。</param>
        /// <param name="sendMalformedResponse">是否让每条预期命令返回故意损坏的 Response payload。</param>
        public NamedPipeReadOnlySystemHost(
            FastChannelEndpoint endpoint,
            IReadOnlyList<string> expectedActions,
            bool waitForDisconnect,
            CancellationToken cancellationToken,
            bool sendMalformedResponse = false)
        {
            mEndpoint = endpoint;
            mExpectedActions = expectedActions;
            mWaitForDisconnect = waitForDisconnect;
            mCancellationToken = cancellationToken;
            mSendMalformedResponse = sendMalformedResponse;
        }

        /// <summary>
        /// 获取当前 Host 已成功校验的 Hello 数量，用于判断 Client 是否复用连接。
        /// </summary>
        public int HelloCount => Volatile.Read(ref mHelloCount);

        /// <summary>
        /// 获取 Host 最近收到的协议 timeoutMs，用于区分本地操作期限与线上信封期限。
        /// </summary>
        public int LastEnvelopeTimeoutMs => Volatile.Read(ref mLastEnvelopeTimeoutMs);

        /// <summary>
        /// 启动单连接服务端任务；测试调用侧负责等待完成并观察协议断言。
        /// </summary>
        /// <returns>Host 完成握手、命令处理和可选断开观察后的任务。</returns>
        public Task ServeAsync()
        {
            return ServeCoreAsync();
        }

        /// <summary>
        /// 建立 Named Pipe，校验唯一 Hello，按顺序响应只读 System 命令，并在需要时验证 Client 主动断开。
        /// </summary>
        /// <returns>Host 生命周期结束后的异步任务。</returns>
        private async Task ServeCoreAsync()
        {
            await using var server = new NamedPipeServerStream(
                mEndpoint.Endpoint,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await server.WaitForConnectionAsync(mCancellationToken);
            var hello = await FastChannelFrameStream.ReadAsync(server, mCancellationToken);
            FastChannelHandshake.EnsureHelloMatchesEndpoint(hello, mEndpoint);
            Interlocked.Increment(ref mHelloCount);
            await FastChannelFrameStream.WriteAsync(server, FastChannelHandshake.CreateHelloAck(mEndpoint), mCancellationToken);

            foreach (var expectedAction in mExpectedActions)
            {
                await ProcessExpectedCommandAsync(server, expectedAction);
            }

            if (mWaitForDisconnect)
            {
                await EnsureClientDisconnectedAsync(server);
            }
        }

        /// <summary>
        /// 读取并验证一条 FastChannel Command，再返回与 FileBridge 相同 schema 的成功 Response。
        /// </summary>
        /// <param name="server">已完成握手的 Named Pipe 服务端流。</param>
        /// <param name="expectedAction">当前请求必须携带的 System action。</param>
        /// <returns>响应帧已写入后的异步任务。</returns>
        private async Task ProcessExpectedCommandAsync(NamedPipeServerStream server, string expectedAction)
        {
            var request = await FastChannelFrameStream.ReadAsync(server, mCancellationToken);
            Assert.Equal(YokiFrameFastChannelMessageKind.Command, request.MessageKind);
            var envelope = CommandEnvelope.FromJson(request.PayloadJson);
            Assert.Equal(ENGINE_ID, envelope.EngineId);
            Assert.Equal(SOURCE, envelope.Source);
            Assert.Equal("System", envelope.Kit);
            Assert.Equal(expectedAction, envelope.Action);
            Assert.Equal("{}", envelope.PayloadJson);
            Assert.NotEmpty(envelope.RequestId);
            Interlocked.Exchange(ref mLastEnvelopeTimeoutMs, envelope.TimeoutMs);

            var responsePayload = mSendMalformedResponse
                ? "{"
                : JsonSerializer.Serialize(new CommandResponse
                {
                    ProtocolVersion = 2,
                    RequestId = envelope.RequestId,
                    EngineId = ENGINE_ID,
                    Status = "Success",
                    ResultJson = "{\"action\":\"" + expectedAction + "\"}",
                    CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
                });
            await FastChannelFrameStream.WriteAsync(
                server,
                new YokiFrameFastChannelFrame(YokiFrameFastChannelMessageKind.Response, 0, responsePayload),
                mCancellationToken);
        }

        /// <summary>
        /// 在 endpoint 生命周期切换测试中等待旧 Client 关闭 Pipe；若收到第二条命令则说明它错误复用了旧连接。
        /// </summary>
        /// <param name="server">已经完成预期命令处理的旧 Named Pipe 服务端流。</param>
        /// <returns>确认读取到 EOF 后的异步任务。</returns>
        private async Task EnsureClientDisconnectedAsync(NamedPipeServerStream server)
        {
            try
            {
                var unexpectedRequest = await FastChannelFrameStream.ReadAsync(server, mCancellationToken);
                throw new InvalidDataException(
                    "engine registry 已变更时 Client 仍在旧 FastChannel 上发送 " + unexpectedRequest.MessageKind + " frame。");
            }
            catch (EndOfStreamException)
            {
                return;
            }
        }
    }
}
