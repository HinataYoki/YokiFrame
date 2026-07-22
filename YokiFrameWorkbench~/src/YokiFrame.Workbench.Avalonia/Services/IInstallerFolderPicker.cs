namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 抽象 Installer 使用的原生目录选择器，使 ViewModel 不依赖窗口或平台 API。
/// </summary>
public interface IInstallerFolderPicker
{
    /// <summary>
    /// 打开单目录选择器。
    /// </summary>
    /// <param name="title">原生对话框标题。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <param name="suggestedPath">可选的已配置目录；存在时对话框从该目录开始。</param>
    /// <returns>用户选中的本地目录；取消时返回 null。</returns>
    Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default, string? suggestedPath = null);
}
