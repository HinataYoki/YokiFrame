namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 抽象 TableKit 对 Luban.dll 的原生文件选择，使 ViewModel 不依赖窗口或平台 API。
/// </summary>
public interface ITableKitLubanFilePicker
{
    /// <summary>
    /// 打开仅允许选择 Luban.dll 的单文件选择器。
    /// </summary>
    /// <param name="title">原生对话框标题。</param>
    /// <param name="cancellationToken">调用方取消令牌。</param>
    /// <param name="suggestedPath">可选的已配置目录；存在时对话框从该目录开始。</param>
    /// <returns>用户选中的 Luban.dll 本地绝对路径；取消时返回 null。</returns>
    Task<string?> PickLubanDllAsync(string title, CancellationToken cancellationToken = default, string? suggestedPath = null);
}
