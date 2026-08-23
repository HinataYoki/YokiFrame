using YokiFrame.Tooling.Application.Models.AudioKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 AudioKit 稳定索引命令和结果投影。</summary>
public sealed partial class AudioKitPageViewModel
{
    /// <summary>获取音频索引只读扫描命令。</summary>
    public AsyncRelayCommand ScanIndexCommand { get; private set; } = null!;
    /// <summary>获取音频索引原子生成命令。</summary>
    public AsyncRelayCommand GenerateIndexCommand { get; private set; } = null!;

    /// <summary>创建页面仅有的两个索引命令。</summary>
    private void CreateCommands()
    {
        ScanIndexCommand = new AsyncRelayCommand(ScanIndexAsync, CanScanIndex);
        GenerateIndexCommand = new AsyncRelayCommand(GenerateIndexAsync, CanGenerateIndex);
    }

    /// <summary>只读扫描当前项目音频索引。</summary>
    private Task ScanIndexAsync() => ExecuteIndexAsync(mScanIndexAsync!, false);

    /// <summary>原子生成当前项目音频索引和 manifest。</summary>
    private Task GenerateIndexAsync() => ExecuteIndexAsync(mGenerateIndexAsync!, true);

    /// <summary>执行索引服务并替换有界页面预览。</summary>
    private async Task ExecuteIndexAsync(
        Func<AudioIndexRequest, CancellationToken, Task<AudioIndexResult>> operation,
        bool generated)
    {
        if (!await TrySaveIndexSettingsAsync(false)) return;
        SetIndexStatus(generated ? IndexStatusKind.Generating : IndexStatusKind.Scanning);
        try
        {
            AudioIndexResult result = await operation(CreateIndexRequest(), mLifetimeCancellation.Token);
            IndexEntries.Clear();
            for (var index = 0; index < result.Entries.Count; index++) IndexEntries.Add(result.Entries[index]);
            OnPropertyChanged(nameof(IsIndexEmpty));
            SetIndexStatus(
                generated ? IndexStatusKind.Generated : IndexStatusKind.Scanned,
                result.Entries.Count);
            SetIndexEmpty(result.Entries.Count == 0 ? IndexEmptyKind.NoEntries : IndexEmptyKind.None);
        }
        catch (OperationCanceledException) when (mLifetimeCancellation.IsCancellationRequested)
        {
            SetIndexStatus(IndexStatusKind.None);
        }
        catch (Exception exception)
        {
            SetIndexStatus(IndexStatusKind.Error, error: exception.Message);
            SetIndexEmpty(IndexEmptyKind.ScanFailed);
        }
    }

    /// <summary>从页面配置创建强类型索引请求。</summary>
    private AudioIndexRequest CreateIndexRequest()
    {
        return new AudioIndexRequest(
            mProjectRoot, ScanFolder, IndexOutputPath, IndexManifestPath,
            IndexNamespace, IndexClassName, checked((int)IndexStartId));
    }

    /// <summary>判断当前项目是否可执行索引扫描。</summary>
    private bool CanScanIndex() => !string.IsNullOrWhiteSpace(mProjectRoot) && mScanIndexAsync != null;
    /// <summary>判断当前项目是否可生成索引。</summary>
    private bool CanGenerateIndex() => !string.IsNullOrWhiteSpace(mProjectRoot) && mGenerateIndexAsync != null;

    /// <summary>通知索引命令重新计算可用状态。</summary>
    private void RaiseIndexCommands()
    {
        ScanIndexCommand.RaiseCanExecuteChanged();
        GenerateIndexCommand.RaiseCanExecuteChanged();
    }
}
