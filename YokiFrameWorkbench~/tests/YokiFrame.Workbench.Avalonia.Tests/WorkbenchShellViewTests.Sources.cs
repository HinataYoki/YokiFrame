using YokiFrame.Workbench.Avalonia;

using YokiFrame.Workbench.Avalonia.Platform;

namespace YokiFrame.Workbench.Avalonia.Tests;



/// <summary>

/// 提供 Windows owner 样式契约使用的平台常量。样式计算走 internal 测试缝。

/// </summary>

public sealed partial class WorkbenchShellViewTests

{

    private const long WS_CHILD = 0x40000000L;

    private const long WS_POPUP = 0x80000000L;

    private const long WS_CAPTION = 0x00C00000L;

    private const long WS_THICKFRAME = 0x00040000L;

    private const long WS_SYSMENU = 0x00080000L;

    private const long WS_MINIMIZEBOX = 0x00020000L;

    private const long WS_MAXIMIZEBOX = 0x00010000L;

    private const long WS_EX_APPWINDOW = 0x00040000L;

    private const long WS_EX_TOOLWINDOW = 0x00000080L;



    /// <summary>

    /// 调用 internal 测试缝计算 owner 窗口样式。

    /// </summary>

    /// <param name="currentStyle">当前 Win32 window style。</param>

    /// <returns>作为 Unity owned tool window 显示时应使用的 Win32 window style。</returns>

    private static long InvokeCreateOwnedWindowStyle(long currentStyle)

    {

        return WindowsWorkbenchWindowHost.CreateOwnedWindowStyle(currentStyle);

    }



    /// <summary>

    /// 调用 internal 测试缝计算 owner 扩展样式。

    /// </summary>

    /// <param name="currentExStyle">当前 Win32 extended window style。</param>

    /// <returns>作为 Unity owned tool window 显示时应使用的 extended style。</returns>

    private static long InvokeCreateOwnedToolWindowExStyle(long currentExStyle)

    {

        return WindowsWorkbenchWindowHost.CreateOwnedToolWindowExStyle(currentExStyle);

    }

}

