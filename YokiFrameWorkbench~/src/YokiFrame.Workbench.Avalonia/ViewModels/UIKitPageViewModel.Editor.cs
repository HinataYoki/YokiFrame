using YokiFrame.Tooling.Application.Models.UIKit;
using YokiFrame.Tooling.Application.Services.UIKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 UIKit Unity Editor Tools 的任务切换、表单和显式命令。</summary>
public sealed partial class UIKitPageViewModel
{
    private readonly Func<WorkbenchUIKitEditorAction, WorkbenchUIKitPanelGenerationRequest?, CancellationToken, Task<WorkbenchUIKitEditorResult>>? mEditorActionAsync;
    private readonly UIKitEditorSettingsService? mEditorSettingsService;
    private string mEditorEngineId = string.Empty;
    private int mSelectedTaskIndex;
    private bool mEditorBusy;
    private bool mEditorDefaultsLoaded;
    private bool mEditorSettingsDirty;
    private bool mApplyingGenerationSettings;
    private string mEditorStatusText = string.Empty;
    private string mPanelName = string.Empty;
    private string mPrefabFolder = "Assets/Resources/Art/UIPrefab";
    private string mScriptFolder = "Assets/Scripts/UI";
    private string mScriptNamespace = "GameUI";
    private string mAssemblyName = "Assembly-CSharp";
    private string mCodeTemplate = "Default";
    private IReadOnlyList<string> mCodeTemplateNames = new[] { "Default", "Minimal" };
    private IReadOnlyList<string> mCodeTemplateOptions = new[] { "默认", "精简" };
    private bool mCanGenerateCode;
    private long mContextRevision;
    private string mActiveGlobalObjectId = string.Empty;

    /// <summary>获取当前任务索引，0 为 Runtime，1 为 Editor Tools。</summary>
    public int SelectedTaskIndex
    {
        get => mSelectedTaskIndex;
        private set
        {
            if (!SetProperty(ref mSelectedTaskIndex, value)) return;
            OnPropertyChanged(nameof(IsRuntimeTask));
            OnPropertyChanged(nameof(IsEditorToolsTask));
        }
    }

    /// <summary>获取当前是否展示 Runtime Diagnostics。</summary>
    public bool IsRuntimeTask => SelectedTaskIndex == 0;

    /// <summary>获取当前是否展示 Unity Editor Tools。</summary>
    public bool IsEditorToolsTask => SelectedTaskIndex == 1;

    /// <summary>获取 Editor action 是否正在执行。</summary>
    public bool EditorBusy
    {
        get => mEditorBusy;
        private set
        {
            if (!SetProperty(ref mEditorBusy, value)) return;
            RaiseEditorCommandStates();
        }
    }

    /// <summary>获取 Editor action 结果文本。</summary>
    public string EditorStatusText
    {
        get => mEditorStatusText;
        private set
        {
            if (!SetProperty(ref mEditorStatusText, value)) return;
            OnPropertyChanged(nameof(HasEditorStatus));
        }
    }

    /// <summary>获取是否存在可显示的 Editor 状态。</summary>
    public bool HasEditorStatus => !string.IsNullOrWhiteSpace(EditorStatusText);

    /// <summary>获取当前 Editor Tools 是否连接到 Unity Editor。</summary>
    public bool EditorToolsAvailable => mEditorActionAsync != null
        && string.Equals(mEditorEngineId, "unity-editor", StringComparison.OrdinalIgnoreCase);

    /// <summary>获取或设置 Panel 类型名。</summary>
    public string PanelName { get => mPanelName; set { if (SetProperty(ref mPanelName, value ?? string.Empty)) RaiseEditorCommandStates(); } }

    /// <summary>获取或设置 Prefab 输出目录。</summary>
    public string PrefabFolder
    {
        get => mPrefabFolder;
        set
        {
            if (!SetProperty(ref mPrefabFolder, value ?? string.Empty)) return;
            MarkEditorSettingsDirty();
        }
    }

