using Avalonia.Platform.Storage;

namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 通过 Avalonia IStorageProvider 打开只选择 Luban.dll 的跨平台原生文件对话框。
/// </summary>
public sealed class AvaloniaTableKitLubanFilePicker : ITableKitLubanFilePicker
{
    private static readonly FilePickerFileType sLubanDllFileType = new("Luban.dll")
    {
        Patterns = new[] { "Luban.dll" }
    };

    private readonly Func<IStorageProvider?> mStorageProviderAccessor;

    /// <summary>
    /// 创建 Luban.dll 文件选择器；provider 在窗口初始化完成后按需获取。
    /// </summary>
    /// <param name="storageProviderAccessor">当前窗口 StorageProvider 访问函数。</param>
    public AvaloniaTableKitLubanFilePicker(Func<IStorageProvider?> storageProviderAccessor)
    {
        mStorageProviderAccessor = storageProviderAccessor
            ?? throw new ArgumentNullException(nameof(storageProviderAccessor));
    }

    /// <summary>
    /// 打开只显示 Luban.dll 的单文件选择器，并返回用户选择的本地绝对路径。
    /// </summary>
    /// <param name="title">原生对话框标题。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <param name="suggestedPath">已配置 Luban.dll 所在目录。</param>
    /// <returns>用户选中的 Luban.dll；取消或平台不支持时返回 null。</returns>
    public async Task<string?> PickLubanDllAsync(
        string title,
        CancellationToken cancellationToken = default,
        string? suggestedPath = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IStorageProvider? provider = mStorageProviderAccessor();
        if (provider == null || !provider.CanOpen)
        {
            return null;
        }

        IStorageFolder? suggestedStartLocation = await ResolveSuggestedStartLocationAsync(provider, suggestedPath);
        IReadOnlyList<IStorageFile> files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[] { sLubanDllFileType },
            SuggestedStartLocation = suggestedStartLocation
        });
        cancellationToken.ThrowIfCancellationRequested();
        return files.Count == 0 ? null : files[0].Path.LocalPath;
    }

    /// <summary>
    /// 将已存在的建议目录转换为 Avalonia StorageProvider 所需的目录对象；平台拒绝路径时静默回退默认位置。
    /// </summary>
    /// <param name="provider">当前窗口的存储服务。</param>
    /// <param name="suggestedPath">候选起始目录。</param>
    /// <returns>可用的起始目录；不可用时返回 null。</returns>
    private static async Task<IStorageFolder?> ResolveSuggestedStartLocationAsync(IStorageProvider provider, string? suggestedPath)
    {
        if (string.IsNullOrWhiteSpace(suggestedPath) || !Path.IsPathRooted(suggestedPath))
        {
            return null;
        }

        try
        {
            return await provider.TryGetFolderFromPathAsync(suggestedPath);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
