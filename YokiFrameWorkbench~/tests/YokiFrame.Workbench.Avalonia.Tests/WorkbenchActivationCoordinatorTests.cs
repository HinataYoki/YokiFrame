using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 验证 Workbench 按项目隔离的单实例锁与本机激活通道。
/// </summary>
public sealed class WorkbenchActivationCoordinatorTests
{
    /// <summary>
    /// 验证同一项目的等价目录写法共享 owner，并把激活请求发送给首个实例。
    /// </summary>
    [Fact]
    public async Task SecondCoordinatorRedirectsActivationToPrimaryInstance()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            using var primary = WorkbenchActivationCoordinator.Start(projectRoot);
            TaskCompletionSource activation = new(TaskCreationOptions.RunContinuationsAsynchronously);
            primary.ActivationRequested += (_, request) =>
            {
                request.Accept();
                activation.TrySetResult();
            };

            var relativeProjectRoot = Path.GetRelativePath(Environment.CurrentDirectory, projectRoot);
            using var secondary = WorkbenchActivationCoordinator.Start(
                relativeProjectRoot + Path.DirectorySeparatorChar);

            Assert.True(primary.IsPrimaryInstance);
            Assert.False(primary.ActivationRedirected);
            Assert.False(secondary.IsPrimaryInstance);
            Assert.True(secondary.ActivationRedirected);
            await activation.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证 owner 尚未提供窗口激活接收者时不会返回伪成功，避免后续进程错误退出。
    /// </summary>
    [Fact]
    public void ActivationWithoutReadyWindowDoesNotRedirectSecondary()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            using var primary = WorkbenchActivationCoordinator.Start(projectRoot);
            using var secondary = WorkbenchActivationCoordinator.Start(projectRoot);

            Assert.True(primary.IsPrimaryInstance);
            Assert.False(secondary.IsPrimaryInstance);
            Assert.False(secondary.ActivationRedirected);
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 验证不同项目使用不同锁和管道，可以同时拥有各自 Workbench。
    /// </summary>
    [Fact]
    public void DifferentProjectsOwnIndependentActivationChannels()
    {
        var firstRoot = CreateProjectRoot();
        var secondRoot = CreateProjectRoot();
        try
        {
            using var first = WorkbenchActivationCoordinator.Start(firstRoot);
            using var second = WorkbenchActivationCoordinator.Start(secondRoot);

            Assert.True(first.IsPrimaryInstance);
            Assert.True(second.IsPrimaryInstance);
            Assert.False(first.ActivationRedirected);
            Assert.False(second.ActivationRedirected);
        }
        finally
        {
            DeleteProjectRoot(firstRoot);
            DeleteProjectRoot(secondRoot);
        }
    }

    /// <summary>
    /// 验证 owner 退出释放项目锁后，新进程可以成为同项目的新 owner。
    /// </summary>
    [Fact]
    public void ReleasedOwnerAllowsNextCoordinatorToBecomePrimary()
    {
        var projectRoot = CreateProjectRoot();
        try
        {
            var first = WorkbenchActivationCoordinator.Start(projectRoot);
            Assert.True(first.IsPrimaryInstance);
            first.Dispose();

            using var next = WorkbenchActivationCoordinator.Start(projectRoot);

            Assert.True(next.IsPrimaryInstance);
            Assert.False(next.ActivationRedirected);
        }
        finally
        {
            DeleteProjectRoot(projectRoot);
        }
    }

    /// <summary>
    /// 创建唯一临时项目根，隔离并行测试的锁文件和管道名。
    /// </summary>
    /// <returns>已创建的临时项目根。</returns>
    private static string CreateProjectRoot()
    {
        var root = Path.Combine(
            Environment.CurrentDirectory,
            ".yokiframe-workbench-activation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// 删除测试项目；文件仍被占用时由失败显式暴露资源泄漏。
    /// </summary>
    /// <param name="projectRoot">待删除项目根。</param>
    private static void DeleteProjectRoot(string projectRoot)
    {
        if (Directory.Exists(projectRoot))
        {
            Directory.Delete(projectRoot, recursive: true);
        }
    }
}
