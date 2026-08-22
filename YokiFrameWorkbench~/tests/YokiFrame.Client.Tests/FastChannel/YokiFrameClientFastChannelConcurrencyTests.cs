using System.IO.Pipes;
using System.Text.Json;
using YokiFrame.Client;
using YokiFrame.Client.FastChannel.IO;
using YokiFrame.Protocol.FastChannel;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Client.Tests.FastChannel;

/// <summary>
/// 覆盖 FastChannel 建连失败与并发 registry 变更交错时的缓存隔离。
/// </summary>
public sealed class YokiFrameClientFastChannelConcurrencyTests
{
    private const string ENGINE_ID = "unity-editor";
    private const string SOURCE = "concurrency-tests";

    /// <summary>
    /// 验证旧 endpoint 握手被新 registry 身份抢占后，不会阻塞或释放健康连接。
    /// </summary>
    [Fact]
    public async Task FailedStaleHandshakeDoesNotDiscardNewEndpointConnection()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var projectRoot = CreateProjectRoot();
        Task? staleServerTask = null;
        Task? healthyServerTask = null;
        try
        {
            var staleEndpoint = FastChannelEndpoint.CreateNamedPipe(ENGINE_ID, "session-a", 1, CreatePipeName());
            await WriteEngineRegistryAsync(projectRoot, staleEndpoint);
            using YokiFrameClient client = new(projectRoot);
            var staleHost = new HandshakeBlockingPipeHost(staleEndpoint, cancellationSource.Token);
            staleServerTask = staleHost.ServeAsync();

            var staleRequest = client.SendFastChannelReadOnlySystemCommandAsync(
                ENGINE_ID,
                "ping",
                SOURCE,
                1000,
                cancellationSource.Token);
            await staleHost.WaitForHelloAsync(cancellationSource.Token);

            var healthyEndpoint = FastChannelEndpoint.CreateNamedPipe(ENGINE_ID, "session-b", 2, CreatePipeName());
            await WriteEngineRegistryAsync(projectRoot, healthyEndpoint);
            var healthyHost = new SequentialPipeHost(healthyEndpoint, new[] { "ping", "bridge_status" }, cancellationSource.Token);
            healthyServerTask = healthyHost.ServeAsync();

            var firstHealthyResponse = await client.SendFastChannelReadOnlySystemCommandAsync(
                ENGINE_ID,
                "ping",
                SOURCE,
                2000,
                cancellationSource.Token);
            Assert.Equal("FastChannelEndpointSuperseded", (await Assert.ThrowsAsync<YokiFrameProtocolException>(() => staleRequest)).Error.Code);
            var secondHealthyResponse = await client.SendFastChannelReadOnlySystemCommandAsync(
                ENGINE_ID,
                "bridge_status",
                SOURCE,
                2000,
                cancellationSource.Token);

            await healthyServerTask;
            await staleServerTask;
            Assert.Equal("{\"action\":\"ping\"}", firstHealthyResponse.ResultJson);
            Assert.Equal("{\"action\":\"bridge_status\"}", secondHealthyResponse.ResultJson);
            Assert.Equal(1, healthyHost.HelloCount);
        }
        finally
        {
            cancellationSource.Cancel();
            await DrainServerAfterCancellationAsync(staleServerTask);
            await DrainServerAfterCancellationAsync(healthyServerTask);
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证 sessionId 或 generation 单独变化时，即使 Pipe 名称未变，Client 也会关闭旧连接并完成新一轮 Hello。
    /// </summary>
    /// <param name="changeSession">是否只变更 sessionId。</param>
    /// <param name="changeGeneration">是否只变更 generation。</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task SessionOrGenerationChangeReconnectsWhenPipeNameIsUnchanged(
        bool changeSession,
        bool changeGeneration)
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
            var pipeName = CreatePipeName();
            var firstEndpoint = FastChannelEndpoint.CreateNamedPipe(ENGINE_ID, "session-a", 1, pipeName);
            var secondEndpoint = FastChannelEndpoint.CreateNamedPipe(
                ENGINE_ID,
                changeSession ? "session-b" : "session-a",
                changeGeneration ? 2 : 1,
                pipeName);
            await WriteEngineRegistryAsync(projectRoot, firstEndpoint);
            using YokiFrameClient client = new(projectRoot);
            var host = new RotatingPipeHost(
                firstEndpoint,
                secondEndpoint,
                cancellationSource.Token);
            serverTask = host.ServeAsync();

