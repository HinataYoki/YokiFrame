using System.Text.Json.Nodes;
using YokiFrame;
using YokiFrame.Protocol.Common;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Telemetry.SharedMemory;
using YokiFrame.Protocol.Validation;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.FsmKit;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>
/// 承载 WorkbenchDashboardService 的 FsmKit 强类型投影和显式详情查询。
/// </summary>
public sealed partial class WorkbenchDashboardService
{
    private const string FSM_KIT = "FsmKit";
    private const string FSM_WORKBENCH_ACTION = "get_workbench_snapshot";

    /// <summary>
    /// 按稳定 instanceId 显式查询一个 FSM 的完整工作台详情。
    /// Application 负责构造 payload，并通过共享 CommandExecutionService 保留实际传输和证据。
    /// </summary>
    /// <param name="engineId">目标 engine；为空时只自动选择唯一在线 engine。</param>
    /// <param name="instanceId">FsmKit 注册表返回的稳定实例标识。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含强类型详情、实际传输和证据路径的 FsmKit 状态。</returns>
    public async Task<WorkbenchFsmKitState> QueryFsmDetailsAsync(
        string engineId,
        string instanceId,
        CancellationToken cancellationToken)
    {
        var selectedInstanceId = SafeIdValidator.EnsureSafeId(instanceId, nameof(instanceId));
        var selectedEngineId = mEngineSelectionService.Resolve(engineId, DateTimeOffset.UtcNow);
        var registryBeforeCommand = FindEngineRegistry(selectedEngineId);
        JsonObject payload = new() { ["instanceId"] = selectedInstanceId };
        var result = await mCommandExecutionService.ExecuteAsync(
            selectedEngineId,
            FSM_KIT,
            FSM_WORKBENCH_ACTION,
            payload.ToJsonString(YokiFrameJson.CompactOptions),
            WORKBENCH_SOURCE,
            COMMAND_TIMEOUT_MS,
            cancellationToken).ConfigureAwait(false);
        EnsureSuccessfulFsmQuery(result);
        var registryAfterCommand = FindEngineRegistry(selectedEngineId);
        var dataSource = CreateCommandDataSource(
            selectedEngineId,
            registryBeforeCommand,
            registryAfterCommand,
            result);
        return WorkbenchFsmKitStateParser.Parse(dataSource);
    }

    /// <summary>
    /// 从命令结果和当前 registry 创建详情查询数据源，不让 UI 解析宿主身份或证据路径。
    /// </summary>
    /// <param name="engineId">已选择的 engine 标识。</param>
    /// <param name="registryBeforeCommand">命令发送前读取的宿主身份。</param>
    /// <param name="registryAfterCommand">命令响应后读取的宿主身份。</param>
    /// <param name="result">共享命令用例结果。</param>
    /// <returns>可交给 FsmKit parser 的数据源。</returns>
    private WorkbenchFsmKitDataSource CreateCommandDataSource(
        string engineId,
        EngineRegistryEntry? registryBeforeCommand,
        EngineRegistryEntry? registryAfterCommand,
        CommandExecutionResult result)
    {
        var response = result.Response;
        var evidencePaths = CreateCommandEvidencePaths(result);
        var staleReason = CreateCommandIdentityStaleReason(registryBeforeCommand, registryAfterCommand);
        var identity = string.IsNullOrWhiteSpace(staleReason) ? registryAfterCommand : null;
        var updatedAtUtc = TryParseUtcTimestamp(response.CompletedAtUtc, out var timestamp)
            ? timestamp
            : DateTimeOffset.MinValue;
        if (updatedAtUtc == DateTimeOffset.MinValue)
        {
            staleReason = CombineStaleReasons(staleReason, "FsmKit command completedAtUtc is missing or invalid.");
        }

        return new WorkbenchFsmKitDataSource(
            engineId,
            identity?.SessionId ?? string.Empty,
            identity?.Generation ?? 0L,
            identity?.Mode ?? string.Empty,
            updatedAtUtc,
            "command",
            result.Transport,
            evidencePaths,
            staleReason,
            response.ResultJson);
    }

