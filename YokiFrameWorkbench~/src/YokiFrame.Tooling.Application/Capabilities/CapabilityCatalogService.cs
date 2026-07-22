using System.Text.Json;
using YokiFrame.Client;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Validation;
using YokiFrame.Tooling.Application.Engines;
using YokiFrame.Tooling.Application.Models.Capabilities;
using YokiFrame.Tooling.Application.Models.ProjectModel;
using YokiFrame.Tooling.Application.ProjectModel;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Capabilities;

/// <summary>
/// 聚合 Project Model、静态 harness 回退、engine registry、heartbeat 和可选实时命令目录。
/// </summary>
public sealed class CapabilityCatalogService
{
    private const string DEFAULT_SOURCE = YokiFrame.YokiFrameCommandSourceContract.WORKBENCH;
    private const int DEFAULT_TIMEOUT_MS = 10000;
    private readonly IYokiFrameClient mClient;
    private readonly EngineSelectionService mEngineSelectionService;
    private readonly TimeProvider mTimeProvider;

    /// <summary>
    /// 使用系统时间创建能力目录服务。
    /// </summary>
    /// <param name="client">统一 YokiFrame Client。</param>
    public CapabilityCatalogService(IYokiFrameClient client)
        : this(client, TimeProvider.System)
    {
    }

