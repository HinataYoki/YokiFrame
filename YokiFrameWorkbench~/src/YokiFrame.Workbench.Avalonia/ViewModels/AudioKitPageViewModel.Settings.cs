using YokiFrame.Tooling.Application.Models.AudioKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 AudioKit 索引项目设置加载、投影和保存。</summary>
public sealed partial class AudioKitPageViewModel
{
    /// <summary>加载当前项目配置，损坏配置形成可见诊断并回退默认值。</summary>
    private void LoadIndexSettings()
    {
        AudioIndexSettings settings = AudioIndexSettings.CreateDefault();
        if (string.IsNullOrWhiteSpace(mProjectRoot) || mLoadIndexSettings == null)
        {
            ApplyIndexSettings(settings);
            return;
        }

        try
        {
            settings = mLoadIndexSettings(mProjectRoot);
            SetIndexStatus(IndexStatusKind.None);
        }
        catch (Exception exception)
        {
            SetIndexStatus(IndexStatusKind.LoadFailed, error: exception.Message);
        }
        ApplyIndexSettings(settings);
    }

    /// <summary>把强类型项目配置投影到可编辑字段。</summary>
    private void ApplyIndexSettings(AudioIndexSettings settings)
    {
        ScanFolder = settings.ScanFolder;
        IndexOutputPath = settings.OutputPath;
        IndexManifestPath = settings.ManifestPath;
        IndexNamespace = settings.NamespaceName;
        IndexClassName = settings.ClassName;
        IndexStartId = settings.StartId;
    }

    /// <summary>从当前页面输入创建待保存的完整项目配置。</summary>
    private AudioIndexSettings CreateIndexSettings()
    {
        return new AudioIndexSettings(
            ScanFolder,
            IndexOutputPath,
            IndexManifestPath,
            IndexNamespace,
            IndexClassName,
            checked((int)IndexStartId));
    }

    /// <summary>保存当前项目配置并把失败投影到索引状态栏。</summary>
    /// <param name="showSuccess">成功时是否显示显式保存反馈。</param>
    /// <returns>配置是否已保存，未注入持久化服务时视为可继续。</returns>
    internal Task SaveIndexSettingsAsync()
    {
        return TrySaveIndexSettingsAsync(true);
    }

    /// <summary>
    /// 在 Workbench 关闭前提交当前索引草稿，避免焦点仍停留在输入控件时跳过失焦保存；
    /// 由关闭流程在线程池上异步等待，不阻塞 UI 线程。
    /// </summary>
    internal async Task PersistIndexSettingsOnCloseAsync()
    {
        await TrySaveIndexSettingsAsync(false).ConfigureAwait(false);
    }

    /// <summary>保存当前页面配置并把失败投影到索引状态栏。</summary>
    private async Task<bool> TrySaveIndexSettingsAsync(bool showSuccess)
    {
        if (mSaveIndexSettingsAsync == null) return true;
        try
        {
            await mSaveIndexSettingsAsync(
                mProjectRoot,
                CreateIndexSettings(),
                mLifetimeCancellation.Token);
            if (showSuccess) SetIndexStatus(IndexStatusKind.Saved);
            return true;
        }
        catch (OperationCanceledException) when (mLifetimeCancellation.IsCancellationRequested)
        {
            SetIndexStatus(IndexStatusKind.None);
            return false;
        }
        catch (Exception exception)
        {
            SetIndexStatus(IndexStatusKind.SaveFailed, error: exception.Message);
            return false;
        }
    }
}
