using System.Text.Json.Nodes;
using YokiFrame.Protocol.Common;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.PoolKit;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>承载 Dashboard 的 PoolKit 强类型状态和显式诊断操作。</summary>
public sealed partial class WorkbenchDashboardService
{
    private const string POOL_KIT = "PoolKit";
    private const string SET_POOL_TRACKING_ACTION = "set_tracking";
    private const string CHECK_POOL_LEAK_ACTION = "check_leak";
    private const string CLEAR_POOL_HISTORY_ACTION = "clear_history";

    /// <summary>应用当前会话 PoolKit 诊断开关，并返回 Provider 的完整新 state。</summary>
    public Task<WorkbenchPoolKitState> SetPoolKitTrackingAsync(
        string engineId,
        bool trackingEnabled,
        bool eventHistoryEnabled,
        bool stackTraceEnabled,
        CancellationToken cancellationToken)
    {
        JsonObject payload = new()
        {
            ["trackingEnabled"] = trackingEnabled,
            ["eventHistoryEnabled"] = eventHistoryEnabled,
            ["stackTraceEnabled"] = stackTraceEnabled
        };
        return ExecutePoolKitStateCommandAsync(
            engineId, SET_POOL_TRACKING_ACTION, payload, cancellationToken);
    }

    /// <summary>显式检查当前借出对象，并返回疑似未归还摘要。</summary>
    public Task<WorkbenchPoolKitState> CheckPoolKitLeaksAsync(
        string engineId,
        CancellationToken cancellationToken)
    {
        return ExecutePoolKitStateCommandAsync(
            engineId, CHECK_POOL_LEAK_ACTION, new JsonObject(), cancellationToken);
    }

    /// <summary>清空当前 PoolKit 事件历史并返回完整新 state。</summary>
    public Task<WorkbenchPoolKitState> ClearPoolKitHistoryAsync(
        string engineId,
        CancellationToken cancellationToken)
    {
        return ExecutePoolKitStateCommandAsync(
            engineId, CLEAR_POOL_HISTORY_ACTION, new JsonObject(), cancellationToken);
    }

    /// <summary>执行返回完整 PoolKit state 的命令并验证宿主身份。</summary>
    private async Task<WorkbenchPoolKitState> ExecutePoolKitStateCommandAsync(
        string engineId,
        string action,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        string selectedEngineId = mEngineSelectionService.Resolve(engineId, DateTimeOffset.UtcNow);
        var before = FindEngineRegistry(selectedEngineId);
        CommandExecutionResult result = await mCommandExecutionService.ExecuteAsync(
            selectedEngineId,
            POOL_KIT,
            action,
            payload.ToJsonString(YokiFrameJson.CompactOptions),
            WORKBENCH_SOURCE,
            COMMAND_TIMEOUT_MS,
            cancellationToken).ConfigureAwait(false);
        EnsureSuccessfulPoolKitCommand(result);
        var after = FindEngineRegistry(selectedEngineId);
        if (!IsSameHost(before, after))
        {
            throw CreatePoolKitIdentityError(result);
        }

        return WorkbenchPoolKitStateParser.Parse(CreatePoolKitCommandSource(selectedEngineId, after!, result));
    }

    /// <summary>创建携带实际传输和宿主身份的 PoolKit 命令数据源。</summary>
    private static WorkbenchPoolKitDataSource CreatePoolKitCommandSource(
        string engineId,
        Protocol.FileBridge.EngineRegistryEntry registry,
        CommandExecutionResult result)
    {
        DateTimeOffset updatedAtUtc = DateTimeOffset.TryParse(result.Response.CompletedAtUtc, out var completedAt)
            ? completedAt.ToUniversalTime()
            : DateTimeOffset.MinValue;
        return new WorkbenchPoolKitDataSource(
            engineId, registry.SessionId, registry.Generation, registry.Mode,
            updatedAtUtc, "command", result.Transport, CreateCommandEvidencePaths(result),
            string.Empty, result.Response.ResultJson);
    }

    /// <summary>验证 Runtime 返回 PoolKit 成功 terminal response。</summary>
    private static void EnsureSuccessfulPoolKitCommand(CommandExecutionResult result)
    {
        if (string.Equals(result.Response.Status, "Success", StringComparison.OrdinalIgnoreCase)) return;
        throw new YokiFrameProtocolException(new YokiFrameError(
            result.Response.ErrorCode,
            result.Response.ErrorMessage,
            "Refresh PoolKit state and retry.",
            CreateCommandEvidencePaths(result)));
    }

    /// <summary>创建命令期间宿主代次变化错误。</summary>
    private static YokiFrameProtocolException CreatePoolKitIdentityError(CommandExecutionResult result)
    {
        return new YokiFrameProtocolException(new YokiFrameError(
            "PoolKitCommandIdentityChanged",
            "PoolKit command result was rejected because the host session or generation changed.",
            "Refresh PoolKit state and retry against the current Runtime session.",
            CreateCommandEvidencePaths(result)));
    }
}
