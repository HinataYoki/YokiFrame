using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia;

/// <summary>承载 Workbench 项目级窗口矩形与最后页面恢复。</summary>
public sealed partial class WorkbenchWindow
{
    /// <summary>恢复上次关闭时的页面；未知页面继续由 Catalog 回落默认页。</summary>
    private void ApplySavedPage()
    {
        var selectedPage = mWindowStateStore?.LoadSelectedPage();
        if (!string.IsNullOrWhiteSpace(selectedPage))
        {
            mShellViewModel.SelectedPage = selectedPage;
        }
    }

    /// <summary>应用上次保存的窗口位置和尺寸；状态不可用时保持默认居中。</summary>
    private void ApplySavedWindowPlacement()
    {
        var placement = mWindowStateStore?.Load(
            DefaultWindowWidth,
            DefaultWindowHeight,
            DefaultWindowStartupLocation,
            GetCurrentWorkAreas());
        if (placement == null)
        {
            return;
        }

        Width = placement.Width;
        Height = placement.Height;
        WindowStartupLocation = placement.StartupLocation;
        if (placement.Position.HasValue)
        {
            Position = placement.Position.Value;
        }
    }

    /// <summary>保存当前页面，并仅在 normal 状态下更新可恢复窗口矩形。</summary>
    private void SaveWindowState()
    {
        mWindowStateStore?.Save(Position, Width, Height, WindowState, mShellViewModel.SelectedPage);
    }

    /// <summary>读取当前窗口可见屏幕工作区，用于避免恢复到离屏位置。</summary>
    /// <returns>当前平台报告的屏幕工作区集合。</returns>
    private IReadOnlyList<WindowWorkArea> GetCurrentWorkAreas()
    {
        return Screens.All
            .Select(static screen => new WindowWorkArea(screen.WorkingArea))
            .ToArray();
    }
}
