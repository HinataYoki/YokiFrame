namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 覆盖 Workbench 窗口激活时序和 Windows 前台恢复的源码级平台契约。
/// </summary>
public sealed class WorkbenchWindowActivationContractTests
{
    /// <summary>
    /// 验证单实例请求必须等待 UI 线程完成激活后才能返回 ACK。
    /// </summary>
    [Fact]
    public void ActivationAcknowledgementWaitsForUiActivation()
    {
        var source = ReadWorkbenchFile("WorkbenchWindow.cs");
        var activationIndex = source.IndexOf(
            "Dispatcher.UIThread.InvokeAsync(ActivateExistingWindow)",
            StringComparison.Ordinal);
        var acknowledgementIndex = source.IndexOf("eventArgs.Accept();", StringComparison.Ordinal);

        Assert.True(activationIndex >= 0);
        Assert.True(acknowledgementIndex > activationIndex);
        Assert.DoesNotContain("Dispatcher.UIThread.Post(ActivateExistingWindow)", source);
    }

    /// <summary>
    /// 验证 Windows 平台使用显示、Z 序和前台窗口三层兜底，而不只依赖 Avalonia Activate。
    /// </summary>
    [Fact]
    public void WindowsHostProvidesExplicitForegroundRecovery()
    {
        var source = ReadWorkbenchFile("Platform", "WindowsWorkbenchWindowHost.cs");

        Assert.Contains("TryBringToFront", source);
        Assert.Contains("ShowWindow", source);
        Assert.Contains("SetWindowPos", source);
        Assert.Contains("SetForegroundWindow", source);
        Assert.Contains("GetForegroundWindow", source);
        Assert.Contains("AttachThreadInput", source);
        Assert.Contains("BringWindowToTop", source);
        Assert.Contains("SetActiveWindow", source);
        Assert.Contains("sHwndTopMost", source);
        Assert.Contains("sHwndNoTopMost", source);
        Assert.Contains("SWP_SHOWWINDOW", source);
    }

    /// <summary>
    /// 验证窗口开始关闭后立即停止接收激活请求，避免新进程重定向到无窗口 owner。
    /// </summary>
    [Fact]
    public void ClosingWindowStopsAcceptingActivationRequests()
    {
        var source = ReadWorkbenchFile("WorkbenchWindow.cs");
        var closingStart = source.IndexOf("private async void OnClosing", StringComparison.Ordinal);
        var closedStart = source.IndexOf("private void OnClosed", StringComparison.Ordinal);
        var closingBody = source[closingStart..closedStart];

        Assert.Contains("mIsClosed = true;", closingBody);
        Assert.Contains("ActivationRequested -= OnActivationRequested", closingBody);
    }

    /// <summary>
    /// 从 Workbench 源码树读取根文件。
    /// </summary>
    /// <param name="fileName">根文件名。</param>
    /// <returns>源码文本。</returns>
    private static string ReadWorkbenchFile(string fileName)
    {
        return ReadWorkbenchFile(string.Empty, fileName);
    }

    /// <summary>
    /// 从测试输出目录向上定位 Workbench 源码树中的指定文件。
    /// </summary>
    /// <param name="subDirectory">相对 Workbench 项目的子目录。</param>
    /// <param name="fileName">文件名。</param>
    /// <returns>源码文本。</returns>
    private static string ReadWorkbenchFile(string subDirectory, string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (var candidate in CreateCandidates(directory.FullName, subDirectory, fileName))
            {
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 Workbench 激活源码文件。");
    }

    /// <summary>
    /// 生成源码根和工作区根下的候选文件路径。
    /// </summary>
    /// <param name="directory">当前向上探测的目录。</param>
    /// <param name="subDirectory">目标子目录。</param>
    /// <param name="fileName">目标文件名。</param>
    /// <returns>候选路径序列。</returns>
    private static IEnumerable<string> CreateCandidates(
        string directory,
        string subDirectory,
        string fileName)
    {
        yield return Path.Combine(
            directory,
            "src",
            "YokiFrame.Workbench.Avalonia",
            subDirectory,
            fileName);
        yield return Path.Combine(
            directory,
            "Assets",
            "YokiFrame",
            "YokiFrameWorkbench~",
            "src",
            "YokiFrame.Workbench.Avalonia",
            subDirectory,
            fileName);
    }
}
