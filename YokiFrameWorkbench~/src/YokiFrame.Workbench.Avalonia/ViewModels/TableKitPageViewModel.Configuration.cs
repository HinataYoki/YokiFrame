using System.Collections.ObjectModel;
using YokiFrame.Tooling.Application.Models.TableKit;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 TableKit 配置保存、恢复和路径投影。</summary>
public sealed partial class TableKitPageViewModel
{
    /// <summary>保存当前页面配置到项目 ProjectSettings。</summary>
    private void SaveConfiguration()
    {
        if (!TryPersistConfiguration())
        {
            return;
        }

        AppendConsole("SUCCESS", GetString(ConfigSavedKey, "TableKit 配置已保存到当前项目。"), false);
        SetStatus(GetString(SavedDetailKey, "已保存"));
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
            SetStatus(GetString(SaveFailedShortKey, "保存失败"));
            StatusDetailText = exception.Message;
            AppendConsole("ERROR", string.Format(GetString(SaveFailedTemplateKey, "TableKit 配置保存失败: {0}"), exception.Message), false);
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
        if (!TryPersistConfiguration())
        {
            return;
        }

        AppendConsole("INFO", GetString(ResetDoneKey, "已还原默认 TableKit 配置。"), false);
        SetStatus(GetString(ResetDetailKey, "已还原默认"));
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

    /// <summary>把页面字段转换为 Application 不可变选项。</summary>
    /// <returns>当前 TableKit 选项。</returns>
    private TableKitOptions CreateOptions()
    {
        return new TableKitOptions
        {
            ProjectRoot = mProjectRoot,
            LubanConfigPath = ResolveInputPath(ConfigPath),
            LubanExecutablePath = ResolveInputPath(LubanExecutablePath),
            LubanAgentExecutablePath = ResolveOptionalInputPath(LubanAgentExecutablePath),
            LubanMcpExecutablePath = ResolveOptionalInputPath(LubanMcpExecutablePath),
            LubanSkillsPath = ResolveOptionalInputPath(LubanSkillsPath),
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
        mLubanAgentExecutablePath = ToProjectRelativePath(options.LubanAgentExecutablePath);
        mLubanMcpExecutablePath = ToProjectRelativePath(options.LubanMcpExecutablePath);
        mLubanSkillsPath = ToProjectRelativePath(options.LubanSkillsPath);
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

    /// <summary>解析可选工具路径；未发现时保持空文本，避免把项目根误写成工具路径。</summary>
    /// <param name="path">可选的项目相对或绝对路径。</param>
    /// <returns>规范化绝对路径或空文本。</returns>
    private string ResolveOptionalInputPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : ResolveInputPath(path);
    }

    /// <summary>将项目内绝对路径折叠为稳定的项目相对路径。</summary>
    /// <param name="path">项目内绝对路径或已有相对路径。</param>
    /// <returns>项目相对路径。</returns>
    private string ToProjectRelativePath(string path)
    {
        return TableKitPathUtilities.ToRelative(mProjectRoot, path);
    }

    /// <summary>重新从当前宿主和数据输出目录推导路径模板。</summary>
    private void RefreshInferredRuntimePathPattern()
    {
        string inferred = ResolveInferredRuntimePathPattern();
        if (mRuntimePathPattern == inferred)
        {
            return;
        }

        mRuntimePathPattern = inferred;
        OnPropertyChanged(nameof(RuntimePathPattern));
    }

    /// <summary>关闭自定义路径时，让编辑器数据目录始终跟随当前数据输出目录。</summary>
    private void RefreshInferredEditorDataPath()
    {
        if (mEditorDataPath == mOutputDataDir)
        {
            return;
        }

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
            return location.IsAddressable
                ? GetString(AddressableModeKey, "按 Luban 表名寻址")
                : location.PathPattern;
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
        if (!string.IsNullOrWhiteSpace(value) && !options.Contains(value, StringComparer.Ordinal))
        {
            options.Add(value);
        }
    }

    /// <summary>通知从配置对象投影出的绑定属性已更新。</summary>
    private void RaiseConfigurationPropertiesChanged()
    {
        OnPropertyChanged(nameof(ConfigPath));
        OnPropertyChanged(nameof(LubanExecutablePath));
        OnPropertyChanged(nameof(LubanAgentExecutablePath));
        OnPropertyChanged(nameof(AgentAvailable));
        OnPropertyChanged(nameof(LubanMcpExecutablePath));
        OnPropertyChanged(nameof(LubanSkillsPath));
        OnPropertyChanged(nameof(OptionalLubanToolsSummary));
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
        OnPropertyChanged(nameof(UseRawResourceLoading));
        OnPropertyChanged(nameof(GenerateExternalTypeUtil));
        OnPropertyChanged(nameof(UseAssemblyDefinition));
        OnPropertyChanged(nameof(AssemblyName));
    }
}
