using Avalonia.Controls;
using Avalonia.Platform;

namespace YokiFrame.Workbench.Avalonia.Platform;

/// <summary>
/// 统一加载 Workbench / Installer 品牌图标，保证窗口左上角与任务栏使用同一资源。
/// </summary>
public static class BrandIconLoader
{
    private const string BrandIconUri = "avares://YokiFrame.Workbench.Avalonia/Assets/Brand/yoki.png";

    /// <summary>
    /// 将品牌图标应用到指定窗口。
    /// </summary>
    /// <param name="window">目标窗口。</param>
    public static void ApplyTo(Window window)
    {
        using var stream = AssetLoader.Open(new Uri(BrandIconUri));
        window.Icon = new WindowIcon(stream);
    }
}