    /// <summary>
    /// 验证 FsmKit terminal response 成功；失败时保留宿主错误码和 FileBridge 证据。
    /// </summary>
    /// <param name="result">共享命令用例结果。</param>
    private static void EnsureSuccessfulFsmQuery(CommandExecutionResult result)
    {
        if (string.Equals(result.Response.Status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var response = result.Response;
        throw new YokiFrameProtocolException(new YokiFrameError(
            response.ErrorCode,
            response.ErrorMessage,
            "Refresh the FsmKit instance list and retry with a current instanceId.",
            CreateCommandEvidencePaths(result)));
    }

    /// <summary>
    /// 在发送命令前读取当前 engine registry，避免响应完成后才因宿主身份读取失败而丢失结果。
    /// </summary>
    /// <param name="engineId">目标 engine 标识。</param>
    /// <returns>匹配 registry；不存在时为空。</returns>
    private EngineRegistryEntry? FindEngineRegistry(string engineId)
    {
        return mClient.ReadEngineEntries().FirstOrDefault(
            entry => string.Equals(entry.EngineId, engineId, StringComparison.Ordinal));
    }

    /// <summary>
    /// 提取 FileBridge command/response 证据；FastChannel 结果自然返回空列表。
    /// </summary>
    /// <param name="result">共享命令用例结果。</param>
    /// <returns>非空证据路径。</returns>
    private static IReadOnlyList<string> CreateCommandEvidencePaths(CommandExecutionResult result)
    {
        List<string> paths = new(2);
        if (!string.IsNullOrWhiteSpace(result.CommandPath)) paths.Add(result.CommandPath);
        if (!string.IsNullOrWhiteSpace(result.ResponsePath)) paths.Add(result.ResponsePath);
        return paths;
    }

    /// <summary>
    /// 严格校验 snapshot 信封身份和 payload 文本，避免旧 generation 或外层信封被解释成 FsmKit 数据。
    /// </summary>
    /// <param name="node">从受控 snapshot 路径读到的 JSON 节点。</param>
    /// <param name="engineId">当前选中 engine 标识。</param>
    /// <param name="kit">当前读取的 Kit 名称。</param>
    /// <param name="name">当前读取的 snapshot 名称。</param>
    /// <param name="expectedGeneration">当前 bridge 已确认的 generation。</param>
    /// <returns>业务 payload、时间和不可用原因。</returns>
    private static WorkbenchSnapshotEnvelopeReadResult ReadSnapshotEnvelope(
        JsonNode node,
        string engineId,
        string kit,
        string name,
        long expectedGeneration)
    {
        if (node is not JsonObject envelope)
        {
            return WorkbenchSnapshotEnvelopeReadResult.Invalid(node.ToJsonString(YokiFrameJson.CompactOptions), "Snapshot envelope root must be an object.");
        }

        var identityReason = ValidateSnapshotEnvelopeIdentity(envelope, engineId, kit, name, expectedGeneration);
        if (!string.IsNullOrWhiteSpace(identityReason))
        {
            return WorkbenchSnapshotEnvelopeReadResult.Invalid(envelope.ToJsonString(YokiFrameJson.CompactOptions), identityReason);
        }

        var payloadJson = ReadOptionalString(envelope, "payloadJson");
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return WorkbenchSnapshotEnvelopeReadResult.Invalid(envelope.ToJsonString(YokiFrameJson.CompactOptions), "Snapshot payloadJson is missing or must be a non-empty string.");
        }

        if (!TryReadSnapshotUpdatedAtUtc(envelope, out var updatedAtUtc))
        {
            return WorkbenchSnapshotEnvelopeReadResult.Stale(payloadJson, "Snapshot writtenAtUtc is missing or invalid.");
        }

        return WorkbenchSnapshotEnvelopeReadResult.Available(payloadJson, updatedAtUtc);
    }

    /// <summary>
    /// 校验信封版本、路径身份和 generation，拒绝当前 bridge 无法证明属于本会话的数据。
    /// </summary>
    private static string ValidateSnapshotEnvelopeIdentity(
        JsonObject envelope,
        string engineId,
        string kit,
        string name,
        long expectedGeneration)
    {
        if (!TryReadInt32(envelope, "protocolVersion", out var protocolVersion)
            || protocolVersion != YokiFrameFileBridgeContract.PROTOCOL_VERSION)
        {
            return "Snapshot protocolVersion does not match the current contract.";
        }

        if (!MatchesRequiredString(envelope, "engineId", engineId)
            || !MatchesRequiredString(envelope, "kit", kit)
            || !MatchesRequiredString(envelope, "name", name))
        {
            return "Snapshot engineId, kit or name does not match the requested state.";
        }

        if (!TryReadInt64(envelope, "generation", out var generation) || generation <= 0L)
        {
            return "Snapshot generation is missing or invalid.";
        }

        return expectedGeneration != 0L && generation != expectedGeneration
            ? "Snapshot generation does not match the current bridge generation."
            : string.Empty;
    }

    /// <summary>
    /// 把 telemetry 回落前的证据和最终 snapshot 路径合并为稳定且无重复的证据列表。
    /// </summary>
    private static IReadOnlyList<string> CreateSnapshotEvidencePaths(
        string snapshotPath,
        IReadOnlyList<string>? priorEvidencePaths)
    {
        List<string> paths = new();
        if (priorEvidencePaths != null)
        {
            foreach (var path in priorEvidencePaths)
            {
                if (!string.IsNullOrWhiteSpace(path) && !paths.Contains(path, StringComparer.Ordinal))
                {
                    paths.Add(path);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(snapshotPath) && !paths.Contains(snapshotPath, StringComparer.Ordinal))
        {
            paths.Add(snapshotPath);
        }

        return paths;
    }

    /// <summary>
    /// 把 telemetry header ticks 转为 UTC 时间；异常 ticks 返回空而不影响 snapshot 回落。
    /// </summary>
    /// <param name="header">已接受 telemetry header。</param>
    /// <returns>有效 UTC 时间；header 缺失或 ticks 无效时为空。</returns>
    private static DateTimeOffset? ReadTelemetryUpdatedAtUtc(SharedMemoryTelemetryFrameHeader? header)
    {
        if (header == null || header.WrittenAtUtcTicks <= 0L)
        {
            return null;
        }

        try
        {
            return new DateTimeOffset(header.WrittenAtUtcTicks, TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// 安全读取 JsonNode 字符串字段，类型不匹配时返回空。
    /// </summary>
    private static string? ReadOptionalString(JsonNode node, string name)
    {
        try
        {
            return node[name]?.GetValue<string>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// 解析协议时间文本并统一为 UTC；无效文本由调用方明确标记 stale。
    /// </summary>
    private static bool TryParseUtcTimestamp(string text, out DateTimeOffset timestamp)
    {
        if (DateTimeOffset.TryParse(text, out var parsed))
        {
            timestamp = parsed.ToUniversalTime();
            return true;
        }

        timestamp = DateTimeOffset.MinValue;
        return false;
    }

    /// <summary>
    /// 读取 snapshot 时间字段，优先 writtenAtUtc，再兼容其余协议时间别名。
    /// </summary>
    private static bool TryReadSnapshotUpdatedAtUtc(JsonObject envelope, out DateTimeOffset updatedAtUtc)
    {
        var timestamp = ReadOptionalString(envelope, "writtenAtUtc")
            ?? ReadOptionalString(envelope, "updatedAtUtc")
            ?? ReadOptionalString(envelope, "createdAtUtc");
        return TryParseUtcTimestamp(timestamp ?? string.Empty, out updatedAtUtc);
    }

    /// <summary>
    /// 判断信封中的字符串字段是否存在且精确匹配当前请求身份。
    /// </summary>
    private static bool MatchesRequiredString(JsonObject envelope, string name, string expectedValue)
    {
        var value = ReadOptionalString(envelope, name);
        return !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, expectedValue, StringComparison.Ordinal);
    }

    /// <summary>
    /// 读取 Int32 JSON number；字符串和其它类型不属于当前 snapshot 信封契约。
    /// </summary>
    private static bool TryReadInt32(JsonObject envelope, string name, out int value)
    {
        if (envelope[name] is not JsonValue jsonValue)
        {
            value = 0;
            return false;
        }

        if (jsonValue.TryGetValue<int>(out value))
        {
            return true;
        }

        if (jsonValue.TryGetValue<long>(out var longValue)
            && longValue is >= int.MinValue and <= int.MaxValue)
        {
            value = (int)longValue;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// 读取 Int64 JSON number；字符串和其它类型不属于当前 snapshot 信封契约。
    /// </summary>
    private static bool TryReadInt64(JsonObject envelope, string name, out long value)
    {
        if (envelope[name] is not JsonValue jsonValue)
        {
            value = 0L;
            return false;
        }

        if (jsonValue.TryGetValue<long>(out value))
        {
            return true;
        }

        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            value = intValue;
            return true;
        }

        value = 0L;
        return false;
    }

    /// <summary>
    /// 判断命令前后 registry 是否仍指向同一可证明的宿主会话。
    /// </summary>
    private static string CreateCommandIdentityStaleReason(
        EngineRegistryEntry? registryBeforeCommand,
        EngineRegistryEntry? registryAfterCommand)
    {
        if (registryBeforeCommand == null
            || registryAfterCommand == null
            || string.IsNullOrWhiteSpace(registryBeforeCommand.SessionId)
            || string.IsNullOrWhiteSpace(registryAfterCommand.SessionId)
            || registryBeforeCommand.Generation <= 0L
            || registryAfterCommand.Generation <= 0L)
        {
            return "FsmKit command host identity could not be confirmed before and after completion.";
        }

        return string.Equals(registryBeforeCommand.SessionId, registryAfterCommand.SessionId, StringComparison.Ordinal)
            && registryBeforeCommand.Generation == registryAfterCommand.Generation
            ? string.Empty
            : "FsmKit command host session or generation changed while waiting for the response.";
    }

    /// <summary>
    /// 封装已验证或已拒绝的 snapshot 信封读取结果，避免无效 envelope 漏入业务 parser。
    /// </summary>
    private sealed class WorkbenchSnapshotEnvelopeReadResult
    {
        /// <summary>使用指定结果字段创建信封读取结果。</summary>
        private WorkbenchSnapshotEnvelopeReadResult(
            bool isReadable,
            string payloadJson,
            string previewJson,
            DateTimeOffset? updatedAtUtc,
            string staleReason)
        {
            IsReadable = isReadable;
            PayloadJson = payloadJson;
            PreviewJson = previewJson;
            UpdatedAtUtc = updatedAtUtc;
            StaleReason = staleReason;
        }

        /// <summary>获取 payload 是否可以交给业务 parser。</summary>
        public bool IsReadable { get; }

        /// <summary>获取已验证的业务 payload；不可读时为空。</summary>
        public string PayloadJson { get; }

        /// <summary>获取用于通用 snapshot 区域的安全预览。</summary>
        public string PreviewJson { get; }

        /// <summary>获取已解析的更新时间；无效时为空。</summary>
        public DateTimeOffset? UpdatedAtUtc { get; }

        /// <summary>获取 envelope 或时间导致的 stale 原因。</summary>
        public string StaleReason { get; }

        /// <summary>创建可正常读取的信封结果。</summary>
        public static WorkbenchSnapshotEnvelopeReadResult Available(string payloadJson, DateTimeOffset updatedAtUtc)
        {
            return new WorkbenchSnapshotEnvelopeReadResult(true, payloadJson, payloadJson, updatedAtUtc, string.Empty);
        }

        /// <summary>创建 payload 可读但时间失效的 stale 结果。</summary>
        public static WorkbenchSnapshotEnvelopeReadResult Stale(string payloadJson, string staleReason)
        {
            return new WorkbenchSnapshotEnvelopeReadResult(true, payloadJson, payloadJson, null, staleReason);
        }

        /// <summary>创建不可读信封结果并保留其预览。</summary>
        public static WorkbenchSnapshotEnvelopeReadResult Invalid(string previewJson, string staleReason)
        {
            return new WorkbenchSnapshotEnvelopeReadResult(false, string.Empty, previewJson, null, staleReason);
        }
    }

    /// <summary>
    /// 合并两个非空 stale 原因，避免回落和最终读取错误相互覆盖。
    /// </summary>
    private static string CombineStaleReasons(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(second) ? first : first + " " + second;
    }
}