            var firstResponse = await client.SendFastChannelReadOnlySystemCommandAsync(
                ENGINE_ID,
                "ping",
                SOURCE,
                2000,
                cancellationSource.Token);
            await WriteEngineRegistryAsync(projectRoot, secondEndpoint);
            var secondResponse = await client.SendFastChannelReadOnlySystemCommandAsync(
                ENGINE_ID,
                "bridge_status",
                SOURCE,
                2000,
                cancellationSource.Token);

            await serverTask;
            Assert.Equal("{\"action\":\"ping\"}", firstResponse.ResultJson);
            Assert.Equal("{\"action\":\"bridge_status\"}", secondResponse.ResultJson);
            Assert.Equal(2, host.HelloCount);
        }
        finally
        {
            cancellationSource.Cancel();
            await DrainServerAfterCancellationAsync(serverTask);
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 将最新 endpoint 写入临时 engine registry，使 Client 每次命令都必须重新读取当前生命周期身份。
    /// </summary>
    /// <param name="projectRoot">临时项目根目录。</param>
    /// <param name="endpoint">当前需要发布的 endpoint。</param>
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
        await File.WriteAllTextAsync(
            Path.Combine(engineRoot, YokiFrameFileBridgeLayout.ENGINE_REGISTRY_FILE_NAME),
            registry.ToJson());
    }

    /// <summary>
    /// 创建当前测试独占的项目根目录，避免改动真实 `.yokiframe` 现场。
    /// </summary>
    /// <returns>尚未创建的唯一临时目录。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-fastchannel-concurrency-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// 创建满足 SafeId 约束且不会与并发测试冲突的 Named Pipe 名称。
    /// </summary>
    /// <returns>唯一 Pipe 名称。</returns>
    private static string CreatePipeName()
    {
        return "YokiFrameFastChannelConcurrency" + Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// 在失败路径中等待服务端观察取消或连接释放，避免后台任务影响后续测试。
    /// </summary>
    /// <param name="serverTask">可能尚未完成的服务端任务。</param>
    /// <returns>服务端结束后的异步任务。</returns>
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
        catch (EndOfStreamException)
        {
            return;
        }
    }

    /// <summary>
    /// 删除当前测试唯一创建的项目目录。
    /// </summary>
    /// <param name="projectRoot">仅属于当前测试的目录。</param>
    private static void DeleteProjectRoot(string projectRoot)
    {
        if (Directory.Exists(projectRoot))
        {
            Directory.Delete(projectRoot, true);
        }
    }

    /// <summary>
    /// 建立连接并在读取 Hello 后故意不写 HelloAck，用于模拟旧 endpoint 在生命周期切换期间的握手卡死。
    /// </summary>
    private sealed class HandshakeBlockingPipeHost
    {
        private readonly FastChannelEndpoint mEndpoint;
        private readonly CancellationToken mCancellationToken;
        private readonly TaskCompletionSource<bool> mHelloReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 使用指定 endpoint 创建握手阻塞服务端。
        /// </summary>
        /// <param name="endpoint">需要校验的旧 endpoint。</param>
        /// <param name="cancellationToken">测试整体取消令牌。</param>
        public HandshakeBlockingPipeHost(FastChannelEndpoint endpoint, CancellationToken cancellationToken)
        {
            mEndpoint = endpoint;
            mCancellationToken = cancellationToken;
        }

        /// <summary>
        /// 启动等待 Hello 后保持静默的服务端任务。
        /// </summary>
        /// <returns>客户端关闭连接或测试取消后的任务。</returns>
        public Task ServeAsync()
        {
            return ServeCoreAsync();
        }

        /// <summary>
        /// 等待 Client 已进入旧 endpoint 的握手阶段，保证后续 registry 替换与建连失败发生交错。
        /// </summary>
        /// <param name="cancellationToken">等待取消令牌。</param>
        /// <returns>Hello 被读取后的异步任务。</returns>
        public async Task WaitForHelloAsync(CancellationToken cancellationToken)
        {
            await mHelloReceived.Task.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// 接收并校验 Hello，随后只等待 Client 因 endpoint 被抢占而释放 Pipe，不返回 HelloAck。
        /// </summary>
        /// <returns>连接关闭后的异步任务。</returns>
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
            mHelloReceived.TrySetResult(true);
            try
            {
                await FastChannelFrameStream.ReadAsync(server, mCancellationToken);
            }
            catch (EndOfStreamException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// 在同一 Pipe 连接上顺序处理多个只读 System 命令，并记录 Hello 次数以验证 Client 未被旧失败路径强制重连。
    /// </summary>
    private sealed class SequentialPipeHost
    {
        private readonly FastChannelEndpoint mEndpoint;
        private readonly IReadOnlyList<string> mExpectedActions;
        private readonly CancellationToken mCancellationToken;
        private int mHelloCount;

        /// <summary>
        /// 使用当前健康 endpoint 和预期命令顺序创建服务端。
        /// </summary>
        /// <param name="endpoint">健康的当前 endpoint。</param>
        /// <param name="expectedActions">连接上需要依次处理的 action。</param>
        /// <param name="cancellationToken">测试整体取消令牌。</param>
        public SequentialPipeHost(
            FastChannelEndpoint endpoint,
            IReadOnlyList<string> expectedActions,
            CancellationToken cancellationToken)
        {
            mEndpoint = endpoint;
            mExpectedActions = expectedActions;
            mCancellationToken = cancellationToken;
        }

        /// <summary>
        /// 获取成功完成握手的连接数量。
        /// </summary>
        public int HelloCount => Volatile.Read(ref mHelloCount);

        /// <summary>
        /// 启动并处理唯一健康连接。
        /// </summary>
        /// <returns>所有预期命令完成后的异步任务。</returns>
        public Task ServeAsync()
        {
            return ServeCoreAsync();
        }

        /// <summary>
        /// 完成 Hello/HelloAck，并为每个预期 action 发送关联的成功 Response。
        /// </summary>
        /// <returns>服务端完成后的异步任务。</returns>
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

            foreach (var action in mExpectedActions)
            {
                await WriteResponseForExpectedActionAsync(server, action);
            }
        }

        /// <summary>
        /// 验证下一条 Command 的 action，并返回相同 requestId 的成功响应。
        /// </summary>
        /// <param name="server">已完成握手的 Pipe 流。</param>
        /// <param name="expectedAction">当前需要接收的 action。</param>
        /// <returns>响应写入完成后的异步任务。</returns>
        private async Task WriteResponseForExpectedActionAsync(NamedPipeServerStream server, string expectedAction)
        {
            var frame = await FastChannelFrameStream.ReadAsync(server, mCancellationToken);
            Assert.Equal(YokiFrameFastChannelMessageKind.Command, frame.MessageKind);
            var envelope = CommandEnvelope.FromJson(frame.PayloadJson);
            Assert.Equal(expectedAction, envelope.Action);
            var response = new CommandResponse
            {
                ProtocolVersion = 2,
                RequestId = envelope.RequestId,
                EngineId = ENGINE_ID,
                Status = "Success",
                ResultJson = "{\"action\":\"" + expectedAction + "\"}",
                CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            await FastChannelFrameStream.WriteAsync(
                server,
                new YokiFrameFastChannelFrame(
                    YokiFrameFastChannelMessageKind.Response,
                    0,
                    JsonSerializer.Serialize(response)),
                mCancellationToken);
        }
    }

    /// <summary>
    /// 在同一 Pipe 名称上按两个不同 endpoint 身份依次创建 listener，用于验证 Client 因 registry 生命周期变化主动关闭旧连接。
    /// </summary>
    private sealed class RotatingPipeHost
    {
        private readonly FastChannelEndpoint mFirstEndpoint;
        private readonly FastChannelEndpoint mSecondEndpoint;
        private readonly CancellationToken mCancellationToken;
        private int mHelloCount;

        /// <summary>
        /// 使用先后两代 endpoint 创建旋转 listener。
        /// </summary>
        /// <param name="firstEndpoint">初始 registry 对应的 endpoint。</param>
        /// <param name="secondEndpoint">只修改 session 或 generation 的后续 endpoint。</param>
        /// <param name="cancellationToken">测试整体取消令牌。</param>
        public RotatingPipeHost(
            FastChannelEndpoint firstEndpoint,
            FastChannelEndpoint secondEndpoint,
            CancellationToken cancellationToken)
        {
            mFirstEndpoint = firstEndpoint;
            mSecondEndpoint = secondEndpoint;
            mCancellationToken = cancellationToken;
        }

        /// <summary>
        /// 获取两轮 listener 已接收的 Hello 数量。
        /// </summary>
        public int HelloCount => Volatile.Read(ref mHelloCount);

        /// <summary>
        /// 先服务初始连接并等待 Client 主动断开，再以相同 Pipe 名称服务下一代 endpoint。
        /// </summary>
        /// <returns>两代 endpoint 均完成预期命令后的异步任务。</returns>
        public async Task ServeAsync()
        {
            await ServeConnectionAsync(mFirstEndpoint, "ping", true);
            await ServeConnectionAsync(mSecondEndpoint, "bridge_status", false);
        }

        /// <summary>
        /// 创建一个单实例 Pipe listener，完成握手、处理一个命令，并在需要时确认 Client 已关闭旧连接。
        /// </summary>
        /// <param name="endpoint">本轮需要校验的 endpoint 身份。</param>
        /// <param name="expectedAction">本轮唯一允许的 action。</param>
        /// <param name="waitForDisconnect">完成响应后是否继续等待旧连接 EOF。</param>
        /// <returns>当前 listener 生命周期结束后的异步任务。</returns>
        private async Task ServeConnectionAsync(
            FastChannelEndpoint endpoint,
            string expectedAction,
            bool waitForDisconnect)
        {
            await using var server = new NamedPipeServerStream(
                endpoint.Endpoint,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await server.WaitForConnectionAsync(mCancellationToken);
            var hello = await FastChannelFrameStream.ReadAsync(server, mCancellationToken);
            FastChannelHandshake.EnsureHelloMatchesEndpoint(hello, endpoint);
            Interlocked.Increment(ref mHelloCount);
            await FastChannelFrameStream.WriteAsync(server, FastChannelHandshake.CreateHelloAck(endpoint), mCancellationToken);
            await WriteResponseForExpectedActionAsync(server, expectedAction);
            if (!waitForDisconnect)
            {
                return;
            }

            try
            {
                await FastChannelFrameStream.ReadAsync(server, mCancellationToken);
                throw new InvalidDataException("registry 身份已变化后 Client 仍在旧 FastChannel 上发送命令。");
            }
            catch (EndOfStreamException)
            {
                return;
            }
        }

        /// <summary>
        /// 读取当前 expected action，并返回关联 requestId 的成功 response。
        /// </summary>
        /// <param name="server">已完成握手的 Pipe 流。</param>
        /// <param name="expectedAction">当前必须收到的 action。</param>
        /// <returns>写回响应后的异步任务。</returns>
        private async Task WriteResponseForExpectedActionAsync(NamedPipeServerStream server, string expectedAction)
        {
            var frame = await FastChannelFrameStream.ReadAsync(server, mCancellationToken);
            Assert.Equal(YokiFrameFastChannelMessageKind.Command, frame.MessageKind);
            var envelope = CommandEnvelope.FromJson(frame.PayloadJson);
            Assert.Equal(expectedAction, envelope.Action);
            var response = new CommandResponse
            {
                ProtocolVersion = 2,
                RequestId = envelope.RequestId,
                EngineId = ENGINE_ID,
                Status = "Success",
                ResultJson = "{\"action\":\"" + expectedAction + "\"}",
                CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O")
            };
            await FastChannelFrameStream.WriteAsync(
                server,
                new YokiFrameFastChannelFrame(
                    YokiFrameFastChannelMessageKind.Response,
                    0,
                    JsonSerializer.Serialize(response)),
                mCancellationToken);
        }
    }
}
