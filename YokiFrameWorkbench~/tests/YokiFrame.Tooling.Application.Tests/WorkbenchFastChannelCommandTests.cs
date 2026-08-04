using System.Reflection;
using YokiFrame.Client;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 Workbench 对只读 System 命令的 FastChannel 优先选择。
/// </summary>
public sealed class WorkbenchFastChannelCommandTests
{
    /// <summary>
    /// 验证 System/ping 在 FastChannel 可用时优先走快速通道，而不写入可靠 FileBridge。
    /// </summary>
    [Fact]
    public async Task SendSystemPingPrefersFastChannel()
    {
        var recorder = RecordingYokiFrameClientProxy.Create();
        var service = new WorkbenchDashboardService(recorder.Client);

        var state = await service.SendSystemCommandAsync(
            "unity-editor",
            "ping",
            CancellationToken.None);

        Assert.True(state.Ok);
        Assert.Equal(1, recorder.FastChannelCallCount);
        Assert.Equal(0, recorder.FileBridgeCallCount);
        Assert.Equal("{\"transport\":\"fast-channel\"}", state.ResultJson);
    }

    /// <summary>
    /// 验证 System/bridge_status 与 ping 同属首版允许的只读诊断操作，并且在快速通道可用时不落盘 FileBridge command。
    /// </summary>
    [Fact]
    public async Task SendSystemBridgeStatusPrefersFastChannel()
    {
        var recorder = RecordingYokiFrameClientProxy.Create();
        var service = new WorkbenchDashboardService(recorder.Client);

        var state = await service.SendSystemCommandAsync(
            "unity-editor",
            "bridge_status",
            CancellationToken.None);

        Assert.True(state.Ok);
        Assert.Equal(1, recorder.FastChannelCallCount);
        Assert.Equal(0, recorder.FileBridgeCallCount);
        Assert.Equal("{\"transport\":\"fast-channel\"}", state.ResultJson);
    }

    /// <summary>
    /// 验证只读 System/list_commands 使用 Host 声明的 FastChannel，不再固定落盘。
    /// </summary>
    [Fact]
    public async Task SendSystemListCommandsPrefersFastChannel()
    {
        var recorder = RecordingYokiFrameClientProxy.Create();
        var service = new WorkbenchDashboardService(recorder.Client);

        var state = await service.SendSystemCommandAsync(
            "unity-editor",
            "list_commands",
            CancellationToken.None);

        Assert.True(state.Ok);
        Assert.Equal(1, recorder.FastChannelCallCount);
        Assert.Equal(0, recorder.FileBridgeCallCount);
        Assert.Equal("System", recorder.LastFastChannelKit);
        Assert.Equal("list_commands", recorder.LastFastChannelAction);
        Assert.Equal(750, recorder.LastFastChannelTimeoutMs);
    }

    /// <summary>
    /// 验证 FsmKit 带实例 payload 的只读查询优先走 FastChannel，并完整保留 payload。
    /// </summary>
    [Fact]
    public async Task SendFsmKitStateQueryWithPayloadPrefersFastChannel()
    {
        var recorder = RecordingYokiFrameClientProxy.Create();
        CommandExecutionService service = new(recorder.Client);
        const string payloadJson = "{\"instanceId\":\"fsm-42\"}";

        var result = await service.ExecuteAsync(
            "unity-editor",
            "FsmKit",
            "get_state",
            payloadJson,
            "workbench",
            2500,
            CancellationToken.None);

        Assert.Equal("fast-channel", result.Transport);
        Assert.Equal(1, recorder.FastChannelCallCount);
        Assert.Equal(0, recorder.FileBridgeCallCount);
        Assert.Equal(payloadJson, recorder.LastFastChannelPayloadJson);
    }