    /// <summary>
    /// 创建可注入时间源的能力目录服务，便于验证 freshness 和身份变更。
    /// </summary>
    /// <param name="client">统一 YokiFrame Client。</param>
    /// <param name="timeProvider">当前时间源。</param>
    public CapabilityCatalogService(IYokiFrameClient client, TimeProvider timeProvider)
    {
        mClient = client ?? throw new ArgumentNullException(nameof(client));
        mEngineSelectionService = new EngineSelectionService(client);
        mTimeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// 读取并聚合当前能力事实；只有显式 refreshCommands 时才发送 System/list_commands。
    /// </summary>
    /// <param name="requestedEngineId">可选目标 engine；为空时读取全部已注册 engine，刷新命令时要求唯一在线 engine。</param>
    /// <param name="refreshCommands">是否显式请求实时命令目录。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">实时命令最大等待毫秒数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>结构化能力目录和状态。</returns>
    public async Task<CapabilityCatalogResult> BuildAsync(
        string requestedEngineId,
        bool refreshCommands,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var nowUtc = mTimeProvider.GetUtcNow();
        var builder = new CapabilityCatalogBuilder(mClient.Paths.ProjectRoot, mClient.Paths.GetHarnessCapabilitiesPath(), nowUtc);
        builder.ApplyProjectModel(InspectProjectModel());
        ApplyHarness(builder);

        var entries = ReadEngineEntries(builder);
        entries = FilterValidEntries(builder, entries);
        var canRefreshCommands = refreshCommands;
        var selectionFailed = false;
        if (refreshCommands)
        {
            try
            {
                var selectedEngineId = mEngineSelectionService.Resolve(requestedEngineId, entries, nowUtc);
                entries = entries
                    .Where(entry => string.Equals(entry.EngineId, selectedEngineId, StringComparison.Ordinal))
                    .ToArray();
            }
            catch (YokiFrameProtocolException exception)
            {
                canRefreshCommands = false;
                selectionFailed = true;
                builder.AddIssue(
                    exception.Error.Code,
                    string.Equals(exception.Error.Code, "InvalidSafeId", StringComparison.Ordinal) ? "Error" : "Warning",
                    "project",
                    exception.Error.Message,
                    exception.Error.Suggestion,
                    exception.Error.EvidencePaths);
                if (!string.IsNullOrWhiteSpace(requestedEngineId))
                {
                    entries = entries
                        .Where(entry => string.Equals(entry.EngineId, requestedEngineId, StringComparison.Ordinal))
                        .ToArray();
                }
            }
            catch (JsonException exception)
            {
                canRefreshCommands = false;
                selectionFailed = true;
                builder.AddIssue(
                    "EngineSelectionInvalid",
                    "Error",
                    "project",
                    "Engine identity evidence is invalid: " + exception.Message,
                    "Repair the engine registry or heartbeat, then retry the catalog refresh.",
                    new[] { mClient.Paths.EnginesRoot });
            }
        }
        else if (!string.IsNullOrWhiteSpace(requestedEngineId))
        {
            entries = entries
                .Where(entry => string.Equals(entry.EngineId, requestedEngineId, StringComparison.Ordinal))
                .ToArray();
        }

        if (entries.Count == 0 && !selectionFailed)
        {
            builder.AddIssue(
                "EngineUnavailable",
                "Warning",
                "project",
                "No matching YokiFrame engine registry entry was found.",
                "Start the engine adapter or pass an existing --engine identifier.",
                Array.Empty<string>());
        }

        foreach (var entry in entries)
        {
            var heartbeat = ReadHeartbeat(builder, entry.EngineId);
            var engine = builder.AddEngine(entry, heartbeat);
            if (canRefreshCommands)
            {
                await RefreshCommandCatalogAsync(
                    builder,
                    engine,
                    entry,
                    source,
                    timeoutMs <= 0 ? DEFAULT_TIMEOUT_MS : timeoutMs,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return builder.Build();
    }

    /// <summary>
    /// 读取并验证 Project Model；意外异常也必须降级为可审计的 Blocked 结果。
    /// </summary>
    /// <returns>Project Model 结构化检查结果。</returns>
    private ProjectModelResult InspectProjectModel()
    {
        try
        {
            return new ProjectModelService(mClient, mTimeProvider).Inspect();
        }
        catch (Exception exception)
        {
            return new ProjectModelResult(
                "Blocked",
                false,
                null,
                new[]
                {
                    new ProjectModelIssue(
                        "ProjectModelInspectFailed",
                        "Error",
                        "Project Model inspection failed: " + exception.Message,
                        "Inspect the Project Model evidence and regenerate the bundle.",
                        new[] { mClient.Paths.ProjectModelManifestPath })
                },
                new[] { mClient.Paths.ProjectModelManifestPath });
        }
    }

    /// <summary>
    /// 读取静态 harness；缺失或损坏只形成目录问题，不阻断对实时 engine 的观察。
    /// </summary>
    /// <param name="builder">能力目录构建器。</param>
    private void ApplyHarness(CapabilityCatalogBuilder builder)
    {
        try
        {
            builder.ApplyHarness(mClient.ReadHarnessCapabilities());
        }
        catch (YokiFrameProtocolException exception)
        {
            builder.AddIssue(
                exception.Error.Code,
                "Warning",
                "harness",
                exception.Error.Message,
                exception.Error.Suggestion,
                exception.Error.EvidencePaths);
        }
    }

    /// <summary>
    /// 读取 engine registry，并把损坏条目转换成可定位的目录问题。
    /// </summary>
    /// <param name="builder">能力目录构建器。</param>
    /// <returns>可用于本次聚合的 registry 条目。</returns>
    private IReadOnlyList<EngineRegistryEntry> ReadEngineEntries(CapabilityCatalogBuilder builder)
    {
        try
        {
            return mClient.ReadEngineEntries();
        }
        catch (YokiFrameProtocolException exception)
        {
            builder.AddIssue(
                exception.Error.Code,
                "Error",
                "project",
                exception.Error.Message,
                exception.Error.Suggestion,
                exception.Error.EvidencePaths);
            return Array.Empty<EngineRegistryEntry>();
        }
        catch (JsonException exception)
        {
            builder.AddIssue(
                "EngineRegistryInvalid",
                "Error",
                "project",
                "Engine registry JSON is invalid: " + exception.Message,
                "Repair or remove the invalid engine registry, then refresh the catalog.",
                new[] { Path.Combine(builder.ProjectRoot, ".yokiframe", "engines") });
            return Array.Empty<EngineRegistryEntry>();
        }
        catch (EngineRegistryReadException exception)
        {
            builder.AddIssue(
                "EngineRegistryInvalid",
                "Warning",
                "project",
                exception.Message,
                "Repair the invalid registry files; healthy engine entries remain available for inspection.",
                exception.InvalidPaths);
            return exception.ValidEntries;
        }
    }

    /// <summary>
    /// 在进入路径和命令操作前验证 registry 中的 engine 标识，避免无效文本进入能力目录或路径解析器。
    /// </summary>
    /// <param name="builder">能力目录构建器。</param>
    /// <param name="entries">原始 registry 条目。</param>
    /// <returns>标识合法的 registry 条目。</returns>
    private static IReadOnlyList<EngineRegistryEntry> FilterValidEntries(
        CapabilityCatalogBuilder builder,
        IReadOnlyList<EngineRegistryEntry> entries)
    {
        List<EngineRegistryEntry> validEntries = new();
        foreach (var entry in entries)
        {
            try
            {
                SafeIdValidator.EnsureSafeId(entry.EngineId, nameof(entry.EngineId));
                validEntries.Add(entry);
            }
            catch (YokiFrameProtocolException exception)
            {
                builder.AddIssue(
                    exception.Error.Code,
                    "Error",
                    "project",
                    exception.Error.Message,
                    exception.Error.Suggestion,
                    new[] { Path.Combine(builder.ProjectRoot, ".yokiframe", "engines") });
            }
        }

        return validEntries;
    }

    /// <summary>
    /// 读取单个 engine heartbeat；失效文件由 Builder 记录并保留 registry 事实。
    /// </summary>
    /// <param name="builder">能力目录构建器。</param>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>heartbeat 或 null。</returns>
    private HeartbeatInfo? ReadHeartbeat(CapabilityCatalogBuilder builder, string engineId)
    {
        try
        {
            return mClient.ReadHeartbeat(engineId);
        }
        catch (YokiFrameProtocolException exception)
        {
            builder.AddIssue(
                exception.Error.Code,
                "Warning",
                engineId,
                exception.Error.Message,
                exception.Error.Suggestion,
                exception.Error.EvidencePaths);
            return null;
        }
        catch (JsonException exception)
        {
            builder.AddIssue(
                "HeartbeatInvalid",
                "Warning",
                engineId,
                "Engine heartbeat JSON is invalid: " + exception.Message,
                "Repair the heartbeat file or restart the engine adapter, then refresh the catalog.",
                new[] { mClient.Paths.GetHeartbeatPath(engineId) });
            return null;
        }
    }

    /// <summary>
    /// 显式刷新命令目录，并在命令前后校验宿主 session/generation 身份。
    /// </summary>
    /// <param name="builder">能力目录构建器。</param>
    /// <param name="engine">已创建的 engine 目录节点。</param>
    /// <param name="before">命令发送前的 registry 条目。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">命令最大等待毫秒数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private async Task RefreshCommandCatalogAsync(
        CapabilityCatalogBuilder builder,
        CapabilityCatalogBuilder.CapabilityCatalogEngineBuilder engine,
        EngineRegistryEntry before,
        string source,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await new CommandExecutionService(mClient).ExecuteAsync(
                before.EngineId,
                "System",
                "list_commands",
                "{}",
                string.IsNullOrWhiteSpace(source) ? DEFAULT_SOURCE : source,
                timeoutMs,
                cancellationToken).ConfigureAwait(false);
            var after = FindEngine(before.EngineId);
            var afterHeartbeat = ReadHeartbeat(builder, before.EngineId);
            builder.ApplyCommandCatalog(engine, before, after, afterHeartbeat, result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (YokiFrameProtocolException exception)
        {
            builder.AddCommandFailure(engine, exception.Error.Code, exception.Error.Message, exception.Error.Suggestion, exception.Error.EvidencePaths);
        }
        catch (JsonException exception)
        {
            builder.AddCommandFailure(
                engine,
                "EngineRegistryInvalid",
                "Engine registry JSON became invalid while refreshing the command catalog: " + exception.Message,
                "Repair or restart the engine adapter, then retry the catalog refresh.",
                new[] { Path.Combine(mClient.Paths.GetEngineRoot(before.EngineId), YokiFrameFileBridgeLayout.ENGINE_REGISTRY_FILE_NAME) });
        }
    }

    /// <summary>
    /// 在命令完成后重新读取 registry，避免把旧会话的目录当作当前事实。
    /// </summary>
    /// <param name="engineId">目标 engine。</param>
    /// <returns>最新 registry 条目；不存在时为空。</returns>
    private EngineRegistryEntry? FindEngine(string engineId)
    {
        return mClient.ReadEngineEntries()
            .FirstOrDefault(entry => string.Equals(entry.EngineId, engineId, StringComparison.Ordinal));
    }
}
