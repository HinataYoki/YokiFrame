using System.Text.Json.Nodes;
using YokiFrame.Protocol.Common;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.ActionKit;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>承载 Dashboard 的 ActionKit 强类型状态和显式堆栈诊断操作。</summary>
public sealed partial class WorkbenchDashboardService
{
    private const string ACTION_KIT = "ActionKit";
    private const string SET_ACTION_STACK_TRACE = "set_stack_trace";
    private const string CLEAR_ACTION_STACK_TRACE = "clear_stack_trace";

    /// <summary>切换后续根 Action 的堆栈捕获，并返回 Provider 的完整新 state。</summary>
    /// <param name="engineId">目标 Runtime engine。</param>
    /// <param name="enabled">是否捕获后续 Start 调用堆栈。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>宿主确认后的 ActionKit 强类型状态。</returns>
    public Task<WorkbenchActionKitState> SetActionKitStackTraceAsync(
        string engineId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        JsonObject payload = new() { ["enabled"] = enabled };
        return ExecuteActionKitStateCommandAsync(
            engineId,
            SET_ACTION_STACK_TRACE,
            payload,
            cancellationToken);
    }

    /// <summary>清空当前活动根堆栈记录，并返回完整新 state。</summary>
    /// <param name="engineId">目标 Runtime engine。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>宿主确认后的 ActionKit 强类型状态。</returns>
    public Task<WorkbenchActionKitState> ClearActionKitStackTraceAsync(
        string engineId,
        CancellationToken cancellationToken)
    {
        return ExecuteActionKitStateCommandAsync(
            engineId,
            CLEAR_ACTION_STACK_TRACE,
            new JsonObject(),
            cancellationToken);
    }

    /// <summary>执行返回完整 ActionKit state 的命令并验证宿主身份。</summary>
    private async Task<WorkbenchActionKitState> ExecuteActionKitStateCommandAsync(
        string engineId,
        string action,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        string selectedEngineId = mEngineSelectionService.Resolve(engineId, DateTimeOffset.UtcNow);
        var before = FindEngineRegistry(selectedEngineId);
        CommandExecutionResult result = await mCommandExecutionService.ExecuteAsync(
            selectedEngineId,
            ACTION_KIT,
            action,
            payload.ToJsonString(YokiFrameJson.CompactOptions),
            WORKBENCH_SOURCE,
            COMMAND_TIMEOUT_MS,
            cancellationToken).ConfigureAwait(false);
        EnsureSuccessfulActionKitCommand(result);
        var after = FindEngineRegistry(selectedEngineId);
        if (!IsSameHost(before, after))
        {
            throw CreateActionKitIdentityError(result);
        }

        return WorkbenchActionKitStateParser.Parse(
            CreateActionKitCommandSource(selectedEngineId, after!, result));
    }

    /// <summary>创建携带实际传输和宿主身份的 ActionKit 命令数据源。</summary>
    private static WorkbenchActionKitDataSource CreateActionKitCommandSource(
        string engineId,
        Protocol.FileBridge.EngineRegistryEntry registry,
        CommandExecutionResult result)
    {
        DateTimeOffset updatedAtUtc = DateTimeOffset.TryParse(
            result.Response.CompletedAtUtc,
            out var completedAt)
                ? completedAt.ToUniversalTime()
                : DateTimeOffset.MinValue;
        return new WorkbenchActionKitDataSource(
            engineId,
            registry.SessionId,
            registry.Generation,
            registry.Mode,
            updatedAtUtc,
            "command",
            result.Transport,
            CreateCommandEvidencePaths(result),
            string.Empty,
            result.Response.ResultJson);
    }

    /// <summary>验证 Runtime 返回 ActionKit 成功 terminal response。</summary>
    private static void EnsureSuccessfulActionKitCommand(CommandExecutionResult result)
    {
        if (string.Equals(result.Response.Status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            result.Response.ErrorCode,
            result.Response.ErrorMessage,
            "Refresh ActionKit state and retry.",
            CreateCommandEvidencePaths(result)));
    }

    /// <summary>创建命令期间宿主代次变化错误。</summary>
    private static YokiFrameProtocolException CreateActionKitIdentityError(
        CommandExecutionResult result)
    {
        return new YokiFrameProtocolException(new YokiFrameError(
            "ActionKitCommandIdentityChanged",
            "ActionKit command result was rejected because the host session or generation changed.",
            "Refresh ActionKit state and retry against the current Runtime session.",
            CreateCommandEvidencePaths(result)));
    }
}
