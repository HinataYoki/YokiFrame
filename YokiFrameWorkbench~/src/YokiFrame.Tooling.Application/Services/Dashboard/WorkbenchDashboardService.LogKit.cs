using System.Text.Json.Nodes;
using YokiFrame;
using YokiFrame.Protocol.Common;
using YokiFrame.Protocol.Results;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.LogKit;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>承载 Dashboard 的 LogKit 强类型状态和显式交互用例。</summary>
public sealed partial class WorkbenchDashboardService
{
    private const string LOG_KIT = "LogKit";
    private const string EMPTY_JSON_OBJECT = "{}";
    private const string SET_LOG_KIT_SETTINGS_ACTION = "set_settings";
    private const string CLEAR_LOG_KIT_HISTORY_ACTION = "clear_history";
    private const string READ_LOG_KIT_FILE_ACTION = "read_log_file";

    /// <summary>读取当前项目的可持久化 LogKit 设置；Godot 返回有效设置只读投影。</summary>
    /// <param name="engineId">目标 engine；为空时只自动选择唯一在线 engine。</param>
    /// <returns>项目设置、指纹和持久化能力。</returns>
    public WorkbenchLogKitProjectSettings LoadLogKitProjectSettings(string engineId)
    {
        var requestedEngineId = engineId ?? string.Empty;
        var projectEngine = DetectProjectEngine(requestedEngineId);
        if (!string.Equals(projectEngine, "Unity", StringComparison.OrdinalIgnoreCase))
        {
            return CreateReadOnlyProjectSettings(
                requestedEngineId,
                projectEngine,
                string.Equals(projectEngine, "Godot", StringComparison.OrdinalIgnoreCase)
                    ? "Godot LogKit settings are read-only in this release."
                    : "The current project type could not be confirmed.",
                ReadEffectiveLogKitSettings(requestedEngineId));
        }

        return mLogKitRuntimeSettingsService.LoadUnitySettings(requestedEngineId);
    }