    /// <summary>获取或设置代码输出目录。</summary>
    public string ScriptFolder
    {
        get => mScriptFolder;
        set
        {
            if (!SetProperty(ref mScriptFolder, value ?? string.Empty)) return;
            MarkEditorSettingsDirty();
        }
    }

    /// <summary>获取或设置代码命名空间。</summary>
    public string ScriptNamespace
    {
        get => mScriptNamespace;
        set
        {
            if (!SetProperty(ref mScriptNamespace, value ?? string.Empty)) return;
            MarkEditorSettingsDirty();
        }
    }

    /// <summary>获取或设置目标程序集。</summary>
    public string AssemblyName
    {
        get => mAssemblyName;
        set
        {
            if (!SetProperty(ref mAssemblyName, value ?? string.Empty)) return;
            MarkEditorSettingsDirty();
        }
    }

    /// <summary>获取或设置提交给 Unity Editor 的代码模板协议值。</summary>
    public string CodeTemplate
    {
        get => mCodeTemplate;
        set
        {
            if (!SetProperty(ref mCodeTemplate, value ?? "Default")) return;
            MarkEditorSettingsDirty();
            OnPropertyChanged(nameof(CodeTemplateDisplay));
        }
    }

    /// <summary>获取或设置代码模板的简体中文显示名称。</summary>
    public string CodeTemplateDisplay
    {
        get => GetCodeTemplateDisplayName(CodeTemplate);
        set => CodeTemplate = GetCodeTemplateName(value);
    }

    /// <summary>获取可供界面选择的模板显示名；项目模板保留原始名称。</summary>
    public IReadOnlyList<string> CodeTemplateOptions => mCodeTemplateOptions;

    /// <summary>获取当前选择是否支持代码生成。</summary>
    public bool CanGenerateCode { get => mCanGenerateCode; private set => SetProperty(ref mCanGenerateCode, value); }

    /// <summary>获取显示 Runtime 任务的命令。</summary>
    public RelayCommand ShowRuntimeTaskCommand { get; private set; } = null!;

    /// <summary>获取显示 Editor Tools 任务的命令。</summary>
    public AsyncRelayCommand ShowEditorToolsTaskCommand { get; private set; } = null!;

    /// <summary>获取创建 Panel Prefab 的命令。</summary>
    public AsyncRelayCommand CreatePanelPrefabCommand { get; private set; } = null!;

    /// <summary>获取为当前 Prefab 生成代码的命令。</summary>
    public AsyncRelayCommand GenerateCodeCommand { get; private set; } = null!;

    /// <summary>由构造函数初始化全部 Editor task 命令。</summary>
    private void InitializeEditorCommands()
    {
        ShowRuntimeTaskCommand = new RelayCommand(() => SelectedTaskIndex = 0);
        ShowEditorToolsTaskCommand = new AsyncRelayCommand(ShowEditorToolsAsync, CanUseEditorTools);
        CreatePanelPrefabCommand = new AsyncRelayCommand(CreatePanelPrefabAsync, CanCreatePanelPrefab);
        GenerateCodeCommand = new AsyncRelayCommand(GenerateCodeAsync, CanUseEditorTools);
    }

    /// <summary>更新当前 engine，并在离开 Unity Editor 时清空选择事实。</summary>
    internal void SetEditorEngine(string engineId)
    {
        string normalized = engineId ?? string.Empty;
        if (string.Equals(mEditorEngineId, normalized, StringComparison.Ordinal)) return;
        PersistEditorSettingsOnClose();
        mEditorEngineId = normalized;
        mEditorDefaultsLoaded = false;
        ResetEditorContext();
        OnPropertyChanged(nameof(EditorToolsAvailable));
        RaiseEditorCommandStates();
        if (!EditorToolsAvailable && IsEditorToolsTask) SelectedTaskIndex = 0;
    }

    /// <summary>切换到 Editor Tools 并读取当前 Unity 选择。</summary>
    private async Task ShowEditorToolsAsync()
    {
        SelectedTaskIndex = 1;
        string loadError = LoadEditorSettings();
        bool refreshed = await RefreshEditorContextAsync();
        if (!string.IsNullOrWhiteSpace(loadError)) EditorStatusText = loadError;
        else if (refreshed) EditorStatusText = string.Empty;
    }

    /// <summary>首次进入当前 Unity engine 时优先读取已保存配置，未配置时继续接受 Provider 默认值。</summary>
    private string LoadEditorSettings()
    {
        if (mEditorDefaultsLoaded || mEditorSettingsService == null) return string.Empty;
        try
        {
            WorkbenchUIKitPanelGenerationRequest? settings = mEditorSettingsService.Load();
            if (settings == null) return string.Empty;
            ApplyGenerationSettings(settings);
            mEditorDefaultsLoaded = true;
            return string.Empty;
        }
        catch (Exception exception)
        {
            return "配置读取失败，已使用 Unity 默认值: " + exception.Message;
        }
    }

    /// <summary>
    /// 在 Workbench 关闭前同步提交用户修改过的 Editor Tools 配置，避免未执行生成操作时丢失表单值。
    /// </summary>
    internal void PersistEditorSettingsOnClose()
    {
        if (mEditorSettingsService == null || !mEditorSettingsDirty) return;
        try
        {
            var request = CreateGenerationRequest();
            Task.Run(() => mEditorSettingsService.SaveAsync(request, CancellationToken.None))
                .GetAwaiter()
                .GetResult();
            mEditorSettingsDirty = false;
            mEditorDefaultsLoaded = true;
        }
        catch (Exception exception)
        {
            EditorStatusText = "配置保存失败: " + exception.Message;
        }
    }

    /// <summary>在创建或生成前通过统一项目配置 Store 保存五个 Editor Tools 生成字段。</summary>
    /// <returns>保存成功或当前项目未提供设置服务时返回 true。</returns>
    private async Task<bool> PersistGenerationSettingsAsync()
    {
        if (mEditorSettingsService == null) return true;
        EditorBusy = true;
        try
        {
            await mEditorSettingsService.SaveAsync(CreateGenerationRequest(), CancellationToken.None);
            mEditorSettingsDirty = false;
            mEditorDefaultsLoaded = true;
            return true;
        }
        catch (Exception exception)
        {
            EditorStatusText = "配置保存失败: " + exception.Message;
            return false;
        }
        finally
        {
            EditorBusy = false;
        }
    }

    /// <summary>显式刷新当前 Unity 选择上下文。</summary>
    private Task<bool> RefreshEditorContextAsync()
    {
        return RunEditorActionAsync(WorkbenchUIKitEditorAction.RefreshContext, null);
    }

    /// <summary>保存当前表单配置后提交 Panel Prefab 生成请求。</summary>
    private async Task CreatePanelPrefabAsync()
    {
        if (!await PersistGenerationSettingsAsync()) return;
        await RunEditorActionAsync(WorkbenchUIKitEditorAction.CreatePanelPrefab, CreateGenerationRequest());
    }

    /// <summary>保存当前表单配置并读取最新选择后，为当前 Panel Prefab 提交代码生成请求。</summary>
    private async Task GenerateCodeAsync()
    {
        if (!await PersistGenerationSettingsAsync()) return;
        if (!await RefreshEditorContextAsync()) return;
        if (!CanGenerateCode)
        {
            EditorStatusText = "当前 Unity 选择不是有效的 Panel Prefab，无法生成代码。";
            return;
        }

        await RunEditorActionAsync(
            WorkbenchUIKitEditorAction.GenerateCodeForSelection,
            CreateGenerationRequest());
    }

    /// <summary>执行强类型 Application action，并接受操作后 context。</summary>
    private async Task<bool> RunEditorActionAsync(
        WorkbenchUIKitEditorAction action,
        WorkbenchUIKitPanelGenerationRequest? request)
    {
        if (mEditorActionAsync == null) return false;
        EditorBusy = true;
        EditorStatusText = "正在执行“" + GetEditorActionName(action) + "”...";
        try
        {
            WorkbenchUIKitEditorResult result = await mEditorActionAsync(
                action,
                request,
                CancellationToken.None);
            EditorStatusText = result.Message;
            if (result.Context != null) ApplyEditorContext(result.Context);
            return result.Succeeded;
        }
        catch (Exception exception)
        {
            EditorStatusText = "操作失败: " + exception.Message;
            return false;
        }
        finally
        {
            EditorBusy = false;
        }
    }

    /// <summary>从表单构造 Application 强类型生成请求。</summary>
    private WorkbenchUIKitPanelGenerationRequest CreateGenerationRequest()
    {
        return WorkbenchUIKitPresentation.CreateGenerationRequest(
            PanelName,
            PrefabFolder,
            ScriptFolder,
            ScriptNamespace,
            AssemblyName,
            CodeTemplate,
            mContextRevision,
            mActiveGlobalObjectId);
    }

    /// <summary>应用 Unity 选择 context，并只在首次读取时接受 Provider 默认值。</summary>
    private void ApplyEditorContext(WorkbenchUIKitEditorContext context)
    {
        CanGenerateCode = context.CanGenerateCode;
        mContextRevision = context.ContextRevision;
        mActiveGlobalObjectId = context.ActiveGlobalObjectId;
        ApplyCodeTemplateOptions(context.CodeTemplateOptions);
        if (!mEditorDefaultsLoaded)
        {
            ApplyGenerationSettings(context.Defaults);
            mEditorDefaultsLoaded = true;
        }

        ApplyAssemblyOptions(context.AssemblyNames);
        string unavailableTemplate = EnsureCodeTemplateSelection(context.Defaults.CodeTemplate);
        if (!string.IsNullOrWhiteSpace(unavailableTemplate))
        {
            EditorStatusText = "代码模板 “" + unavailableTemplate
                + "” 当前不可用，已切换为 “" + CodeTemplateDisplay + "”。";
        }

        RaiseEditorCommandStates();
    }

    /// <summary>把强类型生成配置投影到页面字段，不修改一次性的 Panel 类型名。</summary>
    private void ApplyGenerationSettings(WorkbenchUIKitPanelGenerationRequest settings)
    {
        mApplyingGenerationSettings = true;
        try
        {
            PrefabFolder = settings.PrefabFolder;
            ScriptFolder = settings.ScriptFolder;
            ScriptNamespace = settings.ScriptNamespace;
            EnsureAssemblyOption(settings.AssemblyName);
            AssemblyName = settings.AssemblyName;
            CodeTemplate = settings.CodeTemplate;
        }
        finally
        {
            mApplyingGenerationSettings = false;
        }
    }

    /// <summary>标记用户修改过的项目级 Editor Tools 字段，供关闭时一次性提交。</summary>
    private void MarkEditorSettingsDirty()
    {
        if (!mApplyingGenerationSettings)
        {
            mEditorSettingsDirty = true;
        }
    }

    /// <summary>清空已经离线或切换宿主的 Unity 选择事实。</summary>
    private void ResetEditorContext()
    {
        CanGenerateCode = false;
        mContextRevision = 0L;
        mActiveGlobalObjectId = string.Empty;
        EditorStatusText = EditorToolsAvailable ? string.Empty : "请选择 Unity 编辑器使用编辑器工具。";
    }

    /// <summary>把 Editor action 协议枚举转换为简体中文操作名称。</summary>
    private static string GetEditorActionName(WorkbenchUIKitEditorAction action)
    {
        return WorkbenchUIKitPresentation.GetEditorActionDisplayName(action);
    }

    /// <summary>判断是否可以进入 Editor Tools。</summary>
    private bool CanUseEditorTools() => EditorToolsAvailable && !EditorBusy;

    /// <summary>判断是否可以创建 Panel Prefab。</summary>
    private bool CanCreatePanelPrefab() => CanUseEditorTools() && !string.IsNullOrWhiteSpace(PanelName);

    /// <summary>通知全部 Editor task 命令重新计算可执行状态。</summary>
    private void RaiseEditorCommandStates()
    {
        ShowEditorToolsTaskCommand?.RaiseCanExecuteChanged();
        CreatePanelPrefabCommand?.RaiseCanExecuteChanged();
        GenerateCodeCommand?.RaiseCanExecuteChanged();
    }
}
