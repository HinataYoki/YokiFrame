using Avalonia;

namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 表示可用于恢复窗口位置的屏幕工作区。
/// </summary>
/// <param name="bounds">工作区像素矩形。</param>
public sealed class WindowWorkArea(PixelRect bounds)
{
    /// <summary>
    /// 获取屏幕工作区像素矩形。
    /// </summary>
    public PixelRect Bounds { get; } = bounds;
}
