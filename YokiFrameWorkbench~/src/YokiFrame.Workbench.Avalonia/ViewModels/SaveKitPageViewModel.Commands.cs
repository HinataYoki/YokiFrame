using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 SaveKit 页面命令、异步 IO 和生命周期操作。</summary>
public sealed partial class SaveKitPageViewModel
{
    /// <summary>切换 engine 并加载其项目配置。</summary>
    /// <param name="engineId">新的 engine 标识。</param>
    public void SetEngine(string engineId)
    {
        if (mIsDisposed || string.Equals(EngineId, engineId, StringComparison.Ordinal))
        {
            return;
        }

        EngineId = engineId ?? string.Empty;
        ResetRuntimeState();
        _ = RefreshAsync();
    }

    /// <summary>释放页面异步资源。</summary>
    public void Dispose()
    {
        if (mIsDisposed)
        {
            return;
        }

        mIsDisposed = true;
        WorkbenchI18nService.Instance.CultureChanged -= OnCultureChanged;
        mLifetimeCancellation.Cancel();
        mLifetimeCancellation.Dispose();
    }

    /// <summary>重新加载磁盘配置和目录元信息。</summary>
    private async Task RefreshAsync()
    {
        if (!CanRefresh() || mService == null || string.IsNullOrWhiteSpace(EngineId))
        {
            return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        OnPropertyChanged(nameof(HasError));
        try
        {
            ApplySettings(await Task.Run(() => mService.Load(EngineId), mLifetimeCancellation.Token), true);
        }
        catch (OperationCanceledException) when (mLifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorText = string.Format(
                GetString("String.SaveKit.LoadFailedTemplate", "读取 SaveKit 配置失败: {0}"), exception.Message);
            OnPropertyChanged(nameof(HasError));
            SetStatus(GetString("String.SaveKit.LoadFailedShort", "配置读取失败"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>保存路径和扩展名，并在成功后刷新指纹。</summary>
    private async Task SaveAsync()
    {
        if (!CanSave() || mService == null)
        {
            return;
        }

        IsBusy = true;
        ErrorText = string.Empty;
        OnPropertyChanged(nameof(HasError));
        SetStatus(GetString("String.SaveKit.Saving", "正在保存 SaveKit 配置..."));
        try
        {
            var result = await mService.SaveAsync(EngineId, StoragePath, FileExtension, Fingerprint, mLifetimeCancellation.Token);
            if (result.Conflict)
            {
                ApplySettings(result.Settings, false);
                ErrorText = result.ErrorMessage;
                OnPropertyChanged(nameof(HasError));
                SetStatus(GetString("String.SaveKit.SaveConflict", "保存冲突，草稿已保留"));
            }
            else if (result.Saved)
            {
                ApplySettings(result.Settings, true);
                SetStatus(GetString("String.SaveKit.Saved", "SaveKit 配置已保存"));
            }
            else
            {
                ErrorText = result.ErrorMessage;
                OnPropertyChanged(nameof(HasError));
            }
        }
        catch (OperationCanceledException) when (mLifetimeCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ErrorText = string.Format(
                GetString("String.SaveKit.SaveFailedTemplate", "保存 SaveKit 配置失败: {0}"), exception.Message);
            OnPropertyChanged(nameof(HasError));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>通过系统目录选择器设置存档目录草稿。</summary>
    private async Task BrowseFolderAsync()
    {
        if (!CanBrowseFolder() || mFolderPicker == null)
        {
            return;
        }

        string? selected = await mFolderPicker.PickFolderAsync(
            GetString("String.SaveKit.PickFolderTitle", "选择 SaveKit 存档目录"),
            mLifetimeCancellation.Token, ResolvedStoragePath);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            StoragePath = selected;
        }
    }

    /// <summary>调用宿主平台打开当前可解析的存档目录。</summary>
    private async Task OpenDirectoryAsync()
    {
        if (!CanOpenDirectory() || mOpenDirectoryAsync == null)
        {
            return;
        }

        try
        {
            await mOpenDirectoryAsync(ResolvedStoragePath);
        }
        catch (Exception exception)
        {
            ErrorText = string.Format(
                GetString("String.SaveKit.OpenDirectoryFailedTemplate", "打开存档目录失败: {0}"), exception.Message);
            OnPropertyChanged(nameof(HasError));
        }
    }

    /// <summary>恢复当前引擎默认配置，不立即写入磁盘。</summary>
    private void Reset()
    {
        if (mBaseline == null)
        {
            return;
        }

        StoragePath = EngineId.Contains("godot", StringComparison.OrdinalIgnoreCase)
            ? "${userDataDir}/YokiFrame/Saves"
            : "${persistentDataPath}/YokiFrame/Saves";
        FileExtension = ".yoki";
        SetStatus(GetString("String.SaveKit.ResetDefaultsMessage", "已恢复默认草稿，保存后生效"));
    }

    /// <summary>判断刷新命令是否可执行。</summary>
    private bool CanRefresh()
    {
        return !mIsDisposed && !IsBusy && mService != null && !string.IsNullOrWhiteSpace(EngineId);
    }

    /// <summary>判断保存命令是否可执行。</summary>
    private bool CanSave()
    {
        return CanRefresh() && IsSupported && IsDirty;
    }

    /// <summary>判断目录选择命令是否可执行。</summary>
    private bool CanBrowseFolder()
    {
        return !mIsDisposed && !IsBusy && mFolderPicker != null;
    }

    /// <summary>判断当前是否可以打开已经解析且存在的存档目录。</summary>
    private bool CanOpenDirectory()
    {
        return !mIsDisposed
               && !IsBusy
               && mOpenDirectoryAsync != null
               && !string.IsNullOrWhiteSpace(ResolvedStoragePath)
               && Directory.Exists(ResolvedStoragePath);
    }

    /// <summary>判断恢复默认命令是否可执行。</summary>
    private bool CanReset()
    {
        return !mIsDisposed && mBaseline != null;
    }
}
