namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 验证 Installer autoload 指向的 GodotBootstrap 文件和宿主宏生命周期边界。
/// </summary>
public sealed class GodotBootstrapArchitectureTests
{
    private const string BOOTSTRAP_RELATIVE_PATH =
        "Core/Adapters/Godot/Runtime/GodotBootstrap.cs";
    private const string FILE_BRIDGE_HOST_RELATIVE_PATH =
        "Core/Adapters/Godot/Runtime/FileBridge/GodotFileBridgeHost.cs";
    private const string NAMED_TELEMETRY_RELATIVE_PATH =
        "Core/Adapters/Godot/Runtime/FileBridge/Telemetry/GodotFileBridgeHost.NamedTelemetry.cs";
    private const string GODOT_RESOURCE_PROVIDER_RELATIVE_PATH =
        "Core/Adapters/Godot/Runtime/ResKit/GodotResourceProvider.cs";

    /// <summary>
    /// 验证准确 autoload 路径存在、整文件受 GODOT 宏保护，并仅通过 Node 生命周期组合 Host。
    /// </summary>
    [Fact]
    public void BootstrapUsesGodotNodeLifecycleAtInstallerAutoloadPath()
    {
        var bootstrapPath = Path.Combine(
            FindPackageRoot(),
            BOOTSTRAP_RELATIVE_PATH.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(bootstrapPath), "Installer autoload 缺少目标文件: " + bootstrapPath);
        var source = File.ReadAllText(bootstrapPath);

        Assert.StartsWith("#if GODOT", source.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("partial class GodotBootstrap : Node", source, StringComparison.Ordinal);
        Assert.Contains("GodotFileBridgeHost", source, StringComparison.Ordinal);
        Assert.Contains("override void _Ready()", source, StringComparison.Ordinal);
        Assert.Contains(
            "ResKit.RegisterDefaultProviderFactory(CreateDefaultResourceProvider);",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ResKit.TrySetDefaultProvider(new GodotResourceProvider())",
            source,
            StringComparison.Ordinal);
        Assert.Contains("override void _Process(double delta)", source, StringComparison.Ordinal);
        Assert.Contains("Time.GetTicksUsec()", source, StringComparison.Ordinal);
        Assert.Contains(
            "YokiFrameUpdateDispatcher.Tick(scaledDeltaTime, unscaledDeltaTime);",
            source,
            StringComparison.Ordinal);
        Assert.Contains("ProcessFastChannelRequestsSafely(mFileBridgeHost);", source, StringComparison.Ordinal);
        Assert.Contains("host.ProcessPendingFastChannelRequests();", source, StringComparison.Ordinal);
        var processStart = source.IndexOf("public override void _Process(double delta)", StringComparison.Ordinal);
        var dispatchIndex = source.IndexOf("YokiFrameUpdateDispatcher.Tick", processStart, StringComparison.Ordinal);
        var hostGuardIndex = source.IndexOf("if (mFileBridgeHost == null)", processStart, StringComparison.Ordinal);
        Assert.True(
            processStart >= 0 && dispatchIndex > processStart && dispatchIndex < hostGuardIndex,
            "Godot 必须先投递 Runtime 帧，再判断 FileBridge Host 是否在线。");
        Assert.Contains("override void _ExitTree()", source, StringComparison.Ordinal);
        Assert.Contains("YokiFrameUpdateDispatcher.ResetListeners();", source, StringComparison.Ordinal);
        Assert.EndsWith("#endif", source.TrimEnd(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Godot Runtime 宿主源码都使用整文件宏，防止其它宿主误编译 Godot API。
    /// </summary>
    [Fact]
    public void RuntimeHostFilesUseWholeFileGodotGuard()
    {
        var runtimeRoot = Path.Combine(
            FindPackageRoot(),
            "Core",
            "Adapters",
            "Godot",
            "Runtime");
        var hostFiles = Directory.EnumerateFiles(runtimeRoot, "Godot*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.EndsWith("AssemblyInfo.cs", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(hostFiles);
        foreach (var path in hostFiles)
        {
            var source = File.ReadAllText(path);
            Assert.StartsWith("#if GODOT", source.TrimStart(), StringComparison.Ordinal);
            Assert.EndsWith("#endif", source.TrimEnd(), StringComparison.Ordinal);
        }
    }

    /// <summary>验证 Snapshot-only Provider 能在 Godot Tools 写文件快照，但不会被提升为 Shared Memory Telemetry。</summary>
    [Fact]
    public void GodotHostSeparatesSnapshotOnlyProvidersFromTelemetryProviders()
    {
        string hostSource = ReadPackageSource(FILE_BRIDGE_HOST_RELATIVE_PATH);
        string telemetrySource = ReadPackageSource(NAMED_TELEMETRY_RELATIVE_PATH);

        Assert.Contains("RefreshChangedSnapshots()", hostSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshChangedFallbackSnapshots", hostSource, StringComparison.Ordinal);
        Assert.Contains("IYokiFrameSnapshotVersionedKitInteractionProvider", telemetrySource, StringComparison.Ordinal);
        Assert.Contains("provider is IYokiFrameVersionedKitInteractionProvider", telemetrySource, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 Godot 预加载会等待显式恢复后才实例化场景，避免逻辑预加载提前改变 SceneTree。
    /// </summary>
    [Fact]
    public void GodotSceneProviderDefersPreloadActivation()
    {
        string source = ReadPackageSource(GODOT_RESOURCE_PROVIDER_RELATIVE_PATH);

        Assert.Contains("request.IsPreload || request.SuspendAtProgress < 1f", source, StringComparison.Ordinal);
        Assert.Contains("operation.SetSuspended", source, StringComparison.Ordinal);
        Assert.Contains("resumeAction?.Invoke()", source, StringComparison.Ordinal);
        Assert.Contains("CompleteSceneLoad", source, StringComparison.Ordinal);
    }

    /// <summary>从包根读取指定相对源码，供结构边界断言使用。</summary>
    /// <param name="relativePath">相对于 YokiFrame 包根的路径。</param>
    /// <returns>源码全文。</returns>
    private static string ReadPackageSource(string relativePath)
    {
        string path = Path.Combine(FindPackageRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), "缺少 Godot Host 源码: " + path);
        return File.ReadAllText(path);
    }

    /// <summary>
    /// 从测试输出目录向上定位 YokiFrame 包根，支持本地构建和独立包布局。
    /// </summary>
    /// <returns>YokiFrame 包根绝对路径。</returns>
    private static string FindPackageRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "Assets", "YokiFrame");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法定位 YokiFrame 包根。");
    }
}
