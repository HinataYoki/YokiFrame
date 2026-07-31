using YokiFrame.Installer.Core.Services;
using YokiFrame.Tooling.Application.Installer;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 提供 Installer 工作流测试使用的临时项目、网关和平台 fake。
/// </summary>
public sealed partial class InstallerShellViewModelWorkflowTests
{
    /// <summary>
    /// 构建独立临时项目、Application fake 和 ViewModel 的测试夹具。
    /// </summary>
    private sealed class InstallerViewModelFixture : IDisposable
    {
        private readonly string mRoot;

        /// <summary>
        /// 创建夹具并组合真实目标检测、零延迟节流和 fake gateway。
        /// </summary>
        /// <param name="root">临时根目录。</param>
        /// <param name="sourceRoot">源包根。</param>
        /// <param name="targetRoot">目标项目根。</param>
        /// <param name="packageTarget">预期包目标。</param>
        private InstallerViewModelFixture(
            string root,
            string sourceRoot,
            string targetRoot,
            string packageTarget,
            IInstallerDetectionDelay? detectionDelay = null)
        {
            mRoot = root;
            SourceRoot = sourceRoot;
            TargetRoot = targetRoot;
            PackageTarget = packageTarget;
            Gateway = new FakeInstallerWorkflowGateway(packageTarget);
            FolderPicker = new FakeInstallerFolderPicker();
            GodotRuntimeBootstrapper = new FakeGodotRuntimeBootstrapper();
            InstallerSessionService session = new(Gateway);
            ToolStartupOptions startup = new(ToolStartupMode.Installer, targetRoot, sourceRoot, targetRoot);
            ViewModel = new InstallerShellViewModel(
                startup,
                session,
                new InstallerTargetDetectionService(),
                detectionDelay == null
                    ? new InstallerInputDetectionService(TimeSpan.Zero)
                    : new InstallerInputDetectionService(TimeSpan.Zero, detectionDelay),
                FolderPicker,
                GodotRuntimeBootstrapper);
        }

        /// <summary>
        /// 获取源包根。
        /// </summary>
        public string SourceRoot { get; }

        /// <summary>
        /// 获取目标项目根。
        /// </summary>
        public string TargetRoot { get; }

        /// <summary>
        /// 获取预期包目标。
        /// </summary>
        public string PackageTarget { get; }

        /// <summary>
        /// 获取可观察 Application gateway。
        /// </summary>
        public FakeInstallerWorkflowGateway Gateway { get; }

        /// <summary>
        /// 获取可控目录选择器。
        /// </summary>
        public FakeInstallerFolderPicker FolderPicker { get; }

        /// <summary>
        /// 获取记录源码 bootstrap 调用的 Godot Runtime fake。
        /// </summary>
        public FakeGodotRuntimeBootstrapper GodotRuntimeBootstrapper { get; }

        /// <summary>
        /// 获取待验证 ViewModel。
        /// </summary>
        public InstallerShellViewModel ViewModel { get; }

        /// <summary>
        /// 创建符合最低版本门控的 Unity 临时项目。
        /// </summary>
        /// <returns>Unity Installer ViewModel 夹具。</returns>
        public static InstallerViewModelFixture CreateUnity()
        {
            var root = CreateTempRoot("unity");
            var source = Path.Combine(root, "source");
            var target = Path.Combine(root, "project");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(Path.Combine(target, "Assets"));
            Directory.CreateDirectory(Path.Combine(target, "Packages"));
            Directory.CreateDirectory(Path.Combine(target, "ProjectSettings"));
            File.WriteAllText(Path.Combine(target, "Packages", "manifest.json"), "{\"dependencies\":{}}\n");
            File.WriteAllText(Path.Combine(target, "ProjectSettings", "ProjectVersion.txt"), "m_EditorVersion: 2022.3.62f1\n");
            return new InstallerViewModelFixture(
                root,
                source,
                target,
                Path.Combine(target, "Packages", "com.hinatayoki.yokiframe"));
        }