    /// <summary>
    /// 验证 Maintenance 命令不尝试 FastChannel，继续保留 FileBridge command/response evidence。
    /// </summary>
    [Fact]
    public async Task SendMaintenanceCommandUsesFileBridgeWithoutFastChannel()
    {
        var recorder = RecordingYokiFrameClientProxy.Create();
        var service = new WorkbenchDashboardService(recorder.Client);

        var state = await service.SendSystemCommandAsync(
            "unity-editor",
            "refresh_snapshots",
            CancellationToken.None);

        Assert.True(state.Ok);
        Assert.Equal(0, recorder.FastChannelCallCount);
        Assert.Equal(1, recorder.FileBridgeCallCount);
        Assert.Equal("refresh_snapshots", recorder.LastFileBridgeAction);
    }

    /// <summary>
    /// 验证 Runtime terminal response 为 Error 时，Workbench 不会把已到达响应误报为成功。
    /// </summary>
    [Fact]
    public async Task SendCommandProjectsRuntimeErrorAsNotOk()
    {
        var recorder = RecordingYokiFrameClientProxy.Create();
        recorder.FileBridgeResponseStatus = "Error";
        recorder.FileBridgeErrorMessage = "command rejected by runtime";
        var service = new WorkbenchDashboardService(recorder.Client);

        var state = await service.SendSystemCommandAsync(
            "unity-editor",
            "refresh_snapshots",
            CancellationToken.None);

        Assert.False(state.Ok);
        Assert.Equal("Error", state.Status);
        Assert.Equal("command rejected by runtime", state.ErrorMessage);
        Assert.Equal(1, recorder.FileBridgeCallCount);
    }

    /// <summary>
    /// 验证没有 terminal response 的超时会投影为 Unknown，避免调用方自动重放变更命令。
    /// </summary>
    [Fact]
    public async Task SendCommandTimeoutProjectsUnknownOutcome()
    {
        var recorder = RecordingYokiFrameClientProxy.Create();
        recorder.FileBridgeFailure = new YokiFrameProtocolException(new YokiFrameError(
            "CommandTimeout",
            "command wait expired",
            "query the request evidence before retrying"));
        var service = new WorkbenchDashboardService(recorder.Client);

        var state = await service.SendSystemCommandAsync(
            "unity-editor",
            "refresh_snapshots",
            CancellationToken.None);

        Assert.False(state.Ok);
        Assert.Equal(CommandOutcomeState.Unknown, state.Outcome);
        Assert.Equal("Unknown", state.Status);
    }

    /// <summary>
    /// 验证其它 Kit 即使 action 名称与首版白名单相同，也始终使用可靠 FileBridge。
    /// </summary>
    [Fact]
    public async Task SendOtherKitPingUsesFileBridgeWithoutFastChannel()
    {
        var recorder = RecordingYokiFrameClientProxy.Create();
        var service = new WorkbenchDashboardService(recorder.Client);

        var state = await service.SendCommandAsync(
            "unity-editor",
            "FsmKit",
            "ping",
            CancellationToken.None);

        Assert.True(state.Ok);
        Assert.Equal(0, recorder.FastChannelCallCount);
        Assert.Equal(1, recorder.FileBridgeCallCount);
        Assert.Equal("FsmKit", recorder.LastFileBridgeKit);
        Assert.Equal("ping", recorder.LastFileBridgeAction);
    }

    /// <summary>
    /// 验证 FastChannel 的可预期协议失败不会中断用户操作，而是只回退一次可靠 FileBridge。
    /// </summary>
    [Fact]
    public async Task SendSystemPingFallsBackToFileBridgeAfterFastChannelFailure()
    {
        var recorder = RecordingYokiFrameClientProxy.Create();
        recorder.FastChannelFailure = new YokiFrameProtocolException(new YokiFrameError(
            "FastChannelConnectFailed",
            "test FastChannel failure",
            "use FileBridge fallback"));
        var service = new WorkbenchDashboardService(recorder.Client);

        var state = await service.SendSystemCommandAsync(
            "unity-editor",
            "ping",
            CancellationToken.None);

        Assert.True(state.Ok);
        Assert.Equal(1, recorder.FastChannelCallCount);
        Assert.Equal(1, recorder.FileBridgeCallCount);
        Assert.Equal("System", recorder.LastFileBridgeKit);
        Assert.Equal("ping", recorder.LastFileBridgeAction);
        Assert.Equal("{\"transport\":\"file-bridge\"}", state.ResultJson);
    }

