using Avalonia;
using Avalonia.Controls;

namespace YokiFrame.Workbench.Avalonia.Services;

/// <summary>
/// 表示 Workbench 窗口启动时应使用的尺寸、位置和启动定位策略。
/// </summary>
/// <param name="width">窗口宽度，单位为 Avalonia DIP。</param>
/// <param name="height">窗口高度，单位为 Avalonia DIP。</param>
/// <param name="position">窗口左上角屏幕像素坐标；为空时交给 Avalonia 根据启动策略定位。</param>
/// <param name="startupLocation">Avalonia 窗口启动定位策略。</param>
public sealed class WindowPlacement(double width, double height, PixelPoint? position, WindowStartupLocation startupLocation)
{
    /// <summary>
    /// 获取窗口宽度，单位为 Avalonia DIP。
    /// </summary>
    public double Width { get; } = width;

    /// <summary>
    /// 获取窗口高度，单位为 Avalonia DIP。
    /// </summary>
    public double Height { get; } = height;

    /// <summary>
    /// 获取窗口左上角屏幕像素坐标；为空时不手动设置位置。
    /// </summary>
    public PixelPoint? Position { get; } = position;

    /// <summary>
    /// 获取 Avalonia 窗口启动定位策略。
    /// </summary>
    public WindowStartupLocation StartupLocation { get; } = startupLocation;
}
