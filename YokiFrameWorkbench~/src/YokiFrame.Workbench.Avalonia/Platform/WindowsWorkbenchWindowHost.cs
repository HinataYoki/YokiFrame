using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;

namespace YokiFrame.Workbench.Avalonia.Platform;

/// <summary>
/// 负责 Windows 下把 Workbench 绑定为 Unity Editor 的 owned tool window；其它平台保持无副作用。
/// </summary>
internal static partial class WindowsWorkbenchWindowHost
{
    private const string HwndHandleDescriptor = "HWND";
    private const int GWLP_HWNDPARENT = -8;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const long WS_CHILD = 0x40000000L;
    private const long WS_MINIMIZEBOX = 0x00020000L;
    private const long WS_DISABLED = 0x08000000L;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const long WS_EX_APPWINDOW = 0x00040000L;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int SW_RESTORE = 9;
    private const int FOREGROUND_VERIFICATION_ATTEMPTS = 3;
    private const int FOREGROUND_VERIFICATION_DELAY_MS = 16;
    private static readonly IntPtr sHwndTopMost = new(-1);
    private static readonly IntPtr sHwndNoTopMost = new(-2);
    private static IntPtr sHiddenOwnerWindow;

    /// <summary>
    /// 尝试把 Workbench 设为 Unity Editor 的 owned tool window，而不是真正的 child HWND。
    /// </summary>
    /// <param name="window">已经打开并拥有平台句柄的 Avalonia 窗口。</param>
    /// <param name="parentWindowHandle">宿主窗口 HWND。</param>
    /// <returns>成功绑定 owner 时返回 true；平台或句柄不匹配时返回 false。</returns>
    public static bool TryAttach(Window window, IntPtr parentWindowHandle)
    {
        if (!TryGetWindowHandle(window, out var windowHandle) || !IsWindow(parentWindowHandle))
        {
            return false;
        }

        ApplyOwnedWindowStyle(windowHandle);
        SetWindowLongPtr(windowHandle, GWLP_HWNDPARENT, parentWindowHandle);
        return SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    /// <summary>
    /// 显示并提升已存在的 Workbench HWND，确保显式唤醒请求不会停留在 Unity 后方。
    /// </summary>
    /// <param name="window">已经打开的平台窗口。</param>
    /// <returns>真实前台 HWND 验证成功时返回 true。</returns>
    public static bool TryBringToFront(Window window)
    {
        if (!TryGetWindowHandle(window, out var windowHandle))
        {
            return false;
        }

        ShowWindow(windowHandle, SW_RESTORE);
        SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        if (TrySetForegroundWindow(windowHandle))
        {
            return true;
        }

        PulseTopMost(windowHandle);
        return TrySetForegroundWindow(windowHandle);
    }

    /// <summary>
    /// 尝试在窗口关闭前解除 Win32 owner 关系，避免 Windows 在销毁 owned window 时沿 owner 链路抢焦点。
    /// </summary>
    /// <param name="window">即将关闭的 Workbench Avalonia 窗口。</param>
    /// <returns>成功刷新窗口 owner 状态时返回 true。</returns>
    public static bool TryDetach(Window window)
    {
        if (!TryGetWindowHandle(window, out var windowHandle))
        {
            return false;
        }

        var hiddenOwnerWindow = GetHiddenOwnerWindow();
        if (hiddenOwnerWindow == IntPtr.Zero)
        {
            return false;
        }

        SetWindowLongPtr(windowHandle, GWLP_HWNDPARENT, hiddenOwnerWindow);
        return SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
    }

    /// <summary>
    /// 读取 Avalonia 窗口对应的 Windows HWND。
    /// </summary>
    /// <param name="window">Workbench Avalonia 窗口。</param>
    /// <param name="windowHandle">成功时返回窗口 HWND。</param>
    /// <returns>窗口句柄有效时返回 true。</returns>
    private static bool TryGetWindowHandle(Window window, out IntPtr windowHandle)
    {
        windowHandle = IntPtr.Zero;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var platformHandle = window.TryGetPlatformHandle();
        if (platformHandle == null || !IsWindowsHandle(platformHandle) || platformHandle.Handle == IntPtr.Zero)
        {
            return false;
        }

        windowHandle = platformHandle.Handle;
        return IsWindow(windowHandle);
    }

    /// <summary>
    /// 判断 Avalonia 平台句柄是否为 Windows HWND，避免在 macOS/Linux 上误用 Win32 API。
    /// </summary>
    /// <param name="platformHandle">Avalonia 暴露的平台句柄。</param>
    /// <returns>句柄描述为 HWND 时返回 true。</returns>
    private static bool IsWindowsHandle(IPlatformHandle platformHandle)
    {
        return string.Equals(platformHandle.HandleDescriptor, HwndHandleDescriptor, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 应用 owned tool window 样式：保留顶层窗口框架，去掉任务栏入口和最小化按钮。
    /// </summary>
    /// <param name="windowHandle">Workbench 窗口 HWND。</param>
    private static void ApplyOwnedWindowStyle(IntPtr windowHandle)
    {
        var style = GetWindowLongPtr(windowHandle, GWL_STYLE).ToInt64();
        var exStyle = GetWindowLongPtr(windowHandle, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(windowHandle, GWL_STYLE, new IntPtr(CreateOwnedWindowStyle(style)));
        SetWindowLongPtr(windowHandle, GWL_EXSTYLE, new IntPtr(CreateOwnedToolWindowExStyle(exStyle)));
    }

    /// <summary>
    /// 在显式用户唤醒期间临时连接前台线程输入队列，绕过 Windows 前台锁后立即解除。
    /// </summary>
    /// <param name="windowHandle">需要激活的 Workbench HWND。</param>
    /// <returns>Workbench 最终成为真实前台窗口时返回 true。</returns>
    private static bool TrySetForegroundWindow(IntPtr windowHandle)
    {
        var foregroundHandle = GetForegroundWindow();
        if (foregroundHandle == windowHandle)
        {
            return true;
        }

        var foregroundThreadId = GetWindowThreadProcessId(foregroundHandle, out _);
        var currentThreadId = GetCurrentThreadId();
        var attached = foregroundThreadId != 0
            && foregroundThreadId != currentThreadId
            && AttachThreadInput(currentThreadId, foregroundThreadId, true);
        try
        {
            BringWindowToTop(windowHandle);
            SetActiveWindow(windowHandle);
            SetForegroundWindow(windowHandle);
            SetFocus(windowHandle);
            return WaitForForegroundWindow(windowHandle);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }
        }
    }

    /// <summary>
    /// 短暂切换 TopMost 再立即还原，突破 owned tool window 偶发停留在宿主后方的 Z 序状态。
    /// </summary>
    /// <param name="windowHandle">需要提升的 Workbench HWND。</param>
    private static void PulseTopMost(IntPtr windowHandle)
    {
        var flags = SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW;
        SetWindowPos(windowHandle, sHwndTopMost, 0, 0, 0, 0, flags);
        SetWindowPos(windowHandle, sHwndNoTopMost, 0, 0, 0, 0, flags);
    }

    /// <summary>
    /// 在短暂窗口内验证异步前台切换结果，避免 Windows 尚未提交 Z 序时误报失败。
    /// </summary>
    /// <param name="windowHandle">期望成为前台窗口的 Workbench HWND。</param>
    /// <returns>有限次数内观察到真实前台 HWND 时返回 true。</returns>
    private static bool WaitForForegroundWindow(IntPtr windowHandle)
    {
        for (var attempt = 0; attempt < FOREGROUND_VERIFICATION_ATTEMPTS; attempt++)
        {
            if (GetForegroundWindow() == windowHandle)
            {
                return true;
            }

            Thread.Sleep(FOREGROUND_VERIFICATION_DELAY_MS);
        }

        return false;
    }

    /// <summary>
    /// 计算 owned top-level window 样式，保留系统拖拽、缩放、最大化和关闭能力。
    /// </summary>
    /// <param name="currentStyle">当前 Win32 window style。</param>
    /// <returns>作为 Unity owned tool window 显示时应使用的 Win32 window style。</returns>
    private static long CreateOwnedWindowStyle(long currentStyle)
    {
        return currentStyle & ~WS_CHILD & ~WS_MINIMIZEBOX;
    }

    /// <summary>
    /// 计算 owned tool window 扩展样式，避免 Workbench 作为独立应用进入任务栏。
    /// </summary>
    /// <param name="currentExStyle">当前 Win32 extended window style。</param>
    /// <returns>作为 Unity owned tool window 显示时应使用的 extended style。</returns>
    private static long CreateOwnedToolWindowExStyle(long currentExStyle)
    {
        return (currentExStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
    }

    /// <summary>
    /// 创建进程内隐藏 owner，避免关闭时 Windows 沿 Unity owner 链路激活其它任务栏窗口。
    /// </summary>
    /// <returns>隐藏 owner HWND；创建失败时返回空句柄。</returns>
    private static IntPtr GetHiddenOwnerWindow()
    {
        if (!OperatingSystem.IsWindows())
        {
            return IntPtr.Zero;
        }

        if (sHiddenOwnerWindow != IntPtr.Zero && IsWindow(sHiddenOwnerWindow))
        {
            return sHiddenOwnerWindow;
        }

        sHiddenOwnerWindow = CreateWindowEx(
            (int)WS_EX_TOOLWINDOW,
            "STATIC",
            "YokiFrameWorkbenchClosingOwner",
            (int)WS_DISABLED,
            -32000,
            -32000,
            1,
            1,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        return sHiddenOwnerWindow;
    }

    /// <summary>
    /// 按进程位数读取 Win32 窗口样式，兼容未来非 x64 发布。
    /// </summary>
    /// <param name="windowHandle">窗口 HWND。</param>
    /// <param name="index">Win32 window long 索引。</param>
    /// <returns>窗口样式值。</returns>
    private static IntPtr GetWindowLongPtr(IntPtr windowHandle, int index)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(windowHandle, index)
            : new IntPtr(GetWindowLong32(windowHandle, index));
    }

    /// <summary>
    /// 按进程位数写入 Win32 窗口样式，兼容未来非 x64 发布。
    /// </summary>
    /// <param name="windowHandle">窗口 HWND。</param>
    /// <param name="index">Win32 window long 索引。</param>
    /// <param name="newValue">新的窗口样式值。</param>
    /// <returns>旧窗口样式值。</returns>
    private static IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr newValue)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(windowHandle, index, newValue)
            : new IntPtr(SetWindowLong32(windowHandle, index, newValue.ToInt32()));
    }

    /// <summary>
    /// 在 32 位进程中读取 Win32 window long 值。
    /// </summary>
    /// <param name="hWnd">窗口 HWND。</param>
    /// <param name="nIndex">Win32 window long 索引。</param>
    /// <returns>读取到的 window long 值。</returns>
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    /// <summary>
    /// 在 32 位进程中写入 Win32 window long 值。
    /// </summary>
    /// <param name="hWnd">窗口 HWND。</param>
    /// <param name="nIndex">Win32 window long 索引。</param>
    /// <param name="dwNewLong">新的 window long 值。</param>
    /// <returns>旧的 window long 值。</returns>
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>
    /// 在 64 位进程中读取 Win32 window long ptr 值。
    /// </summary>
    /// <param name="hWnd">窗口 HWND。</param>
    /// <param name="nIndex">Win32 window long 索引。</param>
    /// <returns>读取到的 window long ptr 值。</returns>
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    /// <summary>
    /// 在 64 位进程中写入 Win32 window long ptr 值。
    /// </summary>
    /// <param name="hWnd">窗口 HWND。</param>
    /// <param name="nIndex">Win32 window long 索引。</param>
    /// <param name="dwNewLong">新的 window long ptr 值。</param>
    /// <returns>旧的 window long ptr 值。</returns>
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    /// <summary>
    /// 判断 HWND 当前是否对应有效窗口。
    /// </summary>
    /// <param name="hWnd">待检查的窗口 HWND。</param>
    /// <returns>窗口有效时返回 true。</returns>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool IsWindow(IntPtr hWnd);

    /// <summary>
    /// 刷新窗口位置和样式，让 Win32 重新计算非客户区。
    /// </summary>
    /// <param name="hWnd">窗口 HWND。</param>
    /// <param name="hWndInsertAfter">Z 序目标。</param>
    /// <param name="x">保留参数。</param>
    /// <param name="y">保留参数。</param>
    /// <param name="cx">保留参数。</param>
    /// <param name="cy">保留参数。</param>
    /// <param name="flags">SetWindowPos 标志。</param>
    /// <returns>调用成功时返回 true。</returns>
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    /// <summary>
    /// 恢复被最小化或隐藏的 Workbench 窗口。
    /// </summary>
    /// <param name="hWnd">Workbench HWND。</param>
    /// <param name="nCmdShow">窗口显示命令。</param>
    /// <returns>窗口此前可见时返回 true。</returns>
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>
    /// 请求 Windows 把显式唤醒的 Workbench 设为前台窗口。
    /// </summary>
    /// <param name="hWnd">Workbench HWND。</param>
    /// <returns>前台切换成功时返回 true。</returns>
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// 把 Workbench 移到当前线程可访问的 Z 序顶部。
    /// </summary>
    /// <param name="hWnd">Workbench HWND。</param>
    /// <returns>调用成功时返回 true。</returns>
    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    /// <summary>
    /// 在线程输入队列已连接时把 Workbench 设为活动窗口。
    /// </summary>
    /// <param name="hWnd">Workbench HWND。</param>
    /// <returns>此前的活动窗口 HWND。</returns>
    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr hWnd);

    /// <summary>
    /// 在线程输入队列已连接时把键盘焦点交给 Workbench。
    /// </summary>
    /// <param name="hWnd">Workbench HWND。</param>
    /// <returns>此前拥有键盘焦点的 HWND。</returns>
    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    /// <summary>
    /// 获取当前系统前台窗口，用于验证激活结果而不是只相信 API 返回值。
    /// </summary>
    /// <returns>当前前台 HWND。</returns>
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// 获取窗口所属线程，供显式唤醒时临时共享输入队列。
    /// </summary>
    /// <param name="hWnd">前台窗口 HWND。</param>
    /// <param name="processId">窗口所属进程标识；当前仅用于满足 Win32 契约。</param>
    /// <returns>窗口所属线程标识。</returns>
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>
    /// 临时连接或解除两个线程的输入处理状态。
    /// </summary>
    /// <param name="idAttach">Workbench UI 线程标识。</param>
    /// <param name="idAttachTo">当前前台线程标识。</param>
    /// <param name="attach">true 连接，false 解除。</param>
    /// <returns>调用成功时返回 true。</returns>
    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

    /// <summary>
    /// 获取执行激活逻辑的 Workbench UI 线程标识。
    /// </summary>
    /// <returns>当前原生线程标识。</returns>
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    /// <summary>
    /// 创建 Win32 窗口；本类型只用于创建进程内隐藏 owner。
    /// </summary>
    /// <param name="dwExStyle">扩展样式。</param>
    /// <param name="lpClassName">窗口类名。</param>
    /// <param name="lpWindowName">窗口名称。</param>
    /// <param name="dwStyle">窗口样式。</param>
    /// <param name="x">X 坐标。</param>
    /// <param name="y">Y 坐标。</param>
    /// <param name="nWidth">宽度。</param>
    /// <param name="nHeight">高度。</param>
    /// <param name="hWndParent">父窗口或 message-only 标记。</param>
    /// <param name="hMenu">菜单句柄。</param>
    /// <param name="hInstance">实例句柄。</param>
    /// <param name="lpParam">创建参数。</param>
    /// <returns>新建窗口 HWND。</returns>
    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle,
        string lpClassName,
        string lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    /// <summary>
}
