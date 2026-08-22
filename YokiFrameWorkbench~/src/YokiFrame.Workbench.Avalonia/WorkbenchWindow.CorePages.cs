using Avalonia.Controls;
using Avalonia.Input.Platform;
using YokiFrame.Tooling.Application.Documentation;
using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;
using YokiFrame.Tooling.Application.Models.UIKit;
using YokiFrame.Tooling.Application.Packages;
using YokiFrame.Tooling.Application.Services.EventKit;
using YokiFrame.Tooling.Application.Services.AudioKit;
using YokiFrame.Tooling.Application.Services.SaveKit;
using YokiFrame.Tooling.Application.Services.TableKit;
using YokiFrame.Tooling.Application.Services.UIKit;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>
/// 承载 Workbench 专用 Core 页面使用的可选服务和平台边界。
/// </summary>
public sealed partial class WorkbenchWindow
{
    /// <summary>
    /// 组合 Workbench 页面所需的 Application 服务、包元数据和平台回调。
    /// </summary>
    /// <param name="projectRoot">当前宿主项目根。</param>
    /// <param name="sourcePackageRoot">启动入口解析出的真实 YokiFrame 包根。</param>
    /// <returns>完成依赖组合的 Shell ViewModel。</returns>
    private WorkbenchShellViewModel CreateShellViewModel(
        string projectRoot,
        string sourcePackageRoot)
    {
        var documentationService = TryCreateDocumentationService(
            sourcePackageRoot,
            out var documentationInitializationError);
        var packageMetadata = TryReadPackageMetadata(
            sourcePackageRoot,
            out var packageMetadataInitializationError);
        Func<bool, CancellationToken, Task<WorkbenchEventKitCodeScan>>? eventKitCodeScanAsync =
            string.IsNullOrWhiteSpace(projectRoot)
                ? null
                : (excludeEditor, cancellationToken) => ScanEventKitCodeAsync(
                    projectRoot,
                    excludeEditor,
                    cancellationToken);
        var folderPicker = new AvaloniaInstallerFolderPicker(() => StorageProvider);
        var tableKitLubanFilePicker = new AvaloniaTableKitLubanFilePicker(() => StorageProvider);
        var viewModel = new WorkbenchShellViewModel(
            new WorkbenchShellCallbacks(QueueDashboardRefresh, ChangeEngine, SendCommandAsync, OpenUriAsync, CopyTextAsync),
            new WorkbenchShellMeta(sourcePackageRoot, projectRoot, packageMetadata, documentationService, documentationInitializationError),
            new WorkbenchFsmEventDependencies(QueryFsmDetailsAsync, eventKitCodeScanAsync, OpenEventKitCodeLocationAsync),
            new WorkbenchLogKitDependencies(
                mDashboardService.LoadLogKitProjectSettings,
                mDashboardService.SaveLogKitSettingsAsync,
                mDashboardService.ClearLogKitHistoryAsync,
                mDashboardService.ReadLogKitFileAsync),
            new WorkbenchPoolKitDependencies(
                mDashboardService.SetPoolKitTrackingAsync,
                mDashboardService.CheckPoolKitLeaksAsync,
                mDashboardService.ClearPoolKitHistoryAsync,
                OpenPoolKitCodeLocationAsync),
            new WorkbenchResKitDependencies(
                mDashboardService.GetResKitResourceDetailAsync,
                mDashboardService.SetResKitTrackingAsync,
                mDashboardService.ClearResKitHistoryAsync,
                OpenResKitCodeLocationAsync),
            new WorkbenchActionKitDependencies(
                mDashboardService.SetActionKitStackTraceAsync,
                mDashboardService.ClearActionKitStackTraceAsync),
            new WorkbenchAudioKitDependencies(
                ScanAudioIndexAsync,
                GenerateAudioIndexAsync,
                LoadAudioIndexSettings,
                SaveAudioIndexSettingsAsync),
            new WorkbenchToolPageDependencies(
                string.IsNullOrWhiteSpace(projectRoot)
                    ? null
                    : new SaveKitWorkbenchSettingsService(mDashboardService.ProjectSettingsStore),
                folderPicker,
                OpenExistingDirectoryAsync,
                string.IsNullOrWhiteSpace(projectRoot)
                    ? null
                    : new TableKitApplicationService(),
                tableKitLubanFilePicker,
                ExecuteUIKitEditorActionAsync,
                string.IsNullOrWhiteSpace(projectRoot)
                    ? null
                    : new UIKitEditorSettingsService(mDashboardService.ProjectSettingsStore)));
        viewModel.LogKitPage.SetOpenDirectoryHandler(OpenExistingDirectoryAsync);
        if (!string.IsNullOrWhiteSpace(packageMetadataInitializationError))
        {
            viewModel.ShowTransientError(packageMetadataInitializationError);
        }

        return viewModel;
    }

