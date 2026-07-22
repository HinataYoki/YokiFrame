using System.Text.Json.Nodes;
using YokiFrame.Protocol.Common;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.ResKit;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>承载 Dashboard 的 ResKit 强类型状态、按需详情和显式诊断操作。</summary>
public sealed partial class WorkbenchDashboardService
{
    private const string RES_KIT = "ResKit";
    private const string GET_RESOURCE_DETAIL_ACTION = "get_resource_detail";
    private const string SET_RES_TRACKING_ACTION = "set_tracking";
    private const string CLEAR_RES_HISTORY_ACTION = "clear_history";

    /// <summary>按当前诊断版本查询一个资源的独立 lease 来源。</summary>
    public async Task<WorkbenchResKitResourceDetail> GetResKitResourceDetailAsync(
        string engineId,
        string path,
        string typeName,
        CancellationToken cancellationToken)
    {
        JsonObject payload = new()
        {
            ["path"] = path,
            ["typeName"] = typeName
        };
        CommandExecutionResult result = await ExecuteResKitCommandAsync(
            engineId, GET_RESOURCE_DETAIL_ACTION, payload, cancellationToken).ConfigureAwait(false);
        return WorkbenchResKitStateParser.ParseResourceDetail(result.Response.ResultJson);
    }

    /// <summary>切换加载位置跟踪并返回 Runtime 的完整新 state。</summary>
    public Task<WorkbenchResKitState> SetResKitTrackingAsync(
        string engineId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        JsonObject payload = new() { ["loadLocationTrackingEnabled"] = enabled };
        return ExecuteResKitStateCommandAsync(
            engineId, SET_RES_TRACKING_ACTION, payload, cancellationToken);
    }

    /// <summary>清空卸载历史并返回 Runtime 的完整新 state。</summary>
    public Task<WorkbenchResKitState> ClearResKitHistoryAsync(
        string engineId,
        CancellationToken cancellationToken)
    {
        return ExecuteResKitStateCommandAsync(
            engineId, CLEAR_RES_HISTORY_ACTION, new JsonObject(), cancellationToken);
    }

    /// <summary>执行返回完整 ResKit state 的命令并转换为强类型状态。</summary>
    private async Task<WorkbenchResKitState> ExecuteResKitStateCommandAsync(
        string engineId,
        string action,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        CommandExecutionResult result = await ExecuteResKitCommandAsync(
            engineId, action, payload, cancellationToken).ConfigureAwait(false);
        string selectedEngineId = mEngineSelectionService.Resolve(engineId, DateTimeOffset.UtcNow);
        var registry = FindEngineRegistry(selectedEngineId)
            ?? throw CreateResKitIdentityError(result);
        return WorkbenchResKitStateParser.Parse(CreateResKitCommandSource(selectedEngineId, registry, result));
    }

    /// <summary>执行 ResKit command，并拒绝失败响应或执行期间宿主身份变化。</summary>
    private async Task<CommandExecutionResult> ExecuteResKitCommandAsync(
        string engineId,
        string action,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        string selectedEngineId = mEngineSelectionService.Resolve(engineId, DateTimeOffset.UtcNow);
        var before = FindEngineRegistry(selectedEngineId);
        CommandExecutionResult result = await mCommandExecutionService.ExecuteAsync(
            selectedEngineId,
            RES_KIT,
            action,
            payload.ToJsonString(YokiFrameJson.CompactOptions),
            WORKBENCH_SOURCE,
            COMMAND_TIMEOUT_MS,
            cancellationToken).ConfigureAwait(false);
        EnsureSuccessfulResKitCommand(result);
        var after = FindEngineRegistry(selectedEngineId);
        if (!IsSameHost(before, after)) throw CreateResKitIdentityError(result);
        return result;
    }

    /// <summary>创建携带实际传输和宿主身份的 ResKit 命令数据源。</summary>
    private static WorkbenchResKitDataSource CreateResKitCommandSource(
        string engineId,
        Protocol.FileBridge.EngineRegistryEntry registry,
        CommandExecutionResult result)
    {
        DateTimeOffset updatedAtUtc = DateTimeOffset.TryParse(result.Response.CompletedAtUtc, out var completedAt)
            ? completedAt.ToUniversalTime()
            : DateTimeOffset.MinValue;
        return new WorkbenchResKitDataSource(
            engineId, registry.SessionId, registry.Generation, registry.Mode,
            updatedAtUtc, "command", result.Transport, CreateCommandEvidencePaths(result),
            string.Empty, result.Response.ResultJson);
    }

    /// <summary>验证 Runtime 返回 ResKit 成功 terminal response。</summary>
    private static void EnsureSuccessfulResKitCommand(CommandExecutionResult result)
    {
        if (string.Equals(result.Response.Status, "Success", StringComparison.OrdinalIgnoreCase)) return;
        throw new YokiFrameProtocolException(new YokiFrameError(
            result.Response.ErrorCode,
            result.Response.ErrorMessage,
            "Refresh ResKit state and retry.",
            CreateCommandEvidencePaths(result)));
    }

    /// <summary>创建命令期间宿主代次变化错误。</summary>
    private static YokiFrameProtocolException CreateResKitIdentityError(CommandExecutionResult result)
    {
        return new YokiFrameProtocolException(new YokiFrameError(
            "ResKitCommandIdentityChanged",
            "ResKit command result was rejected because the host session or generation changed.",
            "Refresh ResKit state and retry against the current Runtime session.",
            CreateCommandEvidencePaths(result)));
    }
}
