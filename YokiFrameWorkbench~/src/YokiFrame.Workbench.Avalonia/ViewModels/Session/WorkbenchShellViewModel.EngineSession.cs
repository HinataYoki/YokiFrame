using YokiFrame.Tooling.Application.Models;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>
/// 负责 Workbench Shell 的 engine selector 与每个会话一次的命令目录初始化。
/// </summary>
public sealed partial class WorkbenchShellViewModel
{
    private static readonly TimeSpan CommandCatalogRetryDelay = TimeSpan.FromMilliseconds(500);
    private const int MAX_COMMAND_CATALOG_RETRIES = 3;
    private HostIdentity? mCommandCatalogIdentity;
    private HostIdentity? mCommandCatalogInFlightIdentity;
    private int mCommandCatalogRetryCount;

    /// <summary>
    /// 设置当前 engine，并在用户主动切换时回调窗口刷新 dashboard。
    /// </summary>
    /// <param name="engineId">新选中的 engine 标识。</param>
    private void SetSelectedEngine(string engineId)
    {
        var normalizedEngineId = engineId ?? string.Empty;
        if (!SetProperty(ref mSelectedEngineId, normalizedEngineId))
        {
            return;
        }

        if (!mIsUpdatingEngines && !string.IsNullOrWhiteSpace(normalizedEngineId))
        {
            mEngineChanged(normalizedEngineId);
        }
    }

    /// <summary>
    /// 根据 dashboard 中的真实 registry 刷新 selector，并排除空标识和重复项。
    /// </summary>
    /// <param name="state">dashboard 状态。</param>
    private void UpdateEngineSelector(WorkbenchDashboardState state)
    {
        mIsUpdatingEngines = true;
        var engineIds = state.Engines
            .Select(static engine => engine.EngineId)
            .Where(static engineId => !string.IsNullOrWhiteSpace(engineId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (!string.IsNullOrWhiteSpace(state.SelectedEngineId)
            && !engineIds.Contains(state.SelectedEngineId, StringComparer.Ordinal))
        {
            engineIds.Insert(0, state.SelectedEngineId);
        }

        EngineIds = engineIds;
        SelectedEngineId = state.SelectedEngineId;
        mIsUpdatingEngines = false;
    }

    /// <summary>
    /// 在 dashboard 确认有效选择后为每个新 engine 自动读取一次命令目录。
    /// </summary>
    /// <param name="state">已应用到 Shell 的 dashboard 状态。</param>
    private void RefreshCommandCatalogForSelectedEngine(WorkbenchDashboardState state)
    {
        var engineId = state.SelectedEngineId;
        if (string.IsNullOrWhiteSpace(engineId))
        {
            mCommandCatalogIdentity = null;
            return;
        }

        var currentIdentity = state.CurrentHostIdentity;
        if (currentIdentity == null)
        {
            mCommandCatalogIdentity = null;
            return;
        }

        if (mCommandCatalogIdentity == currentIdentity
            || mCommandCatalogInFlightIdentity == currentIdentity)
        {
            return;
        }

        mCommandCatalogInFlightIdentity = currentIdentity;
        mCommandCatalogRetryCount = 0;
        _ = RequestCommandCatalogAsync(currentIdentity);
    }

    /// <summary>
    /// 请求当前宿主的命令目录；失败时只对同一 session/generation 做有界退避重试。
    /// </summary>
    /// <param name="identity">请求开始时捕获的宿主身份。</param>
    private async Task RequestCommandCatalogAsync(HostIdentity identity)
    {
        try
        {
            await mCommandRequested("System", "list_commands").ConfigureAwait(false);
            if (mCommandCatalogInFlightIdentity == identity)
            {
                mCommandCatalogIdentity = identity;
                mCommandCatalogRetryCount = 0;
            }
        }
        catch (OperationCanceledException)
        {
            // Window 关闭或宿主代次切换时不再重试，调用方的生命周期负责取消。
        }
        catch
        {
            if (mCommandCatalogInFlightIdentity != identity
                || mCommandCatalogRetryCount >= MAX_COMMAND_CATALOG_RETRIES)
            {
                return;
            }

            mCommandCatalogRetryCount++;
            await Task.Delay(CommandCatalogRetryDelay).ConfigureAwait(false);
            if (mCommandCatalogInFlightIdentity == identity)
            {
                await RequestCommandCatalogAsync(identity).ConfigureAwait(false);
            }
        }
        finally
        {
            if (mCommandCatalogInFlightIdentity == identity
                && mCommandCatalogIdentity != identity)
            {
                mCommandCatalogInFlightIdentity = null;
            }
        }
    }
}
