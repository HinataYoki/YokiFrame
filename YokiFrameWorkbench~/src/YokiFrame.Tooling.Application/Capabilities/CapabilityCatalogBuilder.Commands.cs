using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame.Client.Commands;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.Common;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Validation;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.Capabilities;

namespace YokiFrame.Tooling.Application.Capabilities;

/// <summary>
/// 负责解析实时命令目录、校验 engine 身份并记录命令漂移。
/// </summary>
internal sealed partial class CapabilityCatalogBuilder
{
    /// <summary>
    /// 解析并应用本次 System/list_commands 响应；旧 session 或 generation 的目录只能标记 Stale。
    /// </summary>
    /// <param name="engine">目标 engine 内部节点。</param>
    /// <param name="before">发送前 registry。</param>
    /// <param name="after">发送后 registry。</param>
    /// <param name="result">共享命令执行结果。</param>
    public void ApplyCommandCatalog(
        CapabilityCatalogEngineBuilder engine,
        EngineRegistryEntry before,
        EngineRegistryEntry? after,
        HeartbeatInfo? afterHeartbeat,
        CommandExecutionResult result)
    {
        var evidencePaths = GetCommandEvidencePaths(result);
        foreach (var path in evidencePaths)
        {
            AddEvidence(path);
        }

        if (!string.Equals(result.Response.Status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            AddCommandFailure(engine, result.Response.ErrorCode, result.Response.ErrorMessage, "Inspect the terminal response and retry after correcting the engine state.", evidencePaths);
            return;
        }

        try
        {
            var root = ParseCommandRoot(result.Response.ResultJson, evidencePaths);
            var heartbeatIsCurrent = IsCurrentHeartbeat(engine, after, afterHeartbeat);
            var responseIdentityIsCurrent = ValidateCommandIdentity(root, before, after, engine.EngineId, evidencePaths);
            var identityIsCurrent = heartbeatIsCurrent && responseIdentityIsCurrent;
            var commands = ParseCommands(root, engine.EngineId, evidencePaths);
            engine.SetCommandCatalog(
                identityIsCurrent ? "Observed" : "Stale",
                ReadInt64(root, "sequence"),
                result.Transport,
                commands,
                evidencePaths);
            if (identityIsCurrent)
            {
                var declaredKits = mProjectModelTrusted && mProjectModelApplied
                    ? mProjectModelCommandKits
                    : mDeclaredCommandKits;
                foreach (var kit in declaredKits)
                {
                    GetKit(kit).CommandCatalogObserved = true;
                }

                foreach (var command in commands)
                {
                    GetKit(command.Kit).AddObserved(command);
                }
            }

            mSources.Add(new CapabilityCatalogSource("command-catalog", "System/list_commands", identityIsCurrent ? "Observed" : "Stale", engine.EngineId));
            if (!identityIsCurrent)
            {
                AddIssue("CommandCatalogStale", "Warning", engine.EngineId, "The command catalog completed across a session or generation change.", "Refresh the engine and request the catalog again.", evidencePaths);
                if (!heartbeatIsCurrent)
                {
                    AddHeartbeatTrustIssue(engine, after, afterHeartbeat, evidencePaths);
                }
            }
            else
            {
                DetectCommandDrift(commands, engine.EngineId, evidencePaths);
            }
        }
        catch (YokiFrameProtocolException exception)
        {
            AddCommandFailure(engine, exception.Error.Code, exception.Error.Message, exception.Error.Suggestion, exception.Error.EvidencePaths);
        }
    }

    /// <summary>
    /// 验证命令完成后的 registry 与 heartbeat 仍属于同一新鲜宿主身份。
    /// </summary>
    /// <param name="engine">命令开始时的 engine 节点。</param>
    /// <param name="after">命令完成后 registry。</param>
    /// <param name="afterHeartbeat">命令完成后 heartbeat。</param>
    /// <returns>身份和 freshness 均可信时返回 true。</returns>
    private bool IsCurrentHeartbeat(
        CapabilityCatalogEngineBuilder engine,
        EngineRegistryEntry? after,
        HeartbeatInfo? afterHeartbeat)
    {
        return engine.Online
            && string.Equals(engine.IdentityState, "Match", StringComparison.Ordinal)
            && after != null
            && afterHeartbeat != null
            && !afterHeartbeat.IsStale(mGeneratedAtUtc, TimeSpan.FromSeconds(15))
            && ResolveIdentityState(after, afterHeartbeat) == "Match";
    }

    /// <summary>
    /// 为无法信任的命令后 heartbeat 添加精确问题，避免 Stale 目录被误当作实时事实。
    /// </summary>
    /// <param name="engine">目标 engine。</param>
    /// <param name="heartbeat">命令完成后的 heartbeat。</param>
    /// <param name="evidencePaths">命令证据。</param>
    private void AddHeartbeatTrustIssue(
        CapabilityCatalogEngineBuilder engine,
        EngineRegistryEntry? registry,
        HeartbeatInfo? heartbeat,
        IReadOnlyList<string> evidencePaths)
    {
        if (registry == null)
        {
            AddIssue(
                "CommandCatalogRegistryMissing",
                "Warning",
                engine.EngineId,
                "The engine registry was missing when the command catalog completed.",
                "Refresh the engine registry before trusting the command catalog.",
                evidencePaths);
            return;
        }

        if (heartbeat == null)
        {
            AddIssue(
                "CommandCatalogHeartbeatMissing",
                "Warning",
                engine.EngineId,
                "The heartbeat was missing when the command catalog completed.",
                "Refresh the engine heartbeat before trusting the command catalog.",
                evidencePaths);
            return;
        }

        if (heartbeat.IsStale(mGeneratedAtUtc, TimeSpan.FromSeconds(15)))
        {
            AddIssue(
                "CommandCatalogHeartbeatStale",
                "Warning",
                engine.EngineId,
                "The heartbeat was stale when the command catalog completed.",
                "Refresh the engine session before trusting the command catalog.",
                evidencePaths.Concat(new[] { heartbeat.Path }).ToArray());
            return;
        }

        AddIssue(
            "CommandCatalogHeartbeatIdentityMismatch",
            "Warning",
            engine.EngineId,
            "The heartbeat identity did not match the registry when the command catalog completed.",
            "Wait for the engine session to settle, then refresh the catalog.",
            evidencePaths.Concat(new[] { heartbeat.Path }).ToArray());
    }

    /// <summary>
    /// 记录命令通道失败，同时保留 command/response 证据。
    /// </summary>
    /// <param name="engine">目标 engine。</param>
    /// <param name="code">错误码。</param>
    /// <param name="message">错误说明。</param>
    /// <param name="suggestion">恢复建议。</param>
    /// <param name="evidencePaths">证据路径。</param>
    public void AddCommandFailure(
        CapabilityCatalogEngineBuilder engine,
        string code,
        string message,
        string suggestion,
        IReadOnlyList<string> evidencePaths)
    {
        engine.SetCommandCatalog("Failed", 0L, string.Empty, Array.Empty<CapabilityCatalogCommand>(), evidencePaths);
        AddIssue(
            string.IsNullOrWhiteSpace(code) ? "CommandCatalogFailed" : code,
            "Error",
            engine.EngineId,
            string.IsNullOrWhiteSpace(message) ? "System/list_commands did not complete successfully." : message,
            suggestion,
            evidencePaths);
    }

    /// <summary>
    /// 解析命令目录根并确保结果是对象。
    /// </summary>
    /// <param name="resultJson">宿主返回的业务 JSON。</param>
    /// <param name="evidencePaths">响应证据。</param>
    /// <returns>命令目录根对象。</returns>
    private static JsonObject ParseCommandRoot(string resultJson, IReadOnlyList<string> evidencePaths)
    {
        if (string.IsNullOrWhiteSpace(resultJson))
        {
            throw CreateCatalogException("CommandCatalogInvalid", "System/list_commands returned an empty result.", "Refresh the engine command catalog.", evidencePaths);
        }

        try
        {
            return JsonNode.Parse(resultJson) as JsonObject
                ?? throw CreateCatalogException("CommandCatalogInvalid", "System/list_commands result must be a JSON object.", "Refresh the engine command catalog.", evidencePaths);
        }
        catch (JsonException exception)
        {
            throw CreateCatalogException("CommandCatalogInvalid", "System/list_commands returned invalid JSON: " + exception.Message, "Inspect the response evidence and retry.", evidencePaths);
        }
    }

    /// <summary>
    /// 校验命令目录内的 engine/session/generation 与命令前后 registry 一致。
    /// </summary>
    /// <param name="root">命令目录根。</param>
    /// <param name="before">命令前 registry。</param>
    /// <param name="after">命令后 registry。</param>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="evidencePaths">响应证据。</param>
    /// <returns>身份可证明时返回 true。</returns>
    private static bool ValidateCommandIdentity(
        JsonObject root,
        EngineRegistryEntry before,
        EngineRegistryEntry? after,
        string engineId,
        IReadOnlyList<string> evidencePaths)
    {
        var responseEngineId = ReadString(root, "engineId");
        var responseSessionId = ReadString(root, "sessionId");
        var responseGeneration = ReadInt64(root, "generation");
        if (string.IsNullOrWhiteSpace(responseSessionId) || responseGeneration <= 0L)
        {
            throw CreateCatalogException("CommandCatalogIdentityInvalid", "System/list_commands omitted a valid sessionId or generation.", "Refresh the engine registry and retry.", evidencePaths);
        }

        return string.Equals(responseEngineId, engineId, StringComparison.Ordinal)
            && after != null
            && !string.IsNullOrWhiteSpace(after.SessionId)
            && after.Generation > 0L
            && string.Equals(before.SessionId, after.SessionId, StringComparison.Ordinal)
            && before.Generation > 0L
            && before.Generation == after.Generation
            && string.Equals(responseSessionId, after.SessionId, StringComparison.Ordinal)
            && responseGeneration == after.Generation;
    }

    /// <summary>
    /// 解析并验证 Kit/action 命令，拒绝重复或不安全标识。
    /// </summary>
    /// <param name="root">命令目录根。</param>
    /// <param name="engineId">来源 engine。</param>
    /// <param name="evidencePaths">响应证据。</param>
    /// <returns>稳定排序后的命令列表。</returns>
    private static IReadOnlyList<CapabilityCatalogCommand> ParseCommands(
        JsonObject root,
        string engineId,
        IReadOnlyList<string> evidencePaths)
    {
        if (root["kits"] is not JsonArray kits)
        {
            throw CreateCatalogException("CommandCatalogInvalid", "System/list_commands result is missing kits.", "Refresh the engine command catalog.", evidencePaths);
        }

        List<CapabilityCatalogCommand> commands = new();
        HashSet<string> identifiers = new(StringComparer.Ordinal);
        foreach (var kitNode in kits)
        {
            if (kitNode is not JsonObject kitObject)
            {
                throw CreateCatalogException("CommandCatalogInvalid", "Command catalog contains a non-object Kit entry.", "Refresh the engine command catalog.", evidencePaths);
            }

            var kit = ReadSafeIdentifier(kitObject, "kit", evidencePaths);
            if (kitObject["actions"] is not JsonArray actions)
            {
                throw CreateCatalogException("CommandCatalogInvalid", $"Command catalog Kit {kit} is missing actions.", "Refresh the engine command catalog.", evidencePaths);
            }

            foreach (var actionNode in actions)
            {
                if (actionNode is not JsonObject actionObject)
                {
                    throw CreateCatalogException("CommandCatalogInvalid", $"Command catalog Kit {kit} contains a non-object action.", "Refresh the engine command catalog.", evidencePaths);
                }

                var action = ReadSafeIdentifier(actionObject, "action", evidencePaths);
                var kind = ReadString(actionObject, "kind");
                if (!IsKnownCommandKind(kind))
                {
                    throw CreateCatalogException("CommandCatalogInvalid", $"Command {kit}/{action} has unknown kind {kind}.", "Update the engine adapter command descriptor.", evidencePaths);
                }

                var key = kit + "/" + action;
                if (!identifiers.Add(key))
                {
                    throw CreateCatalogException("CommandCatalogInvalid", $"Command catalog contains duplicate {key}.", "Remove duplicate command descriptors from the engine adapter.", evidencePaths);
                }

                commands.Add(new CapabilityCatalogCommand(engineId, kit, action, kind));
            }
        }

        return commands
            .OrderBy(command => command.Kit, StringComparer.Ordinal)
            .ThenBy(command => command.Action, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// 比较静态 harness 的 command Kit 声明与实时目录，显式报告漂移而不把 union 标记为可用。
    /// </summary>
    /// <param name="commands">实时命令列表。</param>
    /// <param name="engineId">来源 engine。</param>
    /// <param name="evidencePaths">实时证据。</param>
    private void DetectCommandDrift(
        IReadOnlyList<CapabilityCatalogCommand> commands,
        string engineId,
        IReadOnlyList<string> evidencePaths)
    {
        var observed = commands.Select(command => command.Kit).Distinct(StringComparer.Ordinal).ToArray();
        var expected = mProjectModelTrusted && mProjectModelApplied
            ? mProjectModelCommandKits
            : mDeclaredCommandKits;
        if (observed.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(
                expected.OrderBy(value => value, StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            return;
        }

        mHasDrift = true;
        AddIssue(
            "HarnessCommandCatalogDrift",
            "Warning",
            engineId,
            mProjectModelTrusted
                ? "Project Model command Kit declarations differ from the current System/list_commands result."
                : "Static harness command Kit declarations differ from the current System/list_commands result.",
            mProjectModelTrusted
                ? "Refresh the Project Model from the owning Installer/Editor process before using strict catalog checks."
                : "Regenerate the harness from the owning Installer/Editor process before using strict catalog checks.",
            mProjectModelTrusted
                ? evidencePaths.Concat(new[] { mHarnessPath, mProjectModelPath }).ToArray()
                : evidencePaths.Concat(new[] { mHarnessPath }).ToArray());
    }

    /// <summary>
    /// 读取并校验实时命令响应中的证据路径。
    /// </summary>
    /// <param name="result">命令执行结果。</param>
    /// <returns>去重后的路径。</returns>
    private static IReadOnlyList<string> GetCommandEvidencePaths(CommandExecutionResult result)
    {
        return new[] { result.CommandPath, result.ResponsePath }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 读取并校验安全标识字段。
    /// </summary>
    /// <param name="node">命令对象。</param>
    /// <param name="name">字段名。</param>
    /// <param name="evidencePaths">错误证据。</param>
    /// <returns>安全标识。</returns>
    private static string ReadSafeIdentifier(JsonObject node, string name, IReadOnlyList<string> evidencePaths)
    {
        var value = ReadString(node, name);
        try
        {
            return SafeIdValidator.EnsureSafeId(value, name);
        }
        catch (YokiFrameProtocolException exception)
        {
            throw CreateCatalogException(exception.Error.Code, exception.Error.Message, exception.Error.Suggestion, evidencePaths);
        }
    }

    /// <summary>
    /// 判断命令风险类型是否属于当前能力契约。
    /// </summary>
    /// <param name="kind">命令风险类型。</param>
    /// <returns>已知类型返回 true。</returns>
    private static bool IsKnownCommandKind(string kind)
    {
        return string.Equals(kind, READ_ONLY_KIND, StringComparison.Ordinal)
            || string.Equals(kind, MAINTENANCE_KIND, StringComparison.Ordinal)
            || string.Equals(kind, USER_ACTION_KIND, StringComparison.Ordinal)
            || string.Equals(kind, DANGEROUS_KIND, StringComparison.Ordinal);
    }
}
