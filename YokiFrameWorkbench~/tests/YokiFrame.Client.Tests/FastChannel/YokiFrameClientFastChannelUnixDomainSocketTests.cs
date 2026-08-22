using System.Net.Sockets;
using System.Text.Json;
using YokiFrame.Client;
using YokiFrame.Client.FastChannel.IO;
using YokiFrame.Protocol.FastChannel;
using YokiFrame.Protocol.FileBridge;

namespace YokiFrame.Client.Tests.FastChannel;

/// <summary>
/// 覆盖 macOS/Linux 上统一 Client 经 Unix Domain Socket 选择、握手和复用 FastChannel 的行为。
/// </summary>
public sealed class YokiFrameClientFastChannelUnixDomainSocketTests
{
    private const string ENGINE_ID = "godot-runtime";
    private const string SOURCE = "unix-socket-tests";

    /// <summary>
    /// 验证 Unix 宿主在同一 registry endpoint 上连续发送两个只读 System 命令时只完成一次 Hello。
    /// </summary>
    [Fact]
    public async Task ReadOnlySystemCommandsReuseUnixDomainSocketConnectionWhenEndpointIsUnchanged()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        using var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var projectRoot = CreateProjectRoot();
        var socketPath = CreateSocketPath();
        Task? serverTask = null;
        try
        {
            var endpoint = FastChannelEndpoint.CreateUnixDomainSocket(ENGINE_ID, "session-a", 1, socketPath);
            await WriteEngineRegistryAsync(projectRoot, endpoint);
            IYokiFrameClient client = new YokiFrameClient(projectRoot);
            var server = new UnixSocketHost(endpoint, new[] { "ping", "bridge_status" }, cancellationSource.Token);
            serverTask = server.ServeAsync();

            var pingResponse = await client.SendFastChannelReadOnlySystemCommandAsync(
                ENGINE_ID,
                "ping",
                SOURCE,
                2000,
                cancellationSource.Token);
            var statusResponse = await client.SendFastChannelReadOnlySystemCommandAsync(
                ENGINE_ID,
                "bridge_status",
                SOURCE,
                2000,
                cancellationSource.Token);

            await serverTask;
            Assert.Equal("{\"action\":\"ping\"}", pingResponse.ResultJson);
            Assert.Equal("{\"action\":\"bridge_status\"}", statusResponse.ResultJson);
            Assert.Equal(1, server.HelloCount);
        }
        finally
        {
            cancellationSource.Cancel();
            await DrainServerAfterCancellationAsync(serverTask);
            DeleteProjectRoot(projectRoot);
            DeleteSocketPath(socketPath);
        }
    }

    /// <summary>
    /// 将 endpoint 写入临时 FileBridge registry，使统一 Client 必须通过 Unix transport 选择当前连接。
    /// </summary>
    /// <param name="projectRoot">临时项目根目录。</param>
    /// <param name="endpoint">当前启用的 Unix endpoint。</param>
    /// <returns>registry 写入完成后的异步任务。</returns>
    private static async Task WriteEngineRegistryAsync(string projectRoot, FastChannelEndpoint endpoint)
    {
        var engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", endpoint.EngineId);
        Directory.CreateDirectory(engineRoot);
        EngineRegistryEntry registry = new()
        {
            ProtocolVersion = 2,
            EngineId = endpoint.EngineId,
            Engine = "Godot",
            SessionId = endpoint.SessionId,
            Generation = endpoint.Generation,
            FastChannels = new List<FastChannelEndpoint> { endpoint }
        };
        await File.WriteAllTextAsync(
            Path.Combine(engineRoot, YokiFrameFileBridgeLayout.ENGINE_REGISTRY_FILE_NAME),
            registry.ToJson());
    }

    /// <summary>
    /// 创建独立临时项目根目录，避免测试访问真实引擎项目。
    /// </summary>
    /// <returns>尚未创建的唯一目录。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-fastchannel-unix-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// 使用短 `/tmp` 路径创建 Unix socket，避免 macOS 与 Linux 的 sun_path 长度限制。
    /// </summary>
    /// <returns>绝对且短的 socket 文件路径。</returns>
    private static string CreateSocketPath()
    {
        return Path.Combine("/tmp", "yf-" + Guid.NewGuid().ToString("N") + ".sock");
    }

    /// <summary>
    /// 等待失败路径中的服务端观察取消或客户端关闭，避免 socket 遗留到下一轮测试。
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
    /// 删除测试专用项目目录。
    /// </summary>
    /// <param name="projectRoot">仅由当前测试创建的目录。</param>
    private static void DeleteProjectRoot(string projectRoot)
    {
        if (Directory.Exists(projectRoot))
        {
            Directory.Delete(projectRoot, true);
        }
    }

    /// <summary>
    /// 删除 Unix listener 退出后保留的 socket 文件；文件不存在时保持幂等。
    /// </summary>
    /// <param name="socketPath">测试创建的 socket 文件路径。</param>
    private static void DeleteSocketPath(string socketPath)
    {
        if (File.Exists(socketPath))
        {
            File.Delete(socketPath);
        }
    }

    /// <summary>
    /// 模拟一个已发布 Unix FastChannel endpoint，在同一连接中处理多个只读 System command。
    /// </summary>
    private sealed class UnixSocketHost
    {
        private readonly FastChannelEndpoint mEndpoint;
        private readonly IReadOnlyList<string> mExpectedActions;
        private readonly CancellationToken mCancellationToken;
        private int mHelloCount;

        /// <summary>
        /// 使用 endpoint 和预期命令顺序创建 Unix 测试 Host。
        /// </summary>
        /// <param name="endpoint">本轮需要校验的 endpoint。</param>
        /// <param name="expectedActions">连接上必须依次接收的 action。</param>
        /// <param name="cancellationToken">测试整体取消令牌。</param>
        public UnixSocketHost(
            FastChannelEndpoint endpoint,
            IReadOnlyList<string> expectedActions,
            CancellationToken cancellationToken)
        {
            mEndpoint = endpoint;
            mExpectedActions = expectedActions;
            mCancellationToken = cancellationToken;
        }

        /// <summary>
        /// 获取已校验的 Hello 数量。
        /// </summary>
        public int HelloCount => Volatile.Read(ref mHelloCount);

        /// <summary>
        /// 启动唯一 Unix listener 并处理本连接上的全部预期命令。
        /// </summary>
        /// <returns>listener 生命周期结束后的异步任务。</returns>
        public Task ServeAsync()
        {
            return ServeCoreAsync();
        }

        /// <summary>
        /// 绑定短 socket 路径、完成握手，并按请求顺序返回关联 response。
        /// </summary>
        /// <returns>所有预期命令完成后的异步任务。</returns>
        private async Task ServeCoreAsync()
        {
            using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                listener.Bind(new UnixDomainSocketEndPoint(mEndpoint.Endpoint));
                listener.Listen(1);
                using var acceptedSocket = await listener.AcceptAsync(mCancellationToken);
                await using var stream = new NetworkStream(acceptedSocket, false);
                var hello = await FastChannelFrameStream.ReadAsync(stream, mCancellationToken);
                FastChannelHandshake.EnsureHelloMatchesEndpoint(hello, mEndpoint);
                Interlocked.Increment(ref mHelloCount);
                await FastChannelFrameStream.WriteAsync(stream, FastChannelHandshake.CreateHelloAck(mEndpoint), mCancellationToken);

                foreach (var action in mExpectedActions)
                {
                    await WriteResponseForExpectedActionAsync(stream, action);
                }
            }
            finally
            {
                DeleteSocketPath(mEndpoint.Endpoint);
            }
        }

        /// <summary>
        /// 验证下一条 Command 的 action，并发送匹配 requestId 的成功 Response。
        /// </summary>
        /// <param name="stream">已完成握手的 Unix socket 流。</param>
        /// <param name="expectedAction">当前必须接收的 action。</param>
        /// <returns>response 写入完成后的异步任务。</returns>
        private async Task WriteResponseForExpectedActionAsync(NetworkStream stream, string expectedAction)
        {
            var request = await FastChannelFrameStream.ReadAsync(stream, mCancellationToken);
            Assert.Equal(YokiFrameFastChannelMessageKind.Command, request.MessageKind);
            var envelope = CommandEnvelope.FromJson(request.PayloadJson);
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
                stream,
                new YokiFrameFastChannelFrame(
                    YokiFrameFastChannelMessageKind.Response,
                    0,
                    JsonSerializer.Serialize(response)),
                mCancellationToken);
        }
    }
}