    /// <summary>
    /// 验证响应关联或协议契约错误不会被降级为 FileBridge 成功，避免隐藏宿主与工具版本漂移。
    /// </summary>
    [Fact]
    public async Task SendSystemPingDoesNotFallbackAfterResponseProtocolFailure()
    {
        var recorder = RecordingYokiFrameClientProxy.Create();
        recorder.FastChannelFailure = new YokiFrameProtocolException(new YokiFrameError(
            "FastChannelResponseMismatch",
            "test response association failure",
            "refresh the host protocol"));
        var service = new WorkbenchDashboardService(recorder.Client);

        var state = await service.SendSystemCommandAsync(
            "unity-editor",
            "ping",
            CancellationToken.None);

        Assert.False(state.Ok);
        Assert.Equal("Error", state.Status);
        Assert.Equal(1, recorder.FastChannelCallCount);
        Assert.Equal(0, recorder.FileBridgeCallCount);
        Assert.Contains("test response association failure", state.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// 通过 DispatchProxy 记录 Workbench 对当前 Client 边界的真实调用，
    /// 使未来接口成员尚未声明时仍可用方法名表达 FastChannel 契约。
    /// </summary>
    public class RecordingYokiFrameClientProxy : DispatchProxy
    {
        private const string FAST_CHANNEL_CAPABILITY_METHOD_NAME = "CanSendFastChannelReadOnlyCommand";
        private const string FAST_CHANNEL_METHOD_NAME = "SendFastChannelReadOnlyCommandAsync";
        private const string FAST_CHANNEL_RESULT_JSON = "{\"transport\":\"fast-channel\"}";
        private const string FILE_BRIDGE_RESULT_JSON = "{\"transport\":\"file-bridge\"}";
        private readonly YokiFramePaths mPaths = new(
            Path.Combine(Path.GetTempPath(), "yokiframe-fastchannel-command-tests", Guid.NewGuid().ToString("N")));
        private readonly EngineRegistryEntry mRegistry = new()
        {
            ProtocolVersion = 2,
            EngineId = "unity-editor",
            Engine = "Unity",
            SessionId = "test-session",
            Generation = 1L,
            Mode = "EditMode"
        };
        private IYokiFrameClient mClient = null!;

        /// <summary>
        /// 获取被 Workbench 注入的动态 Client 实例。
        /// </summary>
        public IYokiFrameClient Client => mClient;

        /// <summary>
        /// 获取未来 FastChannel 只读 System 调用次数。
        /// </summary>
        public int FastChannelCallCount { get; private set; }

        /// <summary>
        /// 获取最近一次 FastChannel 调用使用的 Kit。
        /// </summary>
        public string LastFastChannelKit { get; private set; } = string.Empty;

        /// <summary>
        /// 获取最近一次 FastChannel 调用使用的 action。
        /// </summary>
        public string LastFastChannelAction { get; private set; } = string.Empty;

        /// <summary>
        /// 获取最近一次 FastChannel 调用使用的 payload。
        /// </summary>
        public string LastFastChannelPayloadJson { get; private set; } = string.Empty;

        /// <summary>
        /// 获取 Application 分配给本次 FastChannel 操作的本地期限；该值不等于线上信封期限。
        /// </summary>
        public int LastFastChannelTimeoutMs { get; private set; }

        /// <summary>获取最近一次 FastChannel 能力查询的 Kit/action 键。</summary>
        /// <summary>
        /// 获取可靠 FileBridge 命令调用次数。
        /// </summary>
        public int FileBridgeCallCount { get; private set; }

        /// <summary>获取首次 FileBridge 调用的 action，供多命令工作流验证变更命令。</summary>
        public string FirstFileBridgeAction { get; private set; } = string.Empty;

        /// <summary>获取首次 FileBridge 调用的 payload，供多命令工作流验证精确字段。</summary>
        public string FirstFileBridgePayloadJson { get; private set; } = string.Empty;

        /// <summary>
        /// 获取最近一次 FileBridge 调用使用的 Kit，供路由边界测试验证参数未被改写。
        /// </summary>
        public string LastFileBridgeKit { get; private set; } = string.Empty;

        /// <summary>
        /// 获取最近一次 FileBridge 调用使用的 action，供路由边界测试验证参数未被改写。
        /// </summary>
        public string LastFileBridgeAction { get; private set; } = string.Empty;

        /// <summary>
        /// 获取最近一次 FileBridge 调用使用的 payload，验证 CLI 参数不会在共享用例中丢失。
        /// </summary>
        public string LastFileBridgePayloadJson { get; private set; } = string.Empty;

        /// <summary>
        /// 获取最近一次 FileBridge 调用使用的审计来源。
        /// </summary>
        public string LastFileBridgeSource { get; private set; } = string.Empty;

        /// <summary>
        /// 获取最近一次 FileBridge 调用使用的超时毫秒数。
        /// </summary>
        public int LastFileBridgeTimeoutMs { get; private set; }

        /// <summary>
        /// 获取或设置下一次 FastChannel 调用需要返回的预期连接或协议失败。
        /// </summary>
        public Exception? FastChannelFailure { get; set; }

        /// <summary>
        /// 获取或设置下一次 FileBridge 调用需要返回的协议异常。
        /// </summary>
        public Exception? FileBridgeFailure { get; set; }

        /// <summary>
        /// 获取或设置 FileBridge terminal response 状态，供 Runtime 失败投影测试使用。
        /// </summary>
        public string FileBridgeResponseStatus { get; set; } = "Success";

        /// <summary>
        /// 获取或设置 FileBridge terminal response 错误说明。
        /// </summary>
        public string FileBridgeErrorMessage { get; set; } = string.Empty;

        /// <summary>获取或设置 FileBridge terminal response 的 result JSON。</summary>
        public string FileBridgeResultJson { get; set; } = FILE_BRIDGE_RESULT_JSON;

        /// <summary>
        /// 创建同时可注入为 <see cref="IYokiFrameClient"/> 的记录型代理。
        /// </summary>
        /// <returns>持有动态 Client 和调用记录的代理。</returns>
        public static RecordingYokiFrameClientProxy Create()
        {
            IYokiFrameClient client = DispatchProxy.Create<IYokiFrameClient, RecordingYokiFrameClientProxy>();
            var recorder = (RecordingYokiFrameClientProxy)(object)client;
            recorder.mClient = client;
            return recorder;
        }

        /// <summary>
        /// 拦截 Client 接口调用，并针对两类命令通道返回各自完整的响应对象。
        /// </summary>
        /// <param name="targetMethod">动态代理收到的接口成员。</param>
        /// <param name="_">调用参数；本测试只验证通道选择，不重复验证 Client 参数校验。</param>
        /// <returns>与被调用接口成员返回类型一致的值。</returns>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? arguments)
        {
            if (targetMethod == null)
            {
                throw new ArgumentNullException(nameof(targetMethod));
            }

            return targetMethod.Name switch
            {
                "get_Paths" => mPaths,
                nameof(IYokiFrameClient.ReadEngineEntries) => new[] { mRegistry },
                FAST_CHANNEL_CAPABILITY_METHOD_NAME => CanSendFastChannel(arguments),
                FAST_CHANNEL_METHOD_NAME => SendFastChannelResponse(arguments),
                nameof(IYokiFrameClient.SendCommandAsync) => SendFileBridgeResponse(arguments),
                _ => throw new NotSupportedException("测试代理不支持 Client 成员：" + targetMethod.Name)
            };
        }

        /// <summary>
        /// 记录未来 FastChannel 调用，并返回没有命令或结果文件路径的直接响应。
        /// </summary>
        /// <returns>快速通道的直接响应任务。</returns>
        private Task<CommandResponse> SendFastChannelResponse(object?[]? arguments)
        {
            if (arguments == null || arguments.Length < 6)
            {
                throw new InvalidOperationException("FastChannel 测试调用缺少命令参数。");
            }

            FastChannelCallCount++;
            LastFastChannelKit = Assert.IsType<string>(arguments[1]);
            LastFastChannelAction = Assert.IsType<string>(arguments[2]);
            LastFastChannelPayloadJson = Assert.IsType<string>(arguments[3]);
            LastFastChannelTimeoutMs = Assert.IsType<int>(arguments[5]);
            if (FastChannelFailure != null)
            {
                return Task.FromException<CommandResponse>(FastChannelFailure);
            }

            return Task.FromResult(CreateResponse(FAST_CHANNEL_RESULT_JSON));
        }

        /// <summary>
        /// 模拟 engine registry 对只读 FastChannel 命令的明确声明。
        /// </summary>
        /// <param name="arguments">能力查询参数。</param>
        /// <returns>当前测试 Host 是否声明该只读命令。</returns>
        private bool CanSendFastChannel(object?[]? arguments)
        {
            if (arguments == null || arguments.Length < 3)
            {
                return false;
            }

            var key = Assert.IsType<string>(arguments[1]) + "/" + Assert.IsType<string>(arguments[2]);
            return key is "System/ping"
                or "System/bridge_status"
                or "System/list_commands"
                or "System/get_environment"
                or "FsmKit/get_state";
        }

        /// <summary>
        /// 记录当前可靠 FileBridge 调用，并返回携带文件证据路径的命令发送结果。
        /// </summary>
        /// <returns>FileBridge 命令结果任务。</returns>
        private Task<CommandSendResult> SendFileBridgeResponse(object?[]? arguments)
        {
            if (arguments == null || arguments.Length < 6)
            {
                throw new InvalidOperationException("FileBridge 测试调用缺少命令参数。");
            }

            FileBridgeCallCount++;
            var engineId = Assert.IsType<string>(arguments[0]);
            LastFileBridgeKit = Assert.IsType<string>(arguments[1]);
            LastFileBridgeAction = Assert.IsType<string>(arguments[2]);
            LastFileBridgePayloadJson = Assert.IsType<string>(arguments[3]);
            if (FileBridgeCallCount == 1)
            {
                FirstFileBridgeAction = LastFileBridgeAction;
                FirstFileBridgePayloadJson = LastFileBridgePayloadJson;
            }
            LastFileBridgeSource = Assert.IsType<string>(arguments[4]);
            LastFileBridgeTimeoutMs = Assert.IsType<int>(arguments[5]);
            if (FileBridgeFailure != null)
            {
                return Task.FromException<CommandSendResult>(FileBridgeFailure);
            }

            CommandEnvelope envelope = new()
            {
                ProtocolVersion = 2,
                EngineId = engineId,
                Source = LastFileBridgeSource,
                RequestId = "test-request",
                Kit = LastFileBridgeKit,
                Action = LastFileBridgeAction,
                PayloadJson = LastFileBridgePayloadJson,
                TimeoutMs = LastFileBridgeTimeoutMs
            };
            var result = new CommandSendResult(
                envelope,
                "filebridge-command.json",
                "filebridge-response.json",
                CreateResponse(FileBridgeResultJson, FileBridgeResponseStatus, FileBridgeErrorMessage));
            return Task.FromResult(result);
        }

        /// <summary>
        /// 创建 Workbench 可直接投影的成功响应，以区分两条传输路径。
        /// </summary>
        /// <param name="resultJson">响应内携带的通道标识 JSON。</param>
        /// <returns>完整的 terminal response。</returns>
        private static CommandResponse CreateResponse(string resultJson, string status = "Success", string errorMessage = "")
        {
            return new CommandResponse
            {
                ProtocolVersion = 2,
                RequestId = "test-request",
                EngineId = "unity-editor",
                Status = status,
                ResultJson = resultJson,
                ErrorCode = status == "Success" ? string.Empty : "HostRejected",
                ErrorMessage = errorMessage,
                CompletedAtUtc = "2026-07-11T00:00:00.0000000Z"
            };
        }
    }
}
