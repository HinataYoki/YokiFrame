using System.Windows.Input;
using YokiFrame.Tooling.Application.Documentation;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.ActionKit;
using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Tooling.Application.Models.FsmKit;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;
using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Tooling.Application.Models.PoolKit;
using YokiFrame.Tooling.Application.Models.ResKit;
using YokiFrame.Tooling.Application.Models.UIKit;
using YokiFrame.Tooling.Application.Packages;
using YokiFrame.Tooling.Application.Services.SaveKit;
using YokiFrame.Tooling.Application.Services.TableKit;
using YokiFrame.Tooling.Application.Services.UIKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>Shell 级回调与元数据：刷新、engine 切换、命令发送、外链、剪贴板与包信息。</summary>
public sealed record WorkbenchShellCallbacks(
    Action RefreshRequested,
    Action<string> EngineChanged,
    Func<string, string, Task> CommandRequested,
    Func<Uri, Task>? OpenUriAsync = null,
    Func<string, Task>? CopyTextAsync = null);

/// <summary>Shell 元数据：真实包根、项目根、包元数据与离线文档服务。</summary>
public sealed record WorkbenchShellMeta(
    string SourcePackageRoot,
    string ProjectRoot,
    YokiFramePackageMetadata? PackageMetadata = null,
    OfflineDocumentationService? DocumentationService = null,
    string DocumentationInitializationError = "");

/// <summary>FsmKit 详情查询与 EventKit 扫描/源码定位依赖。</summary>
public sealed record WorkbenchFsmEventDependencies(
    Func<string, CancellationToken, Task<WorkbenchFsmKitState>>? FsmDetailsQuery = null,
    Func<bool, CancellationToken, Task<WorkbenchEventKitCodeScan>>? EventKitCodeScanAsync = null,
    Func<WorkbenchEventKitCodeLocation, Task>? OpenEventKitCodeLocationAsync = null);

/// <summary>LogKit 设置读写、历史清理与日志文件尾读依赖。</summary>
public sealed record WorkbenchLogKitDependencies(
    Func<string, WorkbenchLogKitProjectSettings>? LoadProjectSettings = null,
    Func<string, WorkbenchLogKitSettings, string, CancellationToken, Task<WorkbenchLogKitSettingsSaveResult>>? SaveSettingsAsync = null,
    Func<string, CancellationToken, Task<WorkbenchLogKitState>>? ClearHistoryAsync = null,
    Func<string, string, CancellationToken, Task<WorkbenchLogKitFilePreview>>? ReadFileAsync = null);

/// <summary>PoolKit 跟踪/泄漏/历史与源码定位依赖。</summary>
public sealed record WorkbenchPoolKitDependencies(
    Func<string, bool, bool, bool, CancellationToken, Task<WorkbenchPoolKitState>>? SetTrackingAsync = null,
    Func<string, CancellationToken, Task<WorkbenchPoolKitState>>? CheckLeaksAsync = null,
    Func<string, CancellationToken, Task<WorkbenchPoolKitState>>? ClearHistoryAsync = null,
    Func<string, int, Task>? OpenCodeLocationAsync = null);

/// <summary>ResKit 详情/跟踪/卸载历史与 lease 来源定位依赖。</summary>
public sealed record WorkbenchResKitDependencies(
    Func<string, string, string, CancellationToken, Task<WorkbenchResKitResourceDetail>>? GetResourceDetailAsync = null,
    Func<string, bool, CancellationToken, Task<WorkbenchResKitState>>? SetTrackingAsync = null,
    Func<string, CancellationToken, Task<WorkbenchResKitState>>? ClearHistoryAsync = null,
    Func<WorkbenchResKitLoadSource, Task>? OpenCodeLocationAsync = null);

/// <summary>ActionKit 堆栈捕获切换与清空依赖。</summary>
public sealed record WorkbenchActionKitDependencies(
    Func<string, bool, CancellationToken, Task<WorkbenchActionKitState>>? SetStackTraceAsync = null,
    Func<string, CancellationToken, Task<WorkbenchActionKitState>>? ClearStackTraceAsync = null);

/// <summary>AudioKit 索引扫描/生成与项目独立配置读写依赖。</summary>
public sealed record WorkbenchAudioKitDependencies(
    Func<AudioIndexRequest, CancellationToken, Task<AudioIndexResult>>? ScanIndexAsync = null,
    Func<AudioIndexRequest, CancellationToken, Task<AudioIndexResult>>? GenerateIndexAsync = null,
    Func<string, AudioIndexSettings>? LoadSettings = null,
    Func<string, AudioIndexSettings, CancellationToken, Task>? SaveSettingsAsync = null);

/// <summary>SaveKit / TableKit / UIKit 的服务与文件选择器依赖。</summary>
public sealed record WorkbenchToolPageDependencies(
    SaveKitWorkbenchSettingsService? SaveKitSettingsService = null,
    IInstallerFolderPicker? SaveKitFolderPicker = null,
    Func<string, Task>? SaveKitOpenDirectoryAsync = null,
    TableKitApplicationService? TableKitApplicationService = null,
    ITableKitLubanFilePicker? TableKitLubanFilePicker = null,
    Func<WorkbenchUIKitEditorAction, WorkbenchUIKitPanelGenerationRequest?, CancellationToken, Task<WorkbenchUIKitEditorResult>>? UIKitEditorActionAsync = null,
    UIKitEditorSettingsService? UIKitEditorSettingsService = null);