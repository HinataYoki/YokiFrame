using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text.Json;
using YokiFrame.Tooling.Application.Models.TableKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 TableKit 配置操作、目录交互、结果投影和本地环境读取。</summary>
public sealed partial class TableKitPageViewModel
{
    /// <summary>执行配置验证和预览读取。</summary>
    private async Task ValidateAsync()
    {
        StatusText = "正在验证";
        StatusDetailText = "正在读取 Luban 临时输出。";
        IsConsoleExpanded = true;
        RefreshConfiguration();
        AppendConsole("INFO", "开始验证配置并生成临时 JSON 预览。", false);
        TableKitOperationResult result = await mService.ValidateAsync(CreateOptions());
        ApplyOperationResult(result, true);
    }

    /// <summary>执行正式 Luban 生成并写入项目代码和宿主程序集边界。</summary>
    private async Task GenerateAsync()
    {
        StatusText = "正在生成";
        StatusDetailText = "正在写入正式输出和生成契约。";
        IsConsoleExpanded = true;
        SaveConfiguration();
        AppendConsole("INFO", "开始生成配置表。", false);
        TableKitOperationResult result = await mService.GenerateAsync(CreateOptions());
        ApplyOperationResult(result, false);
    }

    /// <summary>重新读取当前 luban.conf 的 target，并刷新环境摘要。</summary>
    private void RefreshConfiguration()
    {
        RefreshEnvironment();
        if (!File.Exists(ResolveInputPath(ConfigPath))) return;
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ResolveInputPath(ConfigPath)));
            if (document.RootElement.TryGetProperty("targets", out JsonElement targets) && targets.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement target in targets.EnumerateArray())
                {
                    if (!target.TryGetProperty("name", out JsonElement name)) continue;
                    AddOption(TargetOptions, name.GetString());
                }
            }
            AppendConsole("INFO", "已刷新 Luban target 列表。", false);
        }
        catch (JsonException exception)
        {
            AppendConsole("WARNING", "luban.conf 解析失败: " + exception.Message, false);
        }
    }

    /// <summary>保存当前页面配置到项目 ProjectSettings。</summary>
    private void SaveConfiguration()
    {
        if (!TryPersistConfiguration()) return;
        AppendConsole("SUCCESS", "TableKit 配置已保存到当前项目。", false);
        StatusText = "已保存";
    }

    /// <summary>尝试把当前 TableKit 草稿保存到项目设置，失败时保留可见诊断且不阻断窗口关闭。</summary>
    /// <returns>配置成功落盘时返回 true。</returns>
    public bool TryPersistConfiguration()
    {
        try
        {
            mSettingsService.Save(mProjectRoot, CreateOptions());
            return true;
        }
        catch (Exception exception)
        {
            StatusText = "保存失败";
            StatusDetailText = exception.Message;
            AppendConsole("ERROR", "TableKit 配置保存失败: " + exception.Message, false);
            return false;
        }
    }

    /// <summary>恢复默认配置并清除额外输出目标。</summary>
    private void ResetConfiguration()
    {
        ApplyOptions(mDefaultOptions);
        ClearPreview();
        SelectedWorkspaceIndex = 0;
        IsConsoleExpanded = false;
        if (!TryPersistConfiguration()) return;
        AppendConsole("INFO", "已还原默认 TableKit 配置。", false);
        StatusText = "已还原默认";
    }

    /// <summary>加入一个默认额外 JSON 输出目标。</summary>
    private void AddExtraOutput()
    {
        TableKitExtraOutputViewModel output = new(
            new TableKitExtraOutput
            {
                TargetName = "server",
                CodeTarget = "java-json",
                DataTarget = "json",
                OutputDataDir = "Temp/LubanExtra/server/data",
                OutputCodeDir = "Temp/LubanExtra/server/code"
            },
            RemoveExtraOutput,
            TargetOptions,
            ExtraCodeTargetOptions,
            DataTargetOptions,
            mProjectRoot,
            mFolderPicker);
        ExtraOutputTargets.Add(output);
        OnPropertyChanged(nameof(ExtraOutputTargets));
        OnPropertyChanged(nameof(HasExtraOutputTargets));
    }

    /// <summary>从当前集合移除一个额外输出目标。</summary>
    /// <param name="output">待移除目标。</param>
    private void RemoveExtraOutput(TableKitExtraOutputViewModel output)
    {
        ExtraOutputTargets.Remove(output);
        OnPropertyChanged(nameof(ExtraOutputTargets));
        OnPropertyChanged(nameof(HasExtraOutputTargets));
    }

    /// <summary>复制控制台文本到系统剪贴板或给出降级提示。</summary>
    private async Task CopyConsoleAsync()
    {
        string text = string.Join(Environment.NewLine, ConsoleEntries.Select(entry => "[" + entry.Time + "] " + entry.Level + " " + entry.Message));
        if (mCopyTextAsync == null)
        {
            StatusDetailText = "当前没有可用剪贴板服务，请直接选择控制台文本。";
            return;
        }

        await mCopyTextAsync(text);
        AppendConsole("SUCCESS", "控制台日志已复制到剪贴板。", false);
    }

    /// <summary>清空本轮控制台日志。</summary>
    private void ClearConsole()
    {
        ConsoleEntries.Clear();
        IsConsoleExpanded = false;
        StatusText = "控制台已清空";
    }

    /// <summary>响应控制台集合变化，只刷新摘要，不抢夺用户的抽屉展开状态。</summary>
    private void OnConsoleEntriesChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(IsConsoleEmpty));
        OnPropertyChanged(nameof(ConsoleCountText));
        OnPropertyChanged(nameof(ConsoleErrorCount));
        OnPropertyChanged(nameof(ConsoleSummaryText));
    }

    /// <summary>响应预览表集合变化，刷新任务页可用性和状态摘要。</summary>
    private void OnPreviewTablesChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(HasPreviewTables));
        OnPropertyChanged(nameof(PreviewCountText));
        OnPropertyChanged(nameof(PreviewStatusText));
        OnPropertyChanged(nameof(FilteredPreviewTables));
        OnPropertyChanged(nameof(ConsoleSummaryText));
    }

    /// <summary>通过跨平台目录选择器设置 Luban 工作目录。</summary>
    private async Task BrowseLubanWorkDirAsync() => await PickFolderAsync("选择 Luban 工作目录", LubanWorkDir, false, path =>
    {
        LubanWorkDir = path;
        string currentConfig = ResolveInputPath(ConfigPath);
        if (string.IsNullOrWhiteSpace(ConfigPath) || !File.Exists(currentConfig))
        {
            ConfigPath = ToProjectRelativePath(Path.Combine(ResolveInputPath(path), "luban.conf"));
        }
    });

    /// <summary>通过文件选择器设置实际 Luban.dll 路径。</summary>
    private async Task BrowseLubanExecutableAsync()
    {
        if (mLubanFilePicker == null)
        {
            StatusDetailText = "当前窗口没有可用的 Luban.dll 文件选择器。";
            return;
        }

        string suggested = TableKitPathUtilities.FindPickerStartDirectory(mProjectRoot, LubanExecutablePath, true);
        string? selected = await mLubanFilePicker.PickLubanDllAsync("选择 Luban.dll", suggestedPath: suggested);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            LubanExecutablePath = ToProjectRelativePath(selected);
        }
    }

    /// <summary>选择正式数据输出目录。</summary>
    private async Task BrowseOutputDataAsync() => await PickFolderAsync("选择 TableKit 数据输出目录", OutputDataDir, false, path => OutputDataDir = path);

    /// <summary>选择正式代码输出目录。</summary>
    private async Task BrowseOutputCodeAsync() => await PickFolderAsync("选择 TableKit 代码输出目录", OutputCodeDir, false, path => OutputCodeDir = path);

    /// <summary>选择编辑器读取的数据目录。</summary>
    private async Task BrowseEditorDataAsync() => await PickFolderAsync("选择 TableKit 编辑器数据目录", EditorDataPath, false, path => EditorDataPath = path);

    /// <summary>从字段当前路径打开选择器，并转换为项目相对路径。</summary>
    /// <param name="title">原生目录选择器标题。</param>
    /// <param name="currentPath">字段当前显示的路径。</param>
    /// <param name="isFilePath">字段是否指向文件。</param>
    /// <param name="apply">接收相对路径的字段更新回调。</param>
    private async Task PickFolderAsync(string title, string currentPath, bool isFilePath, Action<string> apply)
    {
        if (mFolderPicker == null) { StatusDetailText = "当前窗口没有可用的目录选择器。"; return; }
        string suggested = TableKitPathUtilities.FindPickerStartDirectory(mProjectRoot, currentPath, isFilePath);
        string? selected = await mFolderPicker.PickFolderAsync(title, suggestedPath: suggested);
        if (!string.IsNullOrWhiteSpace(selected)) apply(ToProjectRelativePath(selected));
    }

    /// <summary>打开 Luban Datas 目录；不存在时打开工作目录。</summary>
    private Task OpenConfigDirectoryAsync()
    {
        string workDir = ResolveInputPath(LubanWorkDir);
        string target = Directory.Exists(Path.Combine(workDir, "Datas")) ? Path.Combine(workDir, "Datas") : workDir;
        if (!Directory.Exists(target)) { StatusDetailText = "Luban 配置目录不存在: " + target; return Task.CompletedTask; }
        ProcessStartInfo startInfo = new() { UseShellExecute = false, CreateNoWindow = true };
        if (OperatingSystem.IsWindows()) { startInfo.FileName = "explorer.exe"; startInfo.ArgumentList.Add(target); }
        else if (OperatingSystem.IsMacOS()) { startInfo.FileName = "open"; startInfo.ArgumentList.Add(target); }
        else { startInfo.FileName = "xdg-open"; startInfo.ArgumentList.Add(target); }
        try { Process.Start(startInfo); StatusDetailText = "已打开配置表目录: " + target; }
        catch (Exception exception) { StatusDetailText = "打开配置表目录失败: " + exception.Message; }
        return Task.CompletedTask;
    }

    /// <summary>将 Application 结果投影到状态、日志和任务工作区。</summary>
    /// <param name="result">TableKit 操作结果。</param>
    /// <param name="showDataOnSuccess">成功后是否进入数据浏览任务。</param>
    internal void ApplyOperationResult(TableKitOperationResult result, bool showDataOnSuccess)
    {
        if (!string.IsNullOrWhiteSpace(result.Log)) AppendConsoleLines(result.Succeeded ? "INFO" : "ERROR", result.Log);
        TablesType = result.Contract?.TablesType ?? "未解析";
        DataExtension = result.Contract?.DataExtension ?? "未解析";
        PreviewDirectory = result.PreviewDirectory;
        if (showDataOnSuccess) ApplyPreviewTables(result.PreviewTables);
        StatusText = result.Succeeded ? "成功" : "失败";
        StatusDetailText = result.Succeeded
            ? (result.Contract == null ? "操作完成。" : result.Contract.TablesType + " · " + result.Contract.DataTarget)
            : string.Join("; ", result.Diagnostics);
        CommandPreviewText = CreateCommandPreview();
        RefreshEnvironment();
        if (result.Succeeded)
        {
            if (showDataOnSuccess && HasPreviewTables) SelectedWorkspaceIndex = 1;
            IsConsoleExpanded = false;
        }
        else
        {
            IsConsoleExpanded = true;
        }
    }

    /// <summary>替换预览表并强制选中第一张表和第一条记录。</summary>
    /// <param name="tables">验证阶段生成的预览表。</param>
    private void ApplyPreviewTables(IReadOnlyList<TableKitPreviewTable> tables)
    {
        PreviewTables.Clear();
        foreach (TableKitPreviewTable table in tables) PreviewTables.Add(new TableKitPreviewTableViewModel(table));
        SelectedPreviewTable = PreviewTables.FirstOrDefault();
    }

    /// <summary>清空预览及其三级选择状态。</summary>
    private void ClearPreview()
    {
        SelectedPreviewRecord = null;
        SelectedPreviewTable = null;
        PreviewTables.Clear();
        PreviewSearch = string.Empty;
    }

    /// <summary>把多行 Luban 输出加入有界控制台集合。</summary>
    /// <param name="level">日志级别。</param>
    /// <param name="text">多行日志。</param>
    private void AppendConsoleLines(string level, string text)
    {
        foreach (string line in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)) AppendConsole(level, line.Trim(), false);
    }

    /// <summary>加入一条控制台日志并限制集合长度。</summary>
    /// <param name="level">日志级别。</param>
    /// <param name="message">日志正文。</param>
    /// <param name="notify">是否立即设置操作状态。</param>
    private void AppendConsole(string level, string message, bool notify)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        ConsoleEntries.Add(new TableKitConsoleEntryViewModel(DateTime.Now.ToString("HH:mm:ss"), level, message));
        while (ConsoleEntries.Count > 120) ConsoleEntries.RemoveAt(0);
        if (notify) StatusDetailText = message;
    }

    /// <summary>刷新本地 Luban 配置文件与工具路径可用状态。</summary>
    private void RefreshEnvironment()
    {
        bool configExists = File.Exists(ResolveInputPath(ConfigPath));
        bool executableExists = File.Exists(ResolveInputPath(LubanExecutablePath));
        LubanAvailable = configExists && executableExists;
        OnPropertyChanged(nameof(LubanUnavailable));
        LubanStatusText = LubanAvailable ? "Luban ON" : "Luban OFF";
        EnvironmentMessage = LubanAvailable
            ? "已找到 luban.conf 和 Luban 工具，可以执行验证与生成。"
            : "请确认工作目录包含 luban.conf，并配置 Luban.dll 或可执行文件路径。";
        CommandPreviewText = CreateCommandPreview();
        OnPropertyChanged(nameof(LoaderText));
    }

    /// <summary>构建当前配置的命令预览，不执行任何外部进程。</summary>
    /// <returns>便于复制诊断的命令行。</returns>
    private string CreateCommandPreview()
    {
        string executable = string.IsNullOrWhiteSpace(LubanExecutablePath) ? "dotnet Luban.dll" : LubanExecutablePath;
        return executable + " -t " + TargetName + " --conf luban.conf -c " + CodeTarget + " -d " + DataTarget;
    }

    /// <summary>把页面字段转换为 Application 不可变选项。</summary>
    /// <returns>当前 TableKit 选项。</returns>
    private TableKitOptions CreateOptions()
    {
        return new TableKitOptions
        {
            ProjectRoot = mProjectRoot,
            LubanConfigPath = ResolveInputPath(ConfigPath),
            LubanExecutablePath = ResolveInputPath(LubanExecutablePath),
            LubanWorkDir = ResolveInputPath(LubanWorkDir),
            TargetName = string.IsNullOrWhiteSpace(TargetName) ? "client" : TargetName,
            CodeTarget = string.IsNullOrWhiteSpace(CodeTarget) ? "cs-bin" : CodeTarget,
            DataTarget = string.IsNullOrWhiteSpace(DataTarget) ? "bin" : DataTarget,
            OutputCodeDir = OutputCodeDir,
            OutputDataDir = OutputDataDir,
            IsAddressable = IsAddressable,
            RuntimePathPattern = mRuntimePathPatternIsCustom ? RuntimePathPattern : string.Empty,
            CustomEditorDataPath = CustomEditorDataPath,
            EditorDataPath = EditorDataPath,
            UseAsyncLoading = UseAsyncLoading,
            UseRawResourceLoading = UseRawResourceLoading,
            GenerateExternalTypeUtil = GenerateExternalTypeUtil,
            UseAssemblyDefinition = UseAssemblyDefinition,
            AssemblyName = AssemblyName,
            ExtraOutputTargets = ExtraOutputTargets.Select(output => output.ToModel()).ToArray()
        };
    }

    /// <summary>应用配置对象并重建额外输出目标集合。</summary>
    /// <param name="options">待应用配置。</param>
    private void ApplyOptions(TableKitOptions options)
    {
        mConfigPath = ToProjectRelativePath(options.LubanConfigPath);
        mLubanExecutablePath = ToProjectRelativePath(options.LubanExecutablePath);
        mLubanWorkDir = ToProjectRelativePath(options.LubanWorkDir);
        mTargetName = string.IsNullOrWhiteSpace(options.TargetName) ? "client" : options.TargetName;
        mCodeTarget = string.IsNullOrWhiteSpace(options.CodeTarget) ? "cs-bin" : options.CodeTarget;
        mDataTarget = string.IsNullOrWhiteSpace(options.DataTarget) ? "bin" : options.DataTarget;
        mOutputCodeDir = ToProjectRelativePath(options.OutputCodeDir);
        mOutputDataDir = ToProjectRelativePath(options.OutputDataDir);
        mIsAddressable = options.IsAddressable;
        mRuntimePathPatternIsCustom = !string.IsNullOrWhiteSpace(options.RuntimePathPattern);
        mRuntimePathPattern = mRuntimePathPatternIsCustom
            ? options.RuntimePathPattern
            : ResolveInferredRuntimePathPattern();
        mCustomEditorDataPath = options.CustomEditorDataPath;
        mEditorDataPath = mCustomEditorDataPath
            ? ToProjectRelativePath(options.EditorDataPath)
            : mOutputDataDir;
        mUseAsyncLoading = options.UseAsyncLoading;
        mUseRawResourceLoading = options.UseRawResourceLoading;
        mGenerateExternalTypeUtil = options.GenerateExternalTypeUtil;
        mUseAssemblyDefinition = options.UseAssemblyDefinition;
        mAssemblyName = options.AssemblyName;
        ExtraOutputTargets.Clear();
        foreach (TableKitExtraOutput output in options.ExtraOutputTargets)
        {
            ExtraOutputTargets.Add(new TableKitExtraOutputViewModel(
                output,
                RemoveExtraOutput,
                TargetOptions,
                ExtraCodeTargetOptions,
                DataTargetOptions,
                mProjectRoot,
                mFolderPicker));
        }
        OnPropertyChanged(nameof(HasExtraOutputTargets));
        RaiseConfigurationPropertiesChanged();
    }

    /// <summary>创建绑定当前项目根的默认配置。</summary>
    /// <returns>默认 TableKit 配置。</returns>
    private TableKitOptions CreateDefaultOptions()
    {
        return new TableKitOptions
        {
            ProjectRoot = mProjectRoot,
            LubanConfigPath = "Luban/MiniTemplate/luban.conf",
            LubanWorkDir = "Luban/MiniTemplate",
            LubanExecutablePath = "Luban/Tools/Luban/Luban.dll",
            TargetName = "client"
        };
    }

    /// <summary>将相对输入解析到当前项目根，绝对路径保持不变。</summary>
    /// <param name="path">输入路径。</param>
    /// <returns>绝对路径。</returns>
    private string ResolveInputPath(string path)
    {
        return TableKitPathUtilities.Resolve(mProjectRoot, path);
    }

    /// <summary>将项目内绝对路径折叠为稳定的项目相对路径。</summary>
    private string ToProjectRelativePath(string path)
    {
        return TableKitPathUtilities.ToRelative(mProjectRoot, path);
    }

    /// <summary>重新从当前宿主和数据输出目录推导路径模板。</summary>
    private void RefreshInferredRuntimePathPattern()
    {
        string inferred = ResolveInferredRuntimePathPattern();
        if (mRuntimePathPattern == inferred) return;
        mRuntimePathPattern = inferred;
        OnPropertyChanged(nameof(RuntimePathPattern));
    }

    /// <summary>关闭自定义路径时，让编辑器数据目录始终跟随当前数据输出目录。</summary>
    private void RefreshInferredEditorDataPath()
    {
        if (mEditorDataPath == mOutputDataDir) return;
        mEditorDataPath = mOutputDataDir;
        OnPropertyChanged(nameof(EditorDataPath));
    }

    /// <summary>尝试从当前输出目录推导运行时路径；无法推导时返回空值供用户填写。</summary>
    /// <returns>规范化路径模板，无法推导时为空。</returns>
    private string ResolveInferredRuntimePathPattern()
    {
        try
        {
            TableKitRuntimeLocation location = mResourceLocationResolver.Resolve(new TableKitOptions
            {
                ProjectRoot = mProjectRoot,
                LubanConfigPath = ResolveInputPath(ConfigPath),
                OutputDataDir = OutputDataDir
            });
            return location.PathPattern;
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            return string.Empty;
        }
    }

    /// <summary>解析当前定位摘要；无效配置保留简短错误供用户在生成前修正。</summary>
    /// <returns>规范化定位值或验证错误。</returns>
    private string ResolveRuntimeLocationPreview()
    {
        try
        {
            TableKitRuntimeLocation location = mResourceLocationResolver.Resolve(CreateOptions());
            return location.IsAddressable ? "按 Luban 表名寻址" : location.PathPattern;
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            return exception.Message;
        }
    }

    /// <summary>向选项集合追加非空且不重复的值。</summary>
    /// <param name="options">目标集合。</param>
    /// <param name="value">候选值。</param>
    private static void AddOption(ObservableCollection<string> options, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !options.Contains(value, StringComparer.Ordinal)) options.Add(value);
    }

    /// <summary>通知从配置对象投影出的绑定属性已更新。</summary>
    private void RaiseConfigurationPropertiesChanged()
    {
        OnPropertyChanged(nameof(ConfigPath));
        OnPropertyChanged(nameof(LubanExecutablePath));
        OnPropertyChanged(nameof(LubanWorkDir));
        OnPropertyChanged(nameof(TargetName));
        OnPropertyChanged(nameof(CodeTarget));
        OnPropertyChanged(nameof(DataTarget));
        OnPropertyChanged(nameof(OutputCodeDir));
        OnPropertyChanged(nameof(OutputDataDir));
        OnPropertyChanged(nameof(IsAddressable));
        OnPropertyChanged(nameof(RuntimePathPattern));
        OnPropertyChanged(nameof(IsRuntimePathVisible));
        OnPropertyChanged(nameof(RuntimeLocationPreview));
        OnPropertyChanged(nameof(CustomEditorDataPath));
        OnPropertyChanged(nameof(EditorDataPath));
        OnPropertyChanged(nameof(UseAsyncLoading));
        OnPropertyChanged(nameof(UseRawResourceLoading));
        OnPropertyChanged(nameof(GenerateExternalTypeUtil));
        OnPropertyChanged(nameof(UseAssemblyDefinition));
        OnPropertyChanged(nameof(AssemblyName));
    }
}
