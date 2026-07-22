using YokiFrame.Packaging.Models;
using YokiFrame.RuntimeCache;

namespace YokiFrame.Packaging.Services;

/// <summary>
/// 将可再生 Workbench Runtime 发布到项目 `.yokiframe` 缓存，并维护 sourceFingerprint 当前指针。
/// </summary>
public sealed class RuntimeCacheService
{
    private readonly RuntimeCachePointerStore mPointerStore = new();
    private readonly RuntimePublishPlanBuilder mPlanBuilder = new();
    private readonly RuntimePublishService mPublishService = new();

    /// <summary>
    /// 确保项目当前宿主 profile 存在且与源码指纹一致；缓存完整时只更新 current.json，不重复发布。
    /// </summary>
    /// <param name="packageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="projectRoot">目标 Unity 或 Godot 项目根。</param>
    /// <param name="configuration">dotnet 发布配置。</param>
    /// <returns>缓存指纹、正式入口和是否发生重建。</returns>
    public RuntimeCacheBootstrapResult Bootstrap(string packageRoot, string projectRoot, string configuration)
    {
        var fullPackageRoot = RequirePackageRoot(packageRoot);
        var fullProjectRoot = RequireProjectRoot(projectRoot);
        var sourceFingerprint = YokiFrameWorkbenchSourceFingerprint.Compute(fullPackageRoot);
        var runtimeCacheRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetCacheRoot(fullProjectRoot);
        using var publishLock = RuntimePublishLock.Acquire(runtimeCacheRoot);
        var plan = CreateCurrentPlan(fullPackageRoot, fullProjectRoot, configuration, sourceFingerprint);
        if (TryResolvePublishedResult(plan, out var cachedResult))
        {
            mPointerStore.Write(fullProjectRoot, sourceFingerprint);
            return new RuntimeCacheBootstrapResult(sourceFingerprint, plan.RuntimeRoot, cachedResult, rebuilt: false);
        }

        var publishResult = mPublishService.PublishWithLockHeld(plan);
        mPointerStore.Write(fullProjectRoot, sourceFingerprint);
        return new RuntimeCacheBootstrapResult(sourceFingerprint, plan.RuntimeRoot, publishResult, rebuilt: true);
    }

    /// <summary>
    /// 强制发布指定 profile 到项目缓存；用于用户显式构建目标平台，成功后同步 current.json。
    /// </summary>
    /// <param name="packageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="projectRoot">目标 Unity 或 Godot 项目根。</param>
    /// <param name="configuration">dotnet 发布配置。</param>
    /// <param name="runtimeIdentifier">受 allowlist 约束的目标 profile。</param>
    /// <param name="startupOptimized">是否为 managed Windows profile 启用 ReadyToRun。</param>
    /// <returns>缓存指纹与刚刚发布的入口结果。</returns>
    public RuntimeCacheBootstrapResult Publish(
        string packageRoot,
        string projectRoot,
        string configuration,
        string runtimeIdentifier,
        bool startupOptimized)
    {
        var fullPackageRoot = RequirePackageRoot(packageRoot);
        var sourceFingerprint = YokiFrameWorkbenchSourceFingerprint.Compute(fullPackageRoot);
        var fullProjectRoot = RequireProjectRoot(projectRoot);
        var runtimeCacheRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetCacheRoot(fullProjectRoot);
        using var publishLock = RuntimePublishLock.Acquire(runtimeCacheRoot);
        var runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(fullProjectRoot, sourceFingerprint);
        var plan = mPlanBuilder.Build(
            fullPackageRoot,
            runtimeRoot,
            configuration,
            runtimeIdentifier,
            startupOptimized);
        var publishResult = mPublishService.PublishWithLockHeld(plan);
        mPointerStore.Write(fullProjectRoot, sourceFingerprint);
        return new RuntimeCacheBootstrapResult(sourceFingerprint, runtimeRoot, publishResult, rebuilt: true);
    }

    /// <summary>
    /// 为当前宿主创建受项目边界约束的发布计划。
    /// </summary>
    /// <param name="packageRoot">只读 YokiFrame 源码包根。</param>
    /// <param name="projectRoot">目标项目根。</param>
    /// <param name="configuration">dotnet 发布配置。</param>
    /// <param name="sourceFingerprint">实际 Workbench 源码指纹。</param>
    /// <returns>当前宿主发布计划。</returns>
    private RuntimePublishPlan CreateCurrentPlan(
        string packageRoot,
        string projectRoot,
        string configuration,
        string sourceFingerprint)
    {
        var fullProjectRoot = RequireProjectRoot(projectRoot);
        var runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(fullProjectRoot, sourceFingerprint);
        return mPlanBuilder.BuildCurrent(packageRoot, runtimeRoot, configuration);
    }

    /// <summary>
    /// 检查当前 fingerprint 目录是否已有与目标 profile 匹配的 manifest 和 GUI/CLI 入口。
    /// </summary>
    /// <param name="plan">当前 Runtime 发布计划。</param>
    /// <param name="result">缓存完整时返回可直接启动的入口结果。</param>
    /// <returns>缓存可复用时返回 true。</returns>
    private bool TryResolvePublishedResult(RuntimePublishPlan plan, out RuntimePublishResult result)
    {
        result = null!;
        if (!RuntimeManifestIntegrityValidator.TryValidateProfile(
                plan.ManifestPath,
                plan.RuntimeRoot,
                plan.Profile.RuntimeIdentifier,
                plan.Profile.PublishCli,
                out var profile,
                out _))
        {
            return false;
        }

        result = new RuntimePublishResult(
            plan.Profile.RuntimeIdentifier,
            plan.PublishRoot,
            profile.GuiPath,
            profile.CliPath,
            plan.ManifestPath);
        return true;
    }

    /// <summary>
    /// 验证项目根已经存在，避免 Packaging 在错误路径旁新建孤立 `.yokiframe` 目录。
    /// </summary>
    /// <param name="projectRoot">待验证的项目根。</param>
    /// <returns>规范化完整路径。</returns>
    private static string RequireProjectRoot(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ArgumentException("Project root is required.", nameof(projectRoot));
        }

        var fullProjectRoot = Path.GetFullPath(projectRoot);
        return Directory.Exists(fullProjectRoot)
            ? fullProjectRoot
            : throw new DirectoryNotFoundException("Target project root was not found: " + fullProjectRoot);
    }

    /// <summary>
    /// 验证只读源码包根已经存在，确保命令参数错误不会被后续构建输入检查掩盖。
    /// </summary>
    /// <param name="packageRoot">待验证的 YokiFrame 源码包根。</param>
    /// <returns>规范化完整路径。</returns>
    private static string RequirePackageRoot(string packageRoot)
    {
        if (string.IsNullOrWhiteSpace(packageRoot))
        {
            throw new ArgumentException("YokiFrame package root is required.", nameof(packageRoot));
        }

        var fullPackageRoot = Path.GetFullPath(packageRoot);
        return Directory.Exists(fullPackageRoot)
            ? fullPackageRoot
            : throw new DirectoryNotFoundException("YokiFrame package root was not found: " + fullPackageRoot);
    }
}
