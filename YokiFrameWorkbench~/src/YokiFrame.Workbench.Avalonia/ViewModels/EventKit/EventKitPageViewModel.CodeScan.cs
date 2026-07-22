using YokiFrame.Tooling.Application.Models.EventKit.Scan;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 EventKit 页面低频静态扫描、扫描状态和源码定位交互。</summary>
public sealed partial class EventKitPageViewModel : IDisposable
{
    private Func<bool, CancellationToken, Task<WorkbenchEventKitCodeScan>>? mScanAsync;
    private CancellationTokenSource mScanCancellation = new();
    private string mProjectRoot = string.Empty;
    private string mScanStatusText = "等待进入页面";
    private bool mExcludeEditor = true;
    private bool mIsPageActive;
    private bool mIsScanning;
    private long mScanGeneration;
    private int mScannedFileCount;
    private int mMatchedFileCount;

    /// <summary>获取或设置是否排除任意 Editor 目录。</summary>
    public bool ExcludeEditor
    {
        get => mExcludeEditor;
        set
        {
            if (SetProperty(ref mExcludeEditor, value))
            {
                if (mIsPageActive)
                {
                    _ = ScanCodeAsync();
                }
            }
        }
    }

    /// <summary>获取是否正在后台扫描。</summary>
    public bool IsScanning { get => mIsScanning; private set => SetProperty(ref mIsScanning, value); }
    /// <summary>获取扫描状态文本。</summary>
    public string ScanStatusText { get => mScanStatusText; private set => SetProperty(ref mScanStatusText, value); }
    /// <summary>获取扫描项目根。</summary>
    public string ProjectRoot { get => mProjectRoot; private set => SetProperty(ref mProjectRoot, value); }
    /// <summary>获取扫描文件统计。</summary>
    public string ScanFileCountText => mMatchedFileCount + " matched / " + mScannedFileCount + " files";

    /// <summary>初始化可选扫描边界，设计时页面可不提供扫描实现。</summary>
    private void InitializeCodeScan(
        Func<bool, CancellationToken, Task<WorkbenchEventKitCodeScan>>? scanAsync)
    {
        mScanAsync = scanAsync;
    }

    /// <summary>更新当前项目根；切换项目时取消旧扫描并清空静态关系。</summary>
    internal void SetProjectRoot(string projectRoot)
    {
        string normalized = string.IsNullOrWhiteSpace(projectRoot)
            ? string.Empty
            : Path.GetFullPath(projectRoot);
        if (string.Equals(ProjectRoot, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CancelCurrentScan();
        ProjectRoot = normalized;
        mCodeRelationsByIdentity.Clear();
        ReconcileEventItems(mRuntimeEventsByIdentity.Values.ToArray());
        ReconcileVisibleEvents();
        RestoreSelection(SelectedEvent?.Identity ?? string.Empty);
        ScanStatusText = string.IsNullOrEmpty(normalized) ? "等待项目" : "等待进入页面";
        NotifyScanProperties();
        if (mIsPageActive)
        {
            _ = ScanCodeAsync();
        }
    }

    /// <summary>同步页面激活状态；每次重新进入自动扫描，周期刷新不会重复启动扫描。</summary>
    internal void SetPageActive(bool isActive)
    {
        if (mIsPageActive == isActive)
        {
            return;
        }

        mIsPageActive = isActive;
        if (isActive)
        {
            _ = ScanCodeAsync();
            return;
        }

        CancelCurrentScan();
        IsScanning = false;
        ScanStatusText = string.IsNullOrEmpty(ProjectRoot) ? "等待项目" : "等待进入页面";
        NotifyScanProperties();
    }

    /// <summary>执行当前页面的一次静态扫描，并以请求代次隔离过期结果。</summary>
    private async Task ScanCodeAsync()
    {
        if (mScanAsync == null || string.IsNullOrWhiteSpace(ProjectRoot))
        {
            ScanStatusText = string.IsNullOrWhiteSpace(ProjectRoot) ? "等待项目" : "扫描不可用";
            return;
        }

        CancelCurrentScan();
        long scanGeneration = mScanGeneration;
        CancellationToken token = mScanCancellation.Token;
        IsScanning = true;
        ScanStatusText = "正在扫描 C# 调用点";
        try
        {
            WorkbenchEventKitCodeScan scan = await mScanAsync(ExcludeEditor, token);
            if (token.IsCancellationRequested || scanGeneration != mScanGeneration)
            {
                return;
            }

            ApplyCodeScan(scan);
            ScanStatusText = "扫描完成 · " + scan.Elapsed.TotalMilliseconds.ToString("0") + " ms";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            UpdateScanStatus(scanGeneration, "扫描已取消");
        }
        catch (Exception exception)
        {
            UpdateScanStatus(scanGeneration, "扫描失败：" + exception.Message);
        }
        finally
        {
            FinishScan(scanGeneration);
        }
    }

    /// <summary>只允许当前请求代次更新扫描状态，防止旧任务覆盖新请求。</summary>
    private void UpdateScanStatus(long scanGeneration, string status)
    {
        if (scanGeneration == mScanGeneration)
        {
            ScanStatusText = status;
        }
    }

    /// <summary>只结束当前请求代次，旧扫描的 finally 不得关闭新扫描进度。</summary>
    private void FinishScan(long scanGeneration)
    {
        if (scanGeneration == mScanGeneration)
        {
            IsScanning = false;
            NotifyScanProperties();
        }
    }

    /// <summary>应用一次扫描结果并与最后一帧 Runtime 事件增量合并。</summary>
    private void ApplyCodeScan(WorkbenchEventKitCodeScan scan)
    {
        string selectedIdentity = SelectedEvent?.Identity ?? string.Empty;
        mCodeRelationsByIdentity.Clear();
        for (var index = 0; index < scan.Relations.Count; index++)
        {
            WorkbenchEventKitCodeRelation relation = scan.Relations[index];
            mCodeRelationsByIdentity[relation.Identity] = relation;
        }

        mScannedFileCount = scan.ScannedFileCount;
        mMatchedFileCount = scan.MatchedFileCount;
        ReconcileEventItems(mRuntimeEventsByIdentity.Values.ToArray(), true);
        ReconcileVisibleEvents();
        RestoreSelection(selectedIdentity);
        NotifySummaryProperties();
        NotifyScanProperties();
    }

    /// <summary>通过注入的宿主边界打开一个项目内 C# 位置并转换错误为页面状态。</summary>
    private async Task OpenCodeLocationAsync(WorkbenchEventKitCodeLocation location)
    {
        if (mOpenLocationAsync == null)
        {
            ScanStatusText = "当前宿主不支持源码定位";
            return;
        }

        try
        {
            await mOpenLocationAsync(location);
            ScanStatusText = "已打开 " + location.Display;
        }
        catch (Exception exception)
        {
            ScanStatusText = "打开失败：" + exception.Message;
        }
    }

    /// <summary>取消旧扫描并轮换 CancellationTokenSource。</summary>
    private void CancelCurrentScan()
    {
        mScanGeneration++;
        mScanCancellation.Cancel();
        mScanCancellation.Dispose();
        mScanCancellation = new CancellationTokenSource();
    }

    /// <summary>通知扫描统计与可用状态派生属性。</summary>
    private void NotifyScanProperties()
    {
        OnPropertyChanged(nameof(ScanFileCountText));
    }

    /// <summary>停止页面后台扫描并释放取消资源。</summary>
    public void Dispose()
    {
        mScanGeneration++;
        mScanCancellation.Cancel();
        mScanCancellation.Dispose();
    }
}
