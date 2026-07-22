using Avalonia.Platform.Storage;

namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 通过 Avalonia IStorageProvider 打开跨平台原生目录选择器。
/// </summary>
public sealed class AvaloniaInstallerFolderPicker : IInstallerFolderPicker
{
    private readonly Func<IStorageProvider?> mStorageProviderAccessor;

    /// <summary>
    /// 创建目录选择器；provider 在窗口初始化完成后按需获取。
    /// </summary>
    /// <param name="storageProviderAccessor">当前窗口 StorageProvider 访问函数。</param>
    public AvaloniaInstallerFolderPicker(Func<IStorageProvider?> storageProviderAccessor)
    {
        mStorageProviderAccessor = storageProviderAccessor
            ?? throw new ArgumentNullException(nameof(storageProviderAccessor));
    }

    /// <summary>
    /// 打开只允许选择一个目录的原生对话框，并返回本地绝对路径。
    /// </summary>
    /// <param name="title">原生对话框标题。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <returns>用户选中的目录；取消或平台不支持时返回 null。</returns>
    public async Task<string?> PickFolderAsync(
        string title,
        CancellationToken cancellationToken = default,
        string? suggestedPath = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var provider = mStorageProviderAccessor();
        if (provider == null || !provider.CanPickFolder)
        {
            return null;
        }

        IStorageFolder? suggestedStartLocation = null;
        if (!string.IsNullOrWhiteSpace(suggestedPath) && Path.IsPathRooted(suggestedPath))
        {
            try { suggestedStartLocation = await provider.TryGetFolderFromPathAsync(suggestedPath); }
            catch (Exception) { suggestedStartLocation = null; }
        }

        var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = suggestedStartLocation
        });
        cancellationToken.ThrowIfCancellationRequested();
        return folders.Count == 0 ? null : folders[0].Path.LocalPath;
    }
}
