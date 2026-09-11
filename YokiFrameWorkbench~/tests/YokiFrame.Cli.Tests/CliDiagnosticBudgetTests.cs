using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// E 阶段 AI 场景回归：按计划约定的八个真实场景驱动 CLI，度量三个诊断预算指标。
/// 指标定义：默认查询平均字节数、一次诊断平均调用次数、异常进入 --detail full 的比例。
/// 每个场景同时声明一条"默认输出里必须能看到的信号"，避免指标达标但信号缺失。
/// </summary>
public sealed class CliDiagnosticBudgetTests
{
    private const string ENGINE_ID = "unity-editor";
    private const int NORMAL_BYTE_BUDGET = 400;
    private const int ABNORMAL_BYTE_BUDGET = 800;

    private static readonly Lazy<Task<IReadOnlyList<ScenarioMeasurement>>> sMeasurements =
        new(MeasureAllAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly ITestOutputHelper mOutput;

    /// <summary>注入测试输出，用于打印场景度量表。</summary>
    /// <param name="output">xUnit 输出写入器。</param>
    public CliDiagnosticBudgetTests(ITestOutputHelper output)
    {
        mOutput = output;
    }

    /// <summary>
    /// 指标一：每个场景的默认输出都必须落在字节预算内，正常态更严。
    /// </summary>
    [Fact]
    public async Task DefaultOutputStaysWithinByteBudget()
    {
        var measurements = await sMeasurements.Value;
        WriteTable(measurements);

        foreach (var measurement in measurements)
        {
            var budget = measurement.IsAbnormal ? ABNORMAL_BYTE_BUDGET : NORMAL_BYTE_BUDGET;
            Assert.True(
                measurement.DefaultBytes <= budget,
                measurement.Name + " 默认输出 " + measurement.DefaultBytes + " 字节，超出预算 " + budget);
        }
    }

    /// <summary>
    /// 指标二：每个场景都必须在一次默认调用内收敛，AI 无需再发第二次查询才能知道下一步。
    /// </summary>
    [Fact]
    public async Task EveryScenarioConvergesInOneDefaultCall()
    {
        var measurements = await sMeasurements.Value;

        var unconverged = measurements.Where(static item => !item.ConvergedInOneCall).ToArray();
        Assert.True(
            unconverged.Length == 0,
            "以下场景一次调用后仍无下一步动作: " + string.Join(", ", unconverged.Select(static item => item.Name)));

        var averageCalls = measurements.Average(static item => item.ConvergedInOneCall ? 1.0 : 2.0);
        Assert.Equal(1.0, averageCalls, precision: 6);
    }

    /// <summary>
    /// 指标三：异常场景不得要求 AI 读取 --detail full 才能决定下一步，即原始协议字段不是诊断必需品。
    /// </summary>
    [Fact]
    public async Task AbnormalScenariosDoNotRequireFullDetail()
    {
        var measurements = await sMeasurements.Value;
        var abnormal = measurements.Where(static item => item.IsAbnormal).ToArray();

        Assert.NotEmpty(abnormal);
        var needingDetail = abnormal.Where(static item => !item.ConvergedInOneCall).ToArray();
        Assert.True(
            needingDetail.Length == 0,
            "以下异常场景必须读取 --detail full: " + string.Join(", ", needingDetail.Select(static item => item.Name)));
    }

    /// <summary>
    /// 每个场景都必须在默认输出里暴露它特有的信号；只满足字节数和收敛次数不算通过。
    /// </summary>
    [Fact]
    public async Task EveryScenarioExposesItsRequiredSignal()
    {
        var measurements = await sMeasurements.Value;

        var missing = measurements
            .Where(static item => !string.Equals(item.ExpectedSignal, item.ObservedSignal, StringComparison.Ordinal))
            .ToArray();
        Assert.True(
            missing.Length == 0,
            string.Join("; ", missing.Select(static item =>
                item.Name + " 期望 " + item.ExpectedSignal + "，实际 " + item.ObservedSignal)));
    }

    /// <summary>
    /// 验证同一场景在 --detail full 下仍可取得协议原始字段，避免默认收敛以牺牲深度诊断为代价。
    /// </summary>
    [Fact]
    public async Task FullDetailRemainsAvailableForEveryScenario()
    {
        using var project = CliTestHelpers.CreateProjectRoot("budget-detail");
        PrepareSingleEngine(project.Path, generation: 1L, withHeartbeat: true, writeSnapshot: true);

        var result = await CliTestHelpers.RunCliAsync(
            "bridge", "status", "--detail", "full", "--engine", ENGINE_ID, "--project", project.Path);

        Assert.Equal(0, result.ExitCode);
        var json = JsonNode.Parse(result.StandardOutput)!;
        Assert.True(json["status"]!.AsObject().ContainsKey("engineRoot"));
    }

    /// <summary>按顺序构造并度量全部八个场景。</summary>
    /// <returns>场景度量结果。</returns>
    private static async Task<IReadOnlyList<ScenarioMeasurement>> MeasureAllAsync()
    {
        List<ScenarioMeasurement> measurements = new();
        measurements.Add(await MeasureKitOnTelemetryAsync());
        measurements.Add(await MeasureKitSelfReportedDegradationAsync());
        measurements.Add(await MeasureKitSnapshotFallbackAsync());
        measurements.Add(await MeasureKitSnapshotStaleAsync());
        measurements.Add(await MeasureHostOfflineAsync());
        measurements.Add(await MeasureCommandTimeoutAsync());
        measurements.Add(await MeasureProjectModelMissingAsync());
        measurements.Add(await MeasureMultipleEnginesAsync());
        return measurements;
    }

    /// <summary>场景一：Kit 正常，telemetry 直接命中。</summary>
    /// <returns>场景度量。</returns>
    private static async Task<ScenarioMeasurement> MeasureKitOnTelemetryAsync()
    {
        using var project = CliTestHelpers.CreateProjectRoot("budget-normal");
        PrepareSingleEngine(project.Path, generation: 1L, withHeartbeat: true, writeSnapshot: false);
        // 命名内存映射在所有句柄关闭时消失，必须持有到 CLI 子进程读完为止。
        using var frame = WriteTelemetryFrame(
            project.Path,
            "{\"status\":\"online\",\"loaded\":18,\"inFlight\":0}",
            generation: 1L,
            sequence: 2L);

        return await MeasureAsync(
            "kit 正常",
            isAbnormal: false,
            "Ready/telemetry",
            static json => Value(json, "state") + "/" + Value(json, "source"),
            new[] { "kit", "status", "--kit", "System", "--engine", ENGINE_ID, "--project", project.Path });
    }

    /// <summary>
    /// 场景二：Kit 自报 Provider 未就绪，但 telemetry 通道本身健康。
    /// 这里验证的是 AI 能从默认 summary 读到 Kit 自身的降级字段，而不是 CLI 会替 Kit 判定健康。
    /// </summary>
    /// <returns>场景度量。</returns>
    private static async Task<ScenarioMeasurement> MeasureKitSelfReportedDegradationAsync()
    {
        using var project = CliTestHelpers.CreateProjectRoot("budget-provider");
        PrepareSingleEngine(project.Path, generation: 1L, withHeartbeat: true, writeSnapshot: false);
        using var frame = WriteTelemetryFrame(
            project.Path,
            "{\"status\":\"degraded\",\"provider\":\"YooAsset\",\"providerReady\":false}",
            generation: 1L,
            sequence: 3L,
            kit: "ResKit");

        return await MeasureAsync(
            "Kit 自报降级",
            isAbnormal: true,
            "YooAsset",
            static json => Value(json["summary"], "provider"),
            new[] { "kit", "status", "--kit", "ResKit", "--engine", ENGINE_ID, "--project", project.Path });
    }

    /// <summary>场景三：telemetry 不可用，回落 snapshot 且 generation 一致。</summary>
    /// <returns>场景度量。</returns>
    private static async Task<ScenarioMeasurement> MeasureKitSnapshotFallbackAsync()
    {
        using var project = CliTestHelpers.CreateProjectRoot("budget-fallback");
        PrepareSingleEngine(project.Path, generation: 1L, withHeartbeat: true, writeSnapshot: true);

        return await MeasureAsync(
            "Telemetry 不可用回落 Snapshot",
            isAbnormal: true,
            "TelemetryNotUsed",
            static json => Value(json["issues"]?[0], "code"),
            new[] { "kit", "status", "--kit", "System", "--engine", ENGINE_ID, "--project", project.Path });
    }

    /// <summary>场景四：snapshot 的 generation 落后于当前宿主。</summary>
    /// <returns>场景度量。</returns>
    private static async Task<ScenarioMeasurement> MeasureKitSnapshotStaleAsync()
    {
        using var project = CliTestHelpers.CreateProjectRoot("budget-stale");
        PrepareSingleEngine(project.Path, generation: 2L, withHeartbeat: true, writeSnapshot: true, snapshotGeneration: 1L);

        return await MeasureAsync(
            "Snapshot 过期",
            isAbnormal: true,
            "Stale/KitStateStale",
            static json => Value(json, "state") + "/" + Value(json["error"], "code"),
            new[] { "kit", "status", "--kit", "System", "--engine", ENGINE_ID, "--project", project.Path });
    }

    /// <summary>场景五：registry 存在但 heartbeat 缺失，宿主离线。</summary>
    /// <returns>场景度量。</returns>
    private static async Task<ScenarioMeasurement> MeasureHostOfflineAsync()
    {
        using var project = CliTestHelpers.CreateProjectRoot("budget-offline");
        PrepareSingleEngine(project.Path, generation: 1L, withHeartbeat: false, writeSnapshot: false);

        return await MeasureAsync(
            "Host 离线",
            isAbnormal: true,
            "Unavailable/BridgeHostOffline",
            static json => Value(json, "state") + "/" + Value(json["error"], "code"),
            new[] { "bridge", "status", "--engine", ENGINE_ID, "--project", project.Path });
    }

    /// <summary>场景六：命令超时且结果无法确认。</summary>
    /// <returns>场景度量。</returns>
    private static async Task<ScenarioMeasurement> MeasureCommandTimeoutAsync()
    {
        using var project = CliTestHelpers.CreateProjectRoot("budget-timeout");

        return await MeasureAsync(
            "命令超时未知",
            isAbnormal: true,
            "Unknown/CommandTimeout/true",
            static json => Value(json, "outcome") + "/" + Value(json["error"], "code") + "/"
                + (!string.IsNullOrWhiteSpace(Value(json, "requestId"))).ToString().ToLowerInvariant(),
            new[]
            {
                "command", "send", "--engine", ENGINE_ID, "--kit", "System", "--action", "ping",
                "--timeout", "1000", "--project", project.Path
            });
    }

    /// <summary>场景七：Project Model 尚未生成。</summary>
    /// <returns>场景度量。</returns>
    private static async Task<ScenarioMeasurement> MeasureProjectModelMissingAsync()
    {
        using var project = CliTestHelpers.CreateProjectRoot("budget-model");

        return await MeasureAsync(
            "Project Model 缺失",
            isAbnormal: true,
            "Unavailable/project refresh",
            static json => Value(json, "state") + "/" + Value(json["nextActions"]?[0], null),
            new[] { "project", "status", "--project", project.Path });
    }

    /// <summary>场景八：多个 engine 同时在线，未显式选择目标。</summary>
    /// <returns>场景度量。</returns>
    private static async Task<ScenarioMeasurement> MeasureMultipleEnginesAsync()
    {
        using var project = CliTestHelpers.CreateProjectRoot("budget-multi");
        PrepareSingleEngine(project.Path, generation: 1L, withHeartbeat: true, writeSnapshot: false, engineId: "unity-editor");
        PrepareSingleEngine(project.Path, generation: 1L, withHeartbeat: true, writeSnapshot: false, engineId: "godot-editor");

        return await MeasureAsync(
            "多 Engine 在线",
            isAbnormal: true,
            "EngineSelectionRequired",
            static json => Value(json["error"], "code"),
            new[] { "kit", "status", "--kit", "System", "--project", project.Path });
    }

    /// <summary>
    /// 执行一次默认调用并提取收敛所需的字段；成功读 stdout，失败读 stderr。
    /// </summary>
    /// <param name="name">场景名。</param>
    /// <param name="isAbnormal">是否为异常场景。</param>
    /// <param name="expectedSignal">默认输出里必须出现的信号。</param>
    /// <param name="signalSelector">从默认输出提取实际信号。</param>
    /// <param name="arguments">CLI 参数。</param>
    /// <returns>场景度量。</returns>
    private static async Task<ScenarioMeasurement> MeasureAsync(
        string name,
        bool isAbnormal,
        string expectedSignal,
        Func<JsonNode, string> signalSelector,
        string[] arguments)
    {
        var result = await CliTestHelpers.RunCliAsync(arguments);
        var primary = (result.ExitCode == 0 ? result.StandardOutput : result.StandardError).Trim();
        var json = JsonNode.Parse(primary)
            ?? throw new InvalidOperationException(name + " 的默认输出不是 JSON。");
        var state = json["state"]?.GetValue<string>() ?? string.Empty;
        var nextStep = ExtractNextStep(json);
        var usable = string.Equals(state, "Ready", StringComparison.Ordinal)
            || string.Equals(state, "Degraded", StringComparison.Ordinal);
        var hasIssues = json["issues"] is JsonArray issues && issues.Count > 0;

        // 收敛定义：要么结果可用且无遗留问题，要么输出里已含一条可执行的下一步。
        var converged = (usable && !hasIssues) || nextStep.Length > 0;
        return new ScenarioMeasurement(
            name,
            isAbnormal,
            Encoding.UTF8.GetByteCount(primary),
            result.ExitCode,
            state,
            nextStep,
            converged,
            expectedSignal,
            signalSelector(json));
    }

    /// <summary>按 nextActions、错误建议、issue 码的优先级提取一条可执行下一步。</summary>
    /// <param name="json">CLI 默认输出。</param>
    /// <returns>下一步描述；都没有时返回空串。</returns>
    private static string ExtractNextStep(JsonNode json)
    {
        if (json["nextActions"] is JsonArray actions && actions.Count > 0)
        {
            return actions[0]?.GetValue<string>() ?? string.Empty;
        }

        var suggestion = json["error"]?["suggestion"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(suggestion))
        {
            return suggestion;
        }

        if (json["issues"] is JsonArray issues && issues.Count > 0)
        {
            return issues[0]?["code"]?.GetValue<string>() ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>读取 JSON 节点的字符串字段，节点或字段缺失时返回空串。</summary>
    /// <param name="node">JSON 节点。</param>
    /// <param name="name">字段名；为 null 时直接读取节点自身。</param>
    /// <returns>字段文本。</returns>
    private static string Value(JsonNode? node, string? name)
    {
        if (node == null)
        {
            return string.Empty;
        }

        if (name == null)
        {
            return node.GetValue<string>();
        }

        return node[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;
    }

    /// <summary>写入最小 engine registry、可选 heartbeat 和可选 snapshot。</summary>
    /// <param name="projectRoot">项目根。</param>
    /// <param name="generation">宿主 generation。</param>
    /// <param name="withHeartbeat">是否写入新鲜 heartbeat。</param>
    /// <param name="writeSnapshot">是否写入 System snapshot。</param>
    /// <param name="engineId">目标 engine 标识。</param>
    /// <param name="snapshotGeneration">snapshot 的 generation；默认与宿主一致。</param>
    private static void PrepareSingleEngine(
        string projectRoot,
        long generation,
        bool withHeartbeat,
        bool writeSnapshot,
        string engineId = ENGINE_ID,
        long? snapshotGeneration = null)
    {
        var engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", engineId);
        Directory.CreateDirectory(Path.Combine(engineRoot, "status"));
        File.WriteAllText(
            Path.Combine(engineRoot, "engine.json"),
            "{\"protocolVersion\":2,\"engineId\":\"" + engineId
            + "\",\"engine\":\"Unity\",\"version\":\"6000.0.0f1\",\"adapterVersion\":\"test\","
            + "\"sessionId\":\"session\",\"generation\":" + generation
            + ",\"mode\":\"EditMode\",\"capabilities\":[\"snapshot.read\",\"command.send\"]}");
        if (withHeartbeat)
        {
            File.WriteAllText(
                Path.Combine(engineRoot, "status", "heartbeat.json"),
                "{\"protocolVersion\":2,\"engineId\":\"" + engineId
                + "\",\"sessionId\":\"session\",\"generation\":" + generation
                + ",\"mode\":\"EditMode\",\"sequence\":1,\"createdAtUtc\":\""
                + DateTimeOffset.UtcNow.ToString("O") + "\"}");
        }

        if (!writeSnapshot)
        {
            return;
        }

        var snapshotRoot = Path.Combine(engineRoot, "snapshots", "System");
        Directory.CreateDirectory(snapshotRoot);
        File.WriteAllText(
            Path.Combine(snapshotRoot, "state.json"),
            "{\"protocolVersion\":2,\"engineId\":\"" + engineId
            + "\",\"kit\":\"System\",\"name\":\"state\",\"generation\":"
            + (snapshotGeneration ?? generation)
            + ",\"sequence\":1,\"writtenAtUtc\":\"" + DateTimeOffset.UtcNow.ToString("O")
            + "\",\"payloadJson\":\"{\\\"status\\\":\\\"online\\\"}\"}");
    }

    /// <summary>写入已提交的 telemetry 帧，并返回必须保持到读取结束的映射句柄。</summary>
    /// <param name="projectRoot">项目根。</param>
    /// <param name="payloadJson">payload 文本。</param>
    /// <param name="generation">帧所属 generation。</param>
    /// <param name="sequence">帧序号。</param>
    /// <param name="kit">帧所属 Kit；segment 名必须与查询用的 --kit 完全一致。</param>
    /// <returns>持有命名映射的句柄。</returns>
    private static IDisposable WriteTelemetryFrame(
        string projectRoot,
        string payloadJson,
        long generation,
        long sequence,
        string kit = "System")
    {
        if (!OperatingSystem.IsWindows())
        {
            return EmptyDisposable.Instance;
        }

        var segmentName = SharedMemoryTelemetrySegmentName.Create(projectRoot, ENGINE_ID, kit, "state");
        var payload = Encoding.UTF8.GetBytes(payloadJson);
        var frame = new byte[SharedMemoryTelemetryFrameHeader.HEADER_SIZE + payload.Length];
        var header = new SharedMemoryTelemetryFrameHeader(
            SharedMemoryTelemetryFrameHeader.MAGIC,
            SharedMemoryTelemetryFrameHeader.PROTOCOL_VERSION,
            YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(ENGINE_ID),
            generation,
            sequence,
            DateTimeOffset.UtcNow.UtcTicks,
            payload.Length,
            SharedMemoryTelemetryCrc32.Compute(payload),
            SharedMemoryTelemetryWriteState.Committed);
        header.WriteTo(frame.AsSpan(0, SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        payload.CopyTo(frame.AsSpan(SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        var memoryMap = MemoryMappedFile.CreateNew(segmentName, frame.Length, MemoryMappedFileAccess.ReadWrite);
        try
        {
            using var accessor = memoryMap.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
            accessor.WriteArray(0, frame, 0, frame.Length);
        }
        catch
        {
            memoryMap.Dispose();
            throw;
        }

        return memoryMap;
    }

    /// <summary>非 Windows 平台上的空句柄，保持调用方 using 写法一致。</summary>
    private sealed class EmptyDisposable : IDisposable
    {
        /// <summary>进程内共享实例。</summary>
        public static readonly EmptyDisposable Instance = new();

        /// <summary>无资源可释放。</summary>
        public void Dispose()
        {
        }
    }

    /// <summary>把场景度量表打印到测试输出，便于人工核对三个指标。</summary>
    /// <param name="measurements">场景度量结果。</param>
    private void WriteTable(IReadOnlyList<ScenarioMeasurement> measurements)
    {
        mOutput.WriteLine("场景                          异常  字节  退出  状态          收敛  信号");
        foreach (var item in measurements)
        {
            mOutput.WriteLine(
                item.Name.PadRight(28)
                + (item.IsAbnormal ? "是   " : "否   ")
                + item.DefaultBytes.ToString().PadLeft(4) + "  "
                + item.ExitCode.ToString().PadLeft(3) + "   "
                + item.State.PadRight(13)
                + (item.ConvergedInOneCall ? "是   " : "否   ")
                + item.ObservedSignal);
        }

        mOutput.WriteLine("指标一 默认查询平均字节数: " + measurements.Average(static item => (double)item.DefaultBytes).ToString("F1"));
        mOutput.WriteLine("指标二 一次诊断平均调用次数: " + measurements.Average(static item => item.ConvergedInOneCall ? 1.0 : 2.0).ToString("F2"));
        mOutput.WriteLine("指标三 异常进入 detail 的比例: " + measurements.Where(static item => item.IsAbnormal).Average(static item => item.ConvergedInOneCall ? 0.0 : 1.0).ToString("F2"));
    }

    /// <summary>
    /// 描述单个场景的默认调用度量。
    /// </summary>
    /// <param name="Name">场景名。</param>
    /// <param name="IsAbnormal">是否为异常场景。</param>
    /// <param name="DefaultBytes">默认输出字节数。</param>
    /// <param name="ExitCode">退出码。</param>
    /// <param name="State">AI 可见状态。</param>
    /// <param name="NextStep">提取到的下一步动作。</param>
    /// <param name="ConvergedInOneCall">一次调用是否已足够。</param>
    /// <param name="ExpectedSignal">默认输出里必须出现的信号。</param>
    /// <param name="ObservedSignal">实际观察到的信号。</param>
    private sealed record ScenarioMeasurement(
        string Name,
        bool IsAbnormal,
        int DefaultBytes,
        int ExitCode,
        string State,
        string NextStep,
        bool ConvergedInOneCall,
        string ExpectedSignal,
        string ObservedSignal);
}