        /// <summary>
        /// 创建符合 Godot 4.7 .NET 门控的临时项目。
        /// </summary>
        /// <returns>Godot Installer ViewModel 夹具。</returns>
        public static InstallerViewModelFixture CreateGodot(IInstallerDetectionDelay? detectionDelay = null)
        {
            var root = CreateTempRoot("godot");
            var source = Path.Combine(root, "source");
            var target = Path.Combine(root, "project");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "project.godot"), "config_version=5\n");
            File.WriteAllText(
                Path.Combine(target, "game.csproj"),
                "<Project Sdk=\"Godot.NET.Sdk/4.7.0\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>\n");
            return new InstallerViewModelFixture(
                root,
                source,
                target,
                Path.Combine(target, "addons", "yokiframe", "package", "YokiFrame"),
                detectionDelay);
        }

        /// <summary>
        /// 删除测试创建的临时目录。
        /// </summary>
        public void Dispose()
        {
            if (Directory.Exists(mRoot))
            {
                Directory.Delete(mRoot, recursive: true);
            }
        }

        /// <summary>
        /// 创建带随机后缀的临时根，避免并行测试互相污染。
        /// </summary>
        /// <param name="label">测试场景标签。</param>
        /// <returns>已创建的临时根。</returns>
        private static string CreateTempRoot(string label)
        {
            var root = Path.Combine(Path.GetTempPath(), "yokiframe-installer-vm-" + label + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return root;
        }
    }

    /// <summary>
    /// 记录输入并模拟计划、冲突、进度和成功结果。
    /// </summary>
    private sealed class FakeInstallerWorkflowGateway(string packageTarget) : IInstallerWorkflowGateway
    {
        /// <summary>
        /// 获取最近一次输入。
        /// </summary>
        public InstallerInstallOptions? LastOptions { get; private set; }

        /// <summary>
        /// 获取模拟 Core 写事务的调用次数，用于证明失效旧计划不会触发执行。
        /// </summary>
        public int ExecuteCount { get; private set; }

        /// <summary>
        /// 获取或设置是否拒绝尚未确认的 legacy 接管。
        /// </summary>
        public bool RejectUnconfirmedLegacy { get; set; }

        /// <summary>
        /// 获取或设置计划生成阶段需要抛出的错误，用于模拟缓存前置条件失败。
        /// </summary>
        public Exception? PlanningFailure { get; set; }

        /// <summary>
        /// 获取或设置计划失败剩余次数，用于验证 bootstrap 后会重新规划。
        /// </summary>
        public int PlanningFailuresRemaining { get; set; }

        /// <summary>
        /// 获取或设置执行阶段需要抛出的事务失败，用于验证 UI 诊断投影。
        /// </summary>
        public InstallerExecutionException? ExecutionFailure { get; set; }

        /// <summary>
        /// 生成与输入模式一致的统一预览，或模拟 legacy 冲突。
        /// </summary>
        /// <param name="options">安装输入。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>统一安装预览。</returns>
        public Task<InstallerPlanPreview> CreatePlanAsync(
            InstallerInstallOptions options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastOptions = options;
            if (PlanningFailure != null && PlanningFailuresRemaining > 0)
            {
                PlanningFailuresRemaining--;
                throw PlanningFailure;
            }

            if (RejectUnconfirmedLegacy && options.LegacyPackagePolicy == InstallerLegacyPackagePolicy.Reject)
            {
                throw new InstallerConflictException("Legacy package requires confirmation.", new[] { "legacy.cs" });
            }

            var engine = options.Mode == InstallerInstallMode.GodotLocal
                ? InstallerTargetKind.Godot
                : InstallerTargetKind.Unity;
            InstallerPlanPreview plan = new(
                engine,
                options.Mode,
                options.SourcePackageRoot ?? options.GitUrl ?? string.Empty,
                options.TargetProjectRoot,
                packageTarget,
                new[] { new InstallerPlanActionPreview(InstallerPlanActionKind.InstallPackage, packageTarget, null, "Install") },
                Array.Empty<string>());
            return Task.FromResult(plan);
        }

        /// <summary>
        /// 模拟应用和校验阶段后返回成功结果。
        /// </summary>
        /// <param name="options">安装输入。</param>
        /// <param name="plan">安装预览。</param>
        /// <param name="progress">进度通道。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>统一成功结果。</returns>
        public Task<InstallerExecutionResult> ExecuteAsync(
            InstallerInstallOptions options,
            InstallerPlanPreview plan,
            IProgress<InstallerProgressUpdate> progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecuteCount++;
            progress.Report(new InstallerProgressUpdate(InstallerProgressStage.Applying, 1, 2, "正在写入。"));
            if (ExecutionFailure != null)
            {
                throw ExecutionFailure;
            }

            progress.Report(new InstallerProgressUpdate(InstallerProgressStage.Verifying, 2, 2, "正在校验。"));
            return Task.FromResult(new InstallerExecutionResult(plan.PackageTarget, true, false, new[] { plan.PackageTarget }));
        }
    }

    /// <summary>
    /// 记录 ViewModel 请求的源码包和目标项目，不启动真实 dotnet 或 Avalonia 子进程。
    /// </summary>
    private sealed class FakeGodotRuntimeBootstrapper : IGodotRuntimeBootstrapper
    {
        /// <summary>
        /// 获取 Runtime bootstrap 调用次数。
        /// </summary>
        public int BootstrapCount { get; private set; }

        /// <summary>
        /// 获取 Runtime bootstrap 已进入执行阶段的通知，用于观察构建中的页面状态。
        /// </summary>
        public TaskCompletionSource BootstrapStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取或设置可选的 Runtime bootstrap 等待闸门；为空时保持立即完成行为。
        /// </summary>
        public TaskCompletionSource? BootstrapGate { get; set; }

        /// <summary>
        /// 获取或设置 bootstrap 阶段需要抛出的错误，用于验证失败后的恢复入口。
        /// </summary>
        public Exception? BootstrapFailure { get; set; }

        /// <summary>
        /// 获取最近一次请求的源码包根。
        /// </summary>
        public string? SourcePackageRoot { get; private set; }

        /// <summary>
        /// 获取最近一次请求的目标 Godot 项目根。
        /// </summary>
        public string? TargetProjectRoot { get; private set; }

        /// <summary>
        /// 记录参数并立即模拟 Runtime 构建和新 Installer 启动成功。
        /// </summary>
        /// <param name="sourcePackageRoot">当前选定源包根。</param>
        /// <param name="targetProjectRoot">当前选定 Godot 项目根。</param>
        /// <param name="cancellationToken">测试调用传入的取消令牌。</param>
        /// <returns>立即完成任务。</returns>
        public async Task BootstrapAndOpenInstallerAsync(
            string sourcePackageRoot,
            string targetProjectRoot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BootstrapCount++;
            if (BootstrapFailure != null)
            {
                throw BootstrapFailure;
            }

            SourcePackageRoot = sourcePackageRoot;
            TargetProjectRoot = targetProjectRoot;
            await WaitForBootstrapGateAsync(cancellationToken);
        }

        /// <summary>
        /// 记录自动 Runtime bootstrap 请求并立即模拟构建完成。
        /// </summary>
        /// <param name="sourcePackageRoot">当前选定源包根。</param>
        /// <param name="targetProjectRoot">当前选定目标项目根。</param>
        /// <param name="cancellationToken">测试调用传入的取消令牌。</param>
        /// <returns>立即完成任务。</returns>
        public async Task BootstrapAsync(
            string sourcePackageRoot,
            string targetProjectRoot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BootstrapCount++;
            if (BootstrapFailure != null)
            {
                throw BootstrapFailure;
            }

            SourcePackageRoot = sourcePackageRoot;
            TargetProjectRoot = targetProjectRoot;
            await WaitForBootstrapGateAsync(cancellationToken);
        }

        /// <summary>
        /// 发出 bootstrap 已开始通知，并在测试配置闸门时等待其释放。
        /// </summary>
        /// <param name="cancellationToken">当前 bootstrap 取消令牌。</param>
        private async Task WaitForBootstrapGateAsync(CancellationToken cancellationToken)
        {
            BootstrapStarted.TrySetResult();
            if (BootstrapGate != null)
            {
                await BootstrapGate.Task.WaitAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// 以显式释放的等待点替代真实防抖时间，使测试能稳定观察旧计划与新输入之间的窗口。
    /// </summary>
    private sealed class ControlledInstallerDetectionDelay : IInstallerDetectionDelay
    {
        private readonly object mSyncRoot = new();
        private readonly List<TaskCompletionSource> mPending = new();
        private readonly SemaphoreSlim mStarted = new(0);

        /// <summary>
        /// 记录一个新的防抖等待，并由测试通过 ReleaseLatest 选择何时继续。
        /// </summary>
        /// <param name="delay">生产代码传入的防抖时长；测试不依赖具体数值。</param>
        /// <param name="cancellationToken">被最新输入替代时取消等待的令牌。</param>
        /// <returns>等待被显式释放或取消后完成的任务。</returns>
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (mSyncRoot)
            {
                mPending.Add(completion);
            }

            mStarted.Release();
            return completion.Task.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// 等待下一次防抖调度已经进入可控等待点。
        /// </summary>
        /// <returns>新等待点出现后完成的任务。</returns>
        public Task WaitForNextAsync()
        {
            return mStarted.WaitAsync();
        }

        /// <summary>
        /// 放行最新调度，避免被已取消的旧调度继续生成过期计划。
        /// </summary>
        public void ReleaseLatest()
        {
            TaskCompletionSource completion;
            lock (mSyncRoot)
            {
                completion = mPending[^1];
            }

            completion.TrySetResult();
        }
    }

    /// <summary>
    /// 为 ViewModel 测试提供可控的源目录和目标目录选择结果。
    /// </summary>
    private sealed class FakeInstallerFolderPicker : IInstallerFolderPicker
    {
        /// <summary>
        /// 获取或设置源目录返回值。
        /// </summary>
        public string? SourceResult { get; set; }

        /// <summary>
        /// 获取或设置目标目录返回值。
        /// </summary>
        public string? TargetResult { get; set; }

        /// <summary>
        /// 获取选择请求次数。
        /// </summary>
        public int RequestCount { get; private set; }

        /// <summary>
        /// 根据标题返回预设路径。
        /// </summary>
        /// <param name="title">选择器标题。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>预设路径。</returns>
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default, string? suggestedPath = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            return Task.FromResult(title.Contains("源", StringComparison.Ordinal) ? SourceResult : TargetResult);
        }
    }
}
