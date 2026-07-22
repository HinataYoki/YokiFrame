using YokiFrame.Workbench.Avalonia.Views;
using System.Reflection;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 提供 Workbench Shell 源码契约测试使用的平台常量与源码定位方法。
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
    /// 从当前测试目录向上查找 Workbench Shell code-behind，用于验证事件异常边界。
    /// </summary>
    /// <returns>WorkbenchShellView.axaml.cs 文本。</returns>
    private static string ReadWorkbenchShellViewSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateWorkbenchShellViewSourceCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 WorkbenchShellView.axaml.cs。");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 Workbench Shell code-behind 路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选源码路径。</returns>
    private static IEnumerable<string> CreateWorkbenchShellViewSourceCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Views",
            "WorkbenchShellView.axaml.cs");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Views",
            "WorkbenchShellView.axaml.cs");
    }

    /// <summary>
    /// 通过反射调用平台 owner 样式计算方法，避免测试直接暴露 Windows host 类型为公共 API。
    /// </summary>
    /// <param name="currentStyle">当前 Win32 window style。</param>
    /// <returns>作为 Unity owned tool window 显示时应使用的 Win32 window style。</returns>
    private static long InvokeCreateOwnedWindowStyle(long currentStyle)
    {
        var hostType = typeof(WorkbenchWindow).Assembly.GetType("YokiFrame.Workbench.Avalonia.Platform.WindowsWorkbenchWindowHost");
        Assert.NotNull(hostType);
        var method = hostType.GetMethod("CreateOwnedWindowStyle", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (long)method.Invoke(null, new object[] { currentStyle })!;
    }

    /// <summary>
    /// 通过反射调用平台扩展样式计算方法，验证 owner 窗口不会进入任务栏。
    /// </summary>
    /// <param name="currentExStyle">当前 Win32 extended window style。</param>
    /// <returns>作为 Unity owned tool window 显示时应使用的 extended style。</returns>
    private static long InvokeCreateOwnedToolWindowExStyle(long currentExStyle)
    {
        var hostType = typeof(WorkbenchWindow).Assembly.GetType("YokiFrame.Workbench.Avalonia.Platform.WindowsWorkbenchWindowHost");
        Assert.NotNull(hostType);
        var method = hostType.GetMethod("CreateOwnedToolWindowExStyle", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (long)method.Invoke(null, new object[] { currentExStyle })!;
    }

    /// <summary>
    /// 从当前测试目录向上查找 Workbench Shell XAML，用于验证窗口 chrome 布局契约。
    /// </summary>
    /// <returns>Workbench Shell XAML 文本。</returns>
    private static string ReadWorkbenchShellViewXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateWorkbenchShellViewXamlCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 WorkbenchShellView.axaml。");
    }

    /// <summary>
    /// 从当前测试目录向上查找 WorkbenchApp 源码，用于验证全局设计系统挂载。
    /// </summary>
    /// <returns>WorkbenchApp 源码文本。</returns>
    private static string ReadWorkbenchAppSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateWorkbenchAppSourceCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 WorkbenchApp.cs。");
    }

    /// <summary>
    /// 从当前测试目录向上查找 App.axaml，用于验证全局资源和样式静态挂载。
    /// </summary>
    /// <returns>App.axaml 文本。</returns>
    private static string ReadWorkbenchAppXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateWorkbenchAppXamlCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 App.axaml。");
    }

    /// <summary>
    /// 从当前测试目录向上查找 Program 源码，用于验证发布版入口不保留调试日志初始化。
    /// </summary>
    /// <returns>Program.cs 源码文本。</returns>
    private static string ReadProgramSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateProgramSourceCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 Program.cs。");
    }

    /// <summary>
    /// 从当前测试目录向上查找 Windows host 源码，用于验证 Win32 owner 生命周期契约。
    /// </summary>
    /// <returns>WindowsWorkbenchWindowHost.cs 源码文本。</returns>
    private static string ReadWindowsHostSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateWindowsHostSourceCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 WindowsWorkbenchWindowHost.cs。");
    }

    /// <summary>
    /// 从当前测试目录向上查找 InstallerWindow 源码，用于验证 Installer 模式也使用品牌图标。
    /// </summary>
    /// <returns>InstallerWindow.cs 源码文本。</returns>
    private static string ReadInstallerWindowSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateInstallerWindowSourceCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 InstallerWindow.cs。");
    }

    /// <summary>
    /// 从当前测试目录向上查找 WorkbenchWindow 源码，用于验证启动路径不会阻塞首帧。
    /// </summary>
    /// <returns>WorkbenchWindow.cs 源码文本。</returns>
    private static string ReadWorkbenchWindowSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateWorkbenchWindowSourceCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 WorkbenchWindow.cs。");
    }

    /// <summary>
    /// 从当前测试目录向上查找 AppTitleBar XAML，用于验证窗口按钮仍由组件显式处理。
    /// </summary>
    /// <returns>AppTitleBar XAML 文本。</returns>
    private static string ReadAppTitleBarXaml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateAppTitleBarXamlCandidates(directory.FullName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 AppTitleBar.axaml。");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 Workbench Shell XAML 路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选 XAML 路径。</returns>
    private static IEnumerable<string> CreateWorkbenchShellViewXamlCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Views",
            "WorkbenchShellView.axaml");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Views",
            "WorkbenchShellView.axaml");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 WorkbenchApp 源码路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选源码路径。</returns>
    private static IEnumerable<string> CreateWorkbenchAppSourceCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "WorkbenchApp.cs");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "WorkbenchApp.cs");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 App.axaml 路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选 XAML 路径。</returns>
    private static IEnumerable<string> CreateWorkbenchAppXamlCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "App.axaml");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "App.axaml");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 Program 源码路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选源码路径。</returns>
    private static IEnumerable<string> CreateProgramSourceCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Program.cs");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Program.cs");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 Windows host 源码路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选源码路径。</returns>
    private static IEnumerable<string> CreateWindowsHostSourceCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Platform",
            "WindowsWorkbenchWindowHost.cs");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Platform",
            "WindowsWorkbenchWindowHost.cs");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 InstallerWindow 源码路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选源码路径。</returns>
    private static IEnumerable<string> CreateInstallerWindowSourceCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "InstallerWindow.cs");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "InstallerWindow.cs");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 WorkbenchWindow 源码路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选源码路径。</returns>
    private static IEnumerable<string> CreateWorkbenchWindowSourceCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "WorkbenchWindow.cs");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "WorkbenchWindow.cs");
    }

    /// <summary>
    /// 生成源码树和测试输出树下可能存在的 AppTitleBar XAML 路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <returns>候选 XAML 路径。</returns>
    private static IEnumerable<string> CreateAppTitleBarXamlCandidates(string directory)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Components",
            "AppTitleBar.axaml");
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            "Components",
            "AppTitleBar.axaml");
    }
}
