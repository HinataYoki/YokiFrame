using System.Collections.ObjectModel;
using System.Windows.Input;
using YokiFrame.Tooling.Application.Models.SaveKit;
using YokiFrame.Tooling.Application.Services.SaveKit;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 SaveKit 存储根、子路径和草稿组合。</summary>
public sealed partial class SaveKitPageViewModel
{
    private void RefreshStorageRootOptions()
    {
        string runtimeLabel = EngineId.Contains("godot", StringComparison.OrdinalIgnoreCase)
            ? GetString("String.SaveKit.GodotUserData", "Godot 用户目录 (OS.GetUserDataDir)")
            : GetString("String.SaveKit.UnityPersistentData", "Unity 持久化目录 (Application.persistentDataPath)");
        string projectLabel = GetString("String.SaveKit.ProjectDirectory", "项目目录");
        string customLabel = GetString("String.SaveKit.CustomDirectory", "自定义绝对路径");
        StorageRootOptions.Clear();
        StorageRootOptions.Add(runtimeLabel);
        StorageRootOptions.Add(projectLabel);
        StorageRootOptions.Add(customLabel);
        OnPropertyChanged(nameof(StorageRootOptions));
        OnPropertyChanged(nameof(SelectedStorageRoot));
    }

    /// <summary>把已保存的完整路径拆成下拉根类型和用户可编辑部分。</summary>
    private void ProjectStorageDraft(string value)
    {
        string runtimeToken = EngineId.Contains("godot", StringComparison.OrdinalIgnoreCase)
            ? "${userDataDir}"
            : "${persistentDataPath}";
        if (value.StartsWith(runtimeToken, StringComparison.Ordinal))
        {
            mSelectedStorageRoot = STORAGE_ROOT_RUNTIME;
            mStorageSubPath = TrimStorageSeparator(value[runtimeToken.Length..]);
        }
        else if (!Path.IsPathRooted(value))
        {
            mSelectedStorageRoot = STORAGE_ROOT_PROJECT;
            mStorageSubPath = TrimStorageSeparator(value);
        }
        else
        {
            mSelectedStorageRoot = STORAGE_ROOT_CUSTOM;
            mStorageSubPath = value;
        }
        OnPropertyChanged(nameof(SelectedStorageRoot));
        OnPropertyChanged(nameof(StorageSubPath));
    }

    /// <summary>应用磁盘配置并同步下拉框草稿。</summary>
    private void ApplyStoragePath(string value)
    {
        mUpdatingStorageDraft = true;
        try
        {
            StoragePath = value;
        }
        finally
        {
            mUpdatingStorageDraft = false;
        }
        ProjectStorageDraft(value);
    }

    /// <summary>按当前根类型重新组合持久化路径。</summary>
    private void ComposeStorageDraft()
    {
        if (mUpdatingStorageDraft || string.IsNullOrWhiteSpace(EngineId)) return;
        string value = mSelectedStorageRoot switch
        {
            STORAGE_ROOT_PROJECT => TrimStorageSeparator(mStorageSubPath),
            STORAGE_ROOT_CUSTOM => mStorageSubPath.Trim(),
            _ => (EngineId.Contains("godot", StringComparison.OrdinalIgnoreCase) ? "${userDataDir}" : "${persistentDataPath}")
                 + "/" + TrimStorageSeparator(mStorageSubPath)
        };
        mUpdatingStorageDraft = true;
        try { StoragePath = value; }
        finally { mUpdatingStorageDraft = false; }
    }

    /// <summary>将下拉框展示文本转换为稳定的内部根类型。</summary>
    private string NormalizeStorageRoot(string? value)
    {
        int index = StorageRootOptions.IndexOf(value ?? string.Empty);
        return index switch
        {
            1 => STORAGE_ROOT_PROJECT,
            2 => STORAGE_ROOT_CUSTOM,
            _ => STORAGE_ROOT_RUNTIME
        };
    }

    /// <summary>获取当前根类型在当前语言下的下拉框展示文本。</summary>
    private string GetStorageRootDisplay(string root)
    {
        int index = root switch
        {
            STORAGE_ROOT_PROJECT => 1,
            STORAGE_ROOT_CUSTOM => 2,
            _ => 0
        };
        return index < StorageRootOptions.Count ? StorageRootOptions[index] : string.Empty;
    }

    /// <summary>规范化根目录拼接所需的分隔符。</summary>
    private static string TrimStorageSeparator(string value)
    {
        return value.Trim().Trim('/', '\\');
    }

}