    /// <summary>先原子保存 Unity 项目设置，再把同一份设置应用到当前 Runtime。</summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="settings">完整 LogKit 设置。</param>
    /// <param name="fingerprint">页面加载时的项目文件指纹。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>项目保存与当前会话应用的独立结果。</returns>
    public async Task<WorkbenchLogKitSettingsSaveResult> SaveLogKitSettingsAsync(
        string engineId,
        WorkbenchLogKitSettings settings,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        var projectSettings = LoadLogKitProjectSettings(engineId);
        if (!projectSettings.CanPersist)
        {
            return new WorkbenchLogKitSettingsSaveResult(
                false, false, false, projectSettings, null, projectSettings.StatusMessage);
        }

        var saved = await mLogKitRuntimeSettingsService.SaveUnitySettingsAsync(
            engineId ?? string.Empty,
            settings,
            fingerprint,
            cancellationToken).ConfigureAwait(false);
        if (!saved.ProjectSaved)
        {
            return saved;
        }

        if (!TryResolveRuntimeApplyTarget(engineId ?? string.Empty, out var selectedEngineId, out var applyError))
        {
            return saved with
            {
                ErrorMessage = "Project settings were saved, but Runtime was not applied: " + applyError
            };
        }

        return await ApplySavedLogKitSettingsAsync(selectedEngineId, settings, saved, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>清空内存历史并直接返回 Provider 的原子完整 state。</summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>清空后的强类型 LogKit state。</returns>
    public async Task<WorkbenchLogKitState> ClearLogKitHistoryAsync(
        string engineId,
        CancellationToken cancellationToken)
    {
        return await ExecuteLogKitStateCommandAsync(
            engineId,
            CLEAR_LOG_KIT_HISTORY_ACTION,
            EMPTY_JSON_OBJECT,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>按用户显式选择读取 Editor 或 Player 文件尾部预览。</summary>
    /// <param name="engineId">目标 engine。</param>
    /// <param name="kind">仅允许 editor 或 player。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>有界文件预览和实际传输证据。</returns>
    public async Task<WorkbenchLogKitFilePreview> ReadLogKitFileAsync(
        string engineId,
        string kind,
        CancellationToken cancellationToken)
    {
        var normalizedKind = NormalizeFileKind(kind);
        var selectedEngineId = mEngineSelectionService.Resolve(engineId, DateTimeOffset.UtcNow);
        var before = FindEngineRegistry(selectedEngineId);
        string payloadJson = new JsonObject { ["kind"] = normalizedKind }
            .ToJsonString(YokiFrameJson.CompactOptions);
        var result = await ExecuteLogKitCommandAsync(
            selectedEngineId,
            READ_LOG_KIT_FILE_ACTION,
            payloadJson,
            cancellationToken).ConfigureAwait(false);
        _ = EnsureSameLogKitHost(before, FindEngineRegistry(selectedEngineId), result);
        return WorkbenchLogKitFilePreviewParser.Parse(
            result.Response.ResultJson,
            normalizedKind,
            result.Transport,
            CreateCommandEvidencePaths(result));
    }

    /// <summary>应用已保存设置，并保留“已保存但当前会话未应用”的真实结果。</summary>
    private async Task<WorkbenchLogKitSettingsSaveResult> ApplySavedLogKitSettingsAsync(
        string engineId,
        WorkbenchLogKitSettings settings,
        WorkbenchLogKitSettingsSaveResult saved,
        CancellationToken cancellationToken)
    {
        try
        {
            var state = await ExecuteLogKitStateCommandAsync(
                engineId,
                SET_LOG_KIT_SETTINGS_ACTION,
                WorkbenchLogKitSettingsJson.CreateCommandPayload(settings),
                cancellationToken).ConfigureAwait(false);
            return saved with { RuntimeApplied = true, AppliedState = state };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return saved with { ErrorMessage = "Project settings were saved, but Runtime apply failed: " + exception.Message };
        }
    }

    /// <summary>执行返回完整 LogKit state 的命令并验证宿主身份。</summary>
    private async Task<WorkbenchLogKitState> ExecuteLogKitStateCommandAsync(
        string engineId,
        string action,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var selectedEngineId = mEngineSelectionService.Resolve(engineId, DateTimeOffset.UtcNow);
        var before = FindEngineRegistry(selectedEngineId);
        var result = await ExecuteLogKitCommandAsync(
            selectedEngineId,
            action,
            payloadJson,
            cancellationToken).ConfigureAwait(false);
        var after = FindEngineRegistry(selectedEngineId);
        var current = EnsureSameLogKitHost(before, after, result);
        return WorkbenchLogKitStateParser.Parse(CreateLogKitCommandDataSource(selectedEngineId, current, result));
    }

    /// <summary>通过共享命令用例执行 LogKit action 并校验 terminal response。</summary>
    private async Task<CommandExecutionResult> ExecuteLogKitCommandAsync(
        string engineId,
        string action,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var result = await mCommandExecutionService.ExecuteAsync(
            engineId,
            LOG_KIT,
            action,
            payloadJson,
            WORKBENCH_SOURCE,
            COMMAND_TIMEOUT_MS,
            cancellationToken).ConfigureAwait(false);
        EnsureSuccessfulLogKitCommand(result);
        return result;
    }

    /// <summary>创建携带实际传输和宿主身份的命令数据源。</summary>
    private static WorkbenchLogKitDataSource CreateLogKitCommandDataSource(
        string engineId,
        Protocol.FileBridge.EngineRegistryEntry registry,
        CommandExecutionResult result)
    {
        var updatedAtUtc = DateTimeOffset.TryParse(result.Response.CompletedAtUtc, out var completedAt)
            ? completedAt.ToUniversalTime()
            : DateTimeOffset.MinValue;
        return new WorkbenchLogKitDataSource(
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

    /// <summary>验证命令完成前后仍属于同一 Runtime 会话，否则拒绝解析旧回包。</summary>
    private static Protocol.FileBridge.EngineRegistryEntry EnsureSameLogKitHost(
        Protocol.FileBridge.EngineRegistryEntry? before,
        Protocol.FileBridge.EngineRegistryEntry? after,
        CommandExecutionResult result)
    {
        if (IsSameHost(before, after))
        {
            return after!;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            "LogKitCommandIdentityChanged",
            "LogKit command result was rejected because the host session or generation changed or could not be confirmed.",
            "Refresh LogKit state and retry against the current Runtime session.",
            CreateCommandEvidencePaths(result)));
    }

    /// <summary>验证 Runtime 返回成功 terminal response。</summary>
    private static void EnsureSuccessfulLogKitCommand(CommandExecutionResult result)
    {
        if (string.Equals(result.Response.Status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new YokiFrameProtocolException(new YokiFrameError(
            result.Response.ErrorCode,
            result.Response.ErrorMessage,
            "Refresh LogKit state and retry.",
            CreateCommandEvidencePaths(result)));
    }

    /// <summary>验证 registry 属于当前 Workbench 项目。</summary>
    private string ValidateProjectIdentity(Protocol.FileBridge.EngineRegistryEntry? registry)
    {
        if (registry == null || string.IsNullOrWhiteSpace(registry.ProjectPath))
        {
            return "The selected engine does not provide a project identity.";
        }

        try
        {
            return PathsEqual(registry.ProjectPath, mClient.Paths.ProjectRoot)
                ? string.Empty
                : "The selected engine belongs to another project.";
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return "The selected engine project path is invalid.";
        }
    }

    /// <summary>验证当前 registry 是同项目 Unity Host。</summary>
    private string ValidateWritableUnityRegistry(Protocol.FileBridge.EngineRegistryEntry? registry)
    {
        var identityError = ValidateProjectIdentity(registry);
        if (!string.IsNullOrWhiteSpace(identityError))
        {
            return identityError;
        }

        return string.Equals(registry!.Engine, "Unity", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : "Godot LogKit settings are read-only in this release.";
    }

    /// <summary>按项目文件和同项目 registry 识别当前宿主类型，不要求 Host 在线。</summary>
    private string DetectProjectEngine(string engineId)
    {
        Protocol.FileBridge.EngineRegistryEntry? registry = null;
        try
        {
            registry = string.IsNullOrWhiteSpace(engineId) ? null : FindEngineRegistry(engineId);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            registry = null;
        }
        if (string.IsNullOrWhiteSpace(ValidateProjectIdentity(registry)))
        {
            return registry!.Engine;
        }

        var projectRoot = mClient.Paths.ProjectRoot;
        if (File.Exists(Path.Combine(projectRoot, "project.godot")))
        {
            return "Godot";
        }

        return Directory.Exists(Path.Combine(projectRoot, "Assets")) ? "Unity" : string.Empty;
    }

    /// <summary>解析可即时应用的同项目 Unity Host；失败只影响第二阶段。</summary>
    private bool TryResolveRuntimeApplyTarget(
        string requestedEngineId,
        out string selectedEngineId,
        out string errorMessage)
    {
        IReadOnlyList<Protocol.FileBridge.EngineRegistryEntry> registries;
        try
        {
            registries = mClient.ReadEngineEntries();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            selectedEngineId = string.Empty;
            errorMessage = "Engine registry could not be read: " + exception.Message;
            return false;
        }
        var registry = registries.FirstOrDefault(entry => string.Equals(
            entry.EngineId,
            requestedEngineId,
            StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(requestedEngineId))
        {
            var candidates = registries
                .Where(entry => string.IsNullOrWhiteSpace(ValidateWritableUnityRegistry(entry)))
                .ToArray();
            registry = candidates.Length == 1 ? candidates[0] : null;
            if (candidates.Length > 1)
            {
                selectedEngineId = string.Empty;
                errorMessage = "Multiple Unity hosts are available; select one before applying settings.";
                return false;
            }
        }

        errorMessage = ValidateWritableUnityRegistry(registry);
        selectedEngineId = string.IsNullOrWhiteSpace(errorMessage) ? registry!.EngineId : string.Empty;
        return string.IsNullOrWhiteSpace(errorMessage);
    }

    /// <summary>创建不允许持久写入的项目设置投影。</summary>
    private WorkbenchLogKitProjectSettings CreateReadOnlyProjectSettings(
        string engineId,
        string? engine,
        string message,
        WorkbenchLogKitSettings? effectiveSettings = null)
    {
        return new WorkbenchLogKitProjectSettings(
            engineId,
            engine ?? string.Empty,
            false,
            false,
            string.Empty,
            string.Empty,
            effectiveSettings ?? WorkbenchLogKitSettings.CreateDefault(),
            message);
    }

    /// <summary>为只读宿主读取当前有效设置；断线时安全回退 Core 默认值。</summary>
    private WorkbenchLogKitSettings ReadEffectiveLogKitSettings(string engineId)
    {
        if (string.IsNullOrWhiteSpace(engineId))
        {
            return WorkbenchLogKitSettings.CreateDefault();
        }

        try
        {
            return LoadDashboard(engineId).LogKitState?.Settings
                ?? WorkbenchLogKitSettings.CreateDefault();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return WorkbenchLogKitSettings.CreateDefault();
        }
    }

    /// <summary>判断两条 registry 是否属于同一宿主会话。</summary>
    private static bool IsSameHost(
        Protocol.FileBridge.EngineRegistryEntry? before,
        Protocol.FileBridge.EngineRegistryEntry? after)
    {
        return before != null
            && after != null
            && before.Generation > 0L
            && before.Generation == after.Generation
            && !string.IsNullOrWhiteSpace(before.SessionId)
            && string.Equals(before.SessionId, after.SessionId, StringComparison.Ordinal);
    }

    /// <summary>比较规范化项目路径。</summary>
    private static bool PathsEqual(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(
            Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            comparison);
    }

    /// <summary>验证文件来源选项。</summary>
    private static string NormalizeFileKind(string kind)
    {
        if (string.Equals(kind, "editor", StringComparison.OrdinalIgnoreCase))
        {
            return "editor";
        }

        if (string.Equals(kind, "player", StringComparison.OrdinalIgnoreCase))
        {
            return "player";
        }

        throw new ArgumentException("LogKit file kind must be editor or player.", nameof(kind));
    }
}
