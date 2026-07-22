using YokiFrame.Tooling.Application.Installer;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 描述 Installer Headless 渲染覆盖的四种页面状态。
/// </summary>
internal enum InstallerRenderScenario
{
    /// <summary>
    /// 尚未识别目标宿主的默认状态。
    /// </summary>
    Default,

    /// <summary>
    /// Unity embedded 本地包状态。
    /// </summary>
    UnityLocal,

    /// <summary>
    /// Unity Git URL 状态。
    /// </summary>
    UnityGit,

    /// <summary>
    /// Godot 本地插件状态。
    /// </summary>
    Godot
}

/// <summary>
/// 提供真实目标目录、ViewModel 和可控外部边界的 Headless 测试现场。
/// </summary>
internal sealed class InstallerHeadlessFixture : IDisposable
{
    private const string LONG_SEGMENT = "installer-layout-path-with-a-deliberately-long-name-for-button-overlap-validation";

    /// <summary>
    /// 创建长路径源包以及 Unity、Godot、未知目标目录。
    /// </summary>
    private InstallerHeadlessFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "yokiframe-headless", Guid.NewGuid().ToString("N"));
        SourcePackageRoot = Path.Combine(Root, LONG_SEGMENT, "source", "YokiFrame");
        UnityProjectRoot = Path.Combine(Root, LONG_SEGMENT, "unity-project-with-long-name");
        GodotProjectRoot = Path.Combine(Root, LONG_SEGMENT, "godot-project-with-long-name");
        UnknownProjectRoot = Path.Combine(Root, LONG_SEGMENT, "unknown-project-with-long-name");
        CreateSourcePackage();
        CreateUnityProject();
        CreateGodotProject();
        Directory.CreateDirectory(UnknownProjectRoot);
    }

    /// <summary>
    /// 获取 fixture 总根目录。
    /// </summary>
    internal string Root { get; }

    /// <summary>
    /// 获取长路径源包根。
    /// </summary>
    internal string SourcePackageRoot { get; }

    /// <summary>
    /// 获取长路径 Unity 项目根。
    /// </summary>
    internal string UnityProjectRoot { get; }

    /// <summary>
    /// 获取长路径 Godot 项目根。
    /// </summary>
    internal string GodotProjectRoot { get; }

    /// <summary>
    /// 获取长路径未知项目根。
    /// </summary>
    internal string UnknownProjectRoot { get; }

    /// <summary>
    /// 创建完整 Headless 测试现场。
    /// </summary>
    /// <returns>测试 fixture。</returns>
    internal static InstallerHeadlessFixture Create()
    {
        return new InstallerHeadlessFixture();
    }

    /// <summary>
    /// 根据场景创建并稳定到对应状态的真实 Installer ViewModel。
    /// </summary>
    /// <param name="scenario">渲染场景。</param>
    /// <returns>已完成目标检测和预览准备的 ViewModel。</returns>
    internal async Task<InstallerShellViewModel> CreateViewModelAsync(InstallerRenderScenario scenario)
    {
        var targetRoot = scenario switch
        {
            InstallerRenderScenario.UnityLocal or InstallerRenderScenario.UnityGit => UnityProjectRoot,
            InstallerRenderScenario.Godot => GodotProjectRoot,
            _ => UnknownProjectRoot
        };
        InstallerSessionService session = new(new RenderingInstallerGateway());
        var viewModel = new InstallerShellViewModel(
            new ToolStartupOptions(ToolStartupMode.Installer, targetRoot, SourcePackageRoot, targetRoot),
            session,
            new InstallerTargetDetectionService(),
            new InstallerInputDetectionService(TimeSpan.Zero, ImmediateDetectionDelay.Instance),
            EmptyFolderPicker.Instance);
        if (scenario == InstallerRenderScenario.Default)
        {
            return viewModel;
        }

        await viewModel.InitializeAsync();
        if (scenario == InstallerRenderScenario.UnityGit)
        {
            viewModel.GitUrl = "https://github.com/HinataYoki/YokiFrame.git?path=Assets/YokiFrame&layout=long-query-value";
            viewModel.IsUnityGitSelected = true;
            await viewModel.RefreshPlanAsync();
        }

        return viewModel;
    }

    /// <summary>
    /// 删除测试现场和生成的临时项目。
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    /// <summary>
    /// 创建渲染测试所需的最小源包目录。
    /// </summary>
    private void CreateSourcePackage()
    {
        WriteText(Path.Combine(SourcePackageRoot, "Documentation~", "README.md"), "fixture");
        WriteText(Path.Combine(SourcePackageRoot, "Core", "Runtime", "Marker.cs"), "namespace Fixture; public sealed class Marker { }");
    }

    /// <summary>
    /// 创建满足 Unity 2022.3 版本门控的目标项目。
    /// </summary>
    private void CreateUnityProject()
    {
        Directory.CreateDirectory(Path.Combine(UnityProjectRoot, "Assets"));
        Directory.CreateDirectory(Path.Combine(UnityProjectRoot, "Packages"));
        Directory.CreateDirectory(Path.Combine(UnityProjectRoot, "ProjectSettings"));
        WriteText(Path.Combine(UnityProjectRoot, "Packages", "manifest.json"), "{\"dependencies\":{}}");
        WriteText(
            Path.Combine(UnityProjectRoot, "ProjectSettings", "ProjectVersion.txt"),
            "m_EditorVersion: 2022.3.0f1" + System.Environment.NewLine);
    }

    /// <summary>
    /// 创建满足 Godot 4.7 .NET 门控的目标项目。
    /// </summary>
    private void CreateGodotProject()
    {
        WriteText(
            Path.Combine(GodotProjectRoot, "FirstDemo.csproj"),
            "<Project Sdk=\"Godot.NET.Sdk/4.7.0\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>");
        WriteText(Path.Combine(GodotProjectRoot, "project.godot"), "config_version=5" + System.Environment.NewLine);
    }

    /// <summary>
    /// 写入文本并自动创建父目录。
    /// </summary>
    /// <param name="path">目标路径。</param>
    /// <param name="content">文本内容。</param>
    private static void WriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// 立即完成节流等待，使 ViewModel 状态在测试中确定性收敛。
    /// </summary>
    private sealed class ImmediateDetectionDelay : IInstallerDetectionDelay
    {
        /// <summary>
        /// 获取无状态共享实例。
        /// </summary>
        internal static ImmediateDetectionDelay Instance { get; } = new();

        /// <summary>
        /// 立即响应成功或调用方取消。
        /// </summary>
        /// <param name="delay">被忽略的节流时间。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            return cancellationToken.IsCancellationRequested
                ? Task.FromCanceled(cancellationToken)
                : Task.CompletedTask;
        }
    }

    /// <summary>
    /// Headless 测试不打开原生目录选择器。
    /// </summary>
    private sealed class EmptyFolderPicker : IInstallerFolderPicker
    {
        /// <summary>
        /// 获取无状态共享实例。
        /// </summary>
        internal static EmptyFolderPicker Instance { get; } = new();

        /// <summary>
        /// 模拟用户取消目录选择。
        /// </summary>
        /// <param name="title">对话框标题。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>始终返回 null。</returns>
        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken = default, string? suggestedPath = null)
        {
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>
    /// 返回与输入一致的计划预览，避免 Headless 测试执行真实安装写事务。
    /// </summary>
    private sealed class RenderingInstallerGateway : IInstallerWorkflowGateway
    {
        /// <summary>
        /// 根据输入构造可供 ViewModel 展示的统一计划。
        /// </summary>
        /// <param name="options">安装输入。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>计划预览。</returns>
        public Task<InstallerPlanPreview> CreatePlanAsync(
            InstallerInstallOptions options,
            CancellationToken cancellationToken)
        {
            var engine = options.Mode == InstallerInstallMode.GodotLocal
                ? InstallerTargetKind.Godot
                : InstallerTargetKind.Unity;
            var packageTarget = options.Mode == InstallerInstallMode.GodotLocal
                ? Path.Combine(options.TargetProjectRoot, "addons", "yokiframe", "package", "YokiFrame")
                : Path.Combine(options.TargetProjectRoot, "Packages", "com.hinatayoki.yokiframe");
            InstallerPlanPreview plan = new(
                engine,
                options.Mode,
                options.GitUrl ?? options.SourcePackageRoot ?? string.Empty,
                options.TargetProjectRoot,
                packageTarget,
                new[] { new InstallerPlanActionPreview(InstallerPlanActionKind.InstallPackage, packageTarget, null, "render") },
                Array.Empty<string>());
            return Task.FromResult(plan);
        }

        /// <summary>
        /// Headless 布局测试不执行安装；保留完整 gateway 契约供 ViewModel 使用。
        /// </summary>
        /// <param name="options">安装输入。</param>
        /// <param name="plan">安装计划。</param>
        /// <param name="progress">进度接收器。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>不会被当前测试调用的成功结果。</returns>
        public Task<InstallerExecutionResult> ExecuteAsync(
            InstallerInstallOptions options,
            InstallerPlanPreview plan,
            IProgress<InstallerProgressUpdate> progress,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new InstallerExecutionResult(plan.PackageTarget, true, false));
        }
    }
}