    /// <summary>
    /// 将 UIKit Editor Tools 操作路由到当前 Unity Editor engine，保持页面不直接解析 FileBridge。
    /// </summary>
    /// <param name="action">要执行的 UIKit Editor 操作。</param>
    /// <param name="request">创建或生成代码时的强类型请求。</param>
    /// <param name="cancellationToken">操作取消令牌。</param>
    /// <returns>Application 已解析的 UIKit 操作结果。</returns>
    private Task<WorkbenchUIKitEditorResult> ExecuteUIKitEditorActionAsync(
        WorkbenchUIKitEditorAction action,
        WorkbenchUIKitPanelGenerationRequest? request,
        CancellationToken cancellationToken)
    {
        return mDashboardService.ExecuteUIKitEditorActionAsync(
            mSelectedEngineId,
            action,
            request,
            cancellationToken);
    }

    /// <summary>在线程池执行只读音频索引扫描，避免阻塞 Avalonia UI。</summary>
    private static Task<AudioIndexResult> ScanAudioIndexAsync(
        AudioIndexRequest request,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => new AudioIndexService().Scan(request), cancellationToken);
    }

    /// <summary>在线程池执行音频索引原子生成，避免阻塞 Avalonia UI。</summary>
    private static Task<AudioIndexResult> GenerateAudioIndexAsync(
        AudioIndexRequest request,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => new AudioIndexService().Generate(request), cancellationToken);
    }

    /// <summary>从指定项目的 ProjectSettings 读取 AudioKit 索引配置。</summary>
    private AudioIndexSettings LoadAudioIndexSettings(string projectRoot)
    {
        return new AudioIndexSettingsService(mDashboardService.ProjectSettingsStore).Load();
    }

    /// <summary>把 AudioKit 索引配置原子保存到指定项目的 ProjectSettings。</summary>
    private Task SaveAudioIndexSettingsAsync(
        string projectRoot,
        AudioIndexSettings settings,
        CancellationToken cancellationToken)
    {
        return new AudioIndexSettingsService(mDashboardService.ProjectSettingsStore).SaveAsync(settings, cancellationToken);
    }

    /// <summary>通过操作系统默认文件管理器打开 LogKit 或 SaveKit 已解析目录。</summary>
    /// <param name="path">已解析且存在的目录绝对路径。</param>
    private static Task OpenExistingDirectoryAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return Task.CompletedTask;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }

    /// <summary>在用户进入 EventKit 页面后再创建扫描器，扫描路径无效时只让该页降级。</summary>
    private static Task<WorkbenchEventKitCodeScan> ScanEventKitCodeAsync(
        string projectRoot,
        bool excludeEditor,
        CancellationToken cancellationToken)
    {
        var service = new EventKitCodeScanService(projectRoot);
        return service.ScanAsync(excludeEditor, cancellationToken);
    }

    /// <summary>通过 Application 路径保护和 FileBridge UserAction 打开 EventKit 源码位置。</summary>
    private async Task OpenEventKitCodeLocationAsync(WorkbenchEventKitCodeLocation location)
    {
        await mDashboardService.OpenEventKitCodeLocationAsync(
            mSelectedEngineId,
            location,
            CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>通过 Application 路径保护和 FileBridge UserAction 打开 ResKit lease 来源。</summary>
    private async Task OpenResKitCodeLocationAsync(
        Tooling.Application.Models.ResKit.WorkbenchResKitLoadSource source)
    {
        await mDashboardService.OpenResKitCodeLocationAsync(
            mSelectedEngineId,
            source.FilePath,
            source.Line,
            CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>通过 Application 路径保护和 FileBridge UserAction 打开 PoolKit 借出位置。</summary>
    private async Task OpenPoolKitCodeLocationAsync(string filePath, int line)
    {
        await mDashboardService.OpenPoolKitCodeLocationAsync(
            mSelectedEngineId,
            filePath,
            line,
            CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// 从启动入口解析出的真实包根读取版本与仓库主页；设计时无包根模式保持安静降级。
    /// </summary>
    /// <param name="sourcePackageRoot">真实 YokiFrame 包根。</param>
    /// <param name="errorMessage">无效 package.json 的可显示诊断。</param>
    /// <returns>有效包元数据；无包根或读取失败时为空。</returns>
    private static YokiFramePackageMetadata? TryReadPackageMetadata(
        string sourcePackageRoot,
        out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(sourcePackageRoot))
        {
            errorMessage = string.Empty;
            return null;
        }

        try
        {
            errorMessage = string.Empty;
            return YokiFramePackageMetadataReader.Read(sourcePackageRoot);
        }
        catch (Exception exception)
        {
            errorMessage = "包元数据不可用: " + exception.Message;
            return null;
        }
    }

    /// <summary>
    /// 安全创建可选 Docs 服务；无效包根只形成页面诊断，不阻断主窗口。
    /// </summary>
    /// <param name="sourcePackageRoot">启动入口解析出的包根。</param>
    /// <param name="errorMessage">创建失败时的可显示原因。</param>
    /// <returns>可用服务；创建失败时为空。</returns>
    private static OfflineDocumentationService? TryCreateDocumentationService(
        string sourcePackageRoot,
        out string errorMessage)
    {
        try
        {
            errorMessage = string.Empty;
            return new OfflineDocumentationService(sourcePackageRoot);
        }
        catch (Exception exception)
        {
            errorMessage = "文档服务不可用: " + exception.Message;
            return null;
        }
    }

    /// <summary>
    /// 在后台按稳定 instanceId 查询 FsmKit 详情，周期 dashboard 刷新不会调用此入口。
    /// </summary>
    /// <param name="instanceId">FsmKit 注册表返回的稳定实例标识。</param>
    /// <param name="cancellationToken">查询取消令牌。</param>
    /// <returns>Application 已解析的 FsmKit 详情。</returns>
    private async Task<WorkbenchFsmKitState> QueryFsmDetailsAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        var selectedEngineId = mSelectedEngineId;
        return await mDashboardService.QueryFsmDetailsAsync(
            selectedEngineId,
            instanceId,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 通过当前 Avalonia TopLevel 把文档代码写入系统剪贴板。
    /// </summary>
    /// <param name="text">需要复制的完整代码文本。</param>
    /// <returns>剪贴板写入完成任务。</returns>
    private async Task CopyTextAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null)
        {
            throw new InvalidOperationException("当前窗口没有可用剪贴板服务。");
        }

        await clipboard.SetTextAsync(text);
    }

    /// <summary>
    /// 使用当前 Avalonia TopLevel 的 Launcher 在默认浏览器中打开 HTTPS 地址。
    /// </summary>
    /// <param name="uri">Application 验证完成的仓库主页。</param>
    /// <returns>浏览器启动完成任务。</returns>
    private async Task OpenUriAsync(Uri uri)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if (launcher == null)
        {
            throw new InvalidOperationException("当前窗口没有可用的外部链接服务。");
        }

        if (!await launcher.LaunchUriAsync(uri))
        {
            throw new InvalidOperationException("系统未能打开默认浏览器。");
        }
    }
}
