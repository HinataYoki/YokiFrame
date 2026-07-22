using YokiFrame.Installer.Core.Models;

namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 承载 Installer Core 执行令牌和当前 Application 输入的一致性校验。
/// </summary>
public sealed partial class InstallerCoreWorkflowGateway
{
    /// <summary>
    /// 校验预览由本 gateway 创建且仍与提交选项匹配。
    /// </summary>
    /// <param name="options">提交选项。</param>
    /// <param name="plan">待提交预览。</param>
    private static void ValidateExecutionToken(InstallerInstallOptions options, InstallerPlanPreview plan)
    {
        if (plan.ExecutionToken == null
            || plan.Mode != options.Mode
            || !ExecutionTokenMatchesOptions(plan.ExecutionToken, options))
        {
            throw new InvalidOperationException("Installer plan is missing or does not match the current options.");
        }
    }

    /// <summary>
    /// 校验 typed request 的全部执行输入仍与当前 Application 选项一致。
    /// </summary>
    /// <param name="executionToken">预览内部 typed token。</param>
    /// <param name="options">当前提交选项。</param>
    /// <returns>全部执行字段一致时返回 true。</returns>
    private static bool ExecutionTokenMatchesOptions(object executionToken, InstallerInstallOptions options)
    {
        return executionToken switch
        {
            UnityExecutionToken unity => UnityRequestMatchesOptions(unity.Request, options),
            GodotExecutionToken godot => GodotRequestMatchesOptions(godot.Request, options),
            _ => false
        };
    }

    /// <summary>
    /// 比较 Unity typed request 与当前模式、来源、目标和策略。
    /// </summary>
    /// <param name="request">预览时保存的 Core Unity 请求。</param>
    /// <param name="options">当前 Application 选项。</param>
    /// <returns>执行输入没有变化时返回 true。</returns>
    private static bool UnityRequestMatchesOptions(UnityInstallRequest request, InstallerInstallOptions options)
    {
        var expectedMode = options.Mode == InstallerInstallMode.UnityGit
            ? UnityInstallMode.GitUrl
            : UnityInstallMode.Embedded;
        var sourceMatches = expectedMode == UnityInstallMode.GitUrl
            || PathsEqual(request.SourcePackageRoot, options.SourcePackageRoot);
        return request.Mode == expectedMode
            && PathsEqual(request.ProjectRoot, options.TargetProjectRoot)
            && sourceMatches
            && string.Equals(request.RuntimeProfile, options.RuntimeProfile, StringComparison.Ordinal)
            && string.Equals(request.GitUrl, options.GitUrl, StringComparison.Ordinal)
            && request.UnmanagedPackagePolicy == MapLegacyPolicy(options.LegacyPackagePolicy);
    }

    /// <summary>
    /// 比较 Godot typed request 与当前路径、开关和接管策略。
    /// </summary>
    /// <param name="request">预览时保存的 Core Godot 请求。</param>
    /// <param name="options">当前 Application 选项。</param>
    /// <returns>执行输入没有变化时返回 true。</returns>
    private static bool GodotRequestMatchesOptions(GodotInstallRequest request, InstallerInstallOptions options)
    {
        var godotOptions = options.GodotOptions;
        return godotOptions != null
            && PathsEqual(request.SourcePackageRoot, options.SourcePackageRoot)
            && PathsEqual(request.ProjectRoot, options.TargetProjectRoot)
            && string.Equals(request.RuntimeProfile, options.RuntimeProfile, StringComparison.Ordinal)
            && request.RepairProjectSettings == godotOptions.RepairProjectSettings
            && request.EnablePlugin == godotOptions.EnablePlugin
            && request.UnmanagedPackagePolicy == MapLegacyPolicy(options.LegacyPackagePolicy);
    }

    /// <summary>
    /// 使用完整路径语义比较可能为相对路径的 Application 输入。
    /// </summary>
    /// <param name="left">已规范化或原始路径。</param>
    /// <param name="right">当前输入路径。</param>
    /// <returns>两个路径指向同一位置时返回 true。</returns>
    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right);
        }

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
    }

    /// <summary>
    /// 把 Application legacy 策略映射为 Core 所有权策略。
    /// </summary>
    /// <param name="policy">Application 策略。</param>
    /// <returns>Core 策略。</returns>
    private static UnmanagedPackagePolicy MapLegacyPolicy(InstallerLegacyPackagePolicy policy)
    {
        return policy switch
        {
            InstallerLegacyPackagePolicy.Reject => UnmanagedPackagePolicy.Reject,
            InstallerLegacyPackagePolicy.TakeOverConfirmed => UnmanagedPackagePolicy.TakeOverConfirmed,
            _ => throw new ArgumentOutOfRangeException(nameof(policy))
        };
    }
}
