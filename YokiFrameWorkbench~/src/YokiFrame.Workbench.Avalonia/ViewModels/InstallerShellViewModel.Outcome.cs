using System.Globalization;
using YokiFrame.Tooling.Application.Installer;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

/// <summary>承载 Installer 完成、冲突和失败摘要投影。</summary>
public sealed partial class InstallerShellViewModel
{
    /// <summary>
    /// 清除已失效的成功摘要，使下一次计划或错误状态不会复用旧事务结果。
    /// </summary>
    private void ClearCompletionSummary()
    {
        mPresentedResult = null;
        CompletionSummaryText = string.Empty;
        IsCompletionSummaryVisible = false;
    }

    /// <summary>
    /// 根据统一计划和执行结果创建可审阅的完成摘要，不伪造 Core 未提供的复制或配置数量。
    /// </summary>
    /// <param name="plan">已执行的安装计划。</param>
    /// <param name="result">Core 提交后的统一结果。</param>
    /// <param name="options">执行时使用的安装输入。</param>
    /// <returns>供页面显示的多行摘要。</returns>
    private static string CreateCompletionSummary(
        InstallerPlanPreview plan,
        InstallerExecutionResult result,
        InstallerInstallOptions? options)
    {
        var changeText = result.Changed
            ? GetLocalizedText("String.Installer.Summary.Changed", "已提交变更")
            : GetLocalizedText("String.Installer.Summary.NoChanges", "无需写入变更");
        if (result.CommittedNeedsVerification)
        {
            changeText += GetLocalizedText(
                "String.Installer.Summary.NeedsVerificationSuffix",
                "，但宿主 post-verify 尚未完成");
        }
        if (result.ReplacedExistingPackage)
        {
            changeText += GetLocalizedText(
                "String.Installer.Summary.ReplacedSuffix",
                "，已替换既有安装来源");
        }

        List<string> lines = new()
        {
            FormatLocalizedText("String.Installer.Summary.Engine", "引擎: {0}", GetEngineText(plan.Engine)),
            FormatLocalizedText("String.Installer.Summary.Mode", "模式: {0}", GetModeText(plan.Mode)),
            FormatLocalizedText("String.Installer.Summary.Platform", "平台: {0}", GetCurrentPlatformText()),
            FormatLocalizedText("String.Installer.Summary.Target", "目标: {0}", result.TargetPath),
            FormatLocalizedText("String.Installer.Summary.Result", "结果: {0}", changeText),
            FormatLocalizedText("String.Installer.Summary.ActionCount", "计划动作: {0} 项", plan.Actions.Count),
            FormatLocalizedText("String.Installer.Summary.EvidenceCount", "校验证据: {0} 项", result.EvidencePaths.Count)
        };
        if (plan.Engine == InstallerTargetKind.Godot)
        {
            var pluginEnabled = options?.GodotOptions?.EnablePlugin == true;
            lines.Add(pluginEnabled
                ? GetLocalizedText(
                    "String.Installer.Summary.GodotPluginEnabled",
                    "Godot: 请刷新文件系统并确认插件已启用；之后可从 Project > Tools > YokiFrame > Open Workbench 或按 Ctrl+E 打开工作台，系统热键冲突时按 Ctrl+Alt+E。")
                : GetLocalizedText(
                    "String.Installer.Summary.GodotPluginDisabled",
                    "Godot: 请刷新文件系统；当前计划未自动启用 YokiFrame 插件。"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// 把冲突路径、事务回滚结果和诊断证据投影为页面可直接审阅的详情。
    /// </summary>
    /// <param name="state">最新 Installer 会话状态。</param>
    private void ApplyOutcomeDetails(InstallerSessionState state)
    {
        if (mIsGodotRuntimeBootstrapRunning)
        {
            ClearOutcomeDetails();
            return;
        }

        if (state.Status == InstallerSessionStatus.Conflict)
        {
            OutcomeDetailsTitle = GetLocalizedText("String.Installer.Outcome.ConflictTitle", "安装冲突");
            OutcomeDetailsText = CreateConflictDetails(state);
            IsOutcomeDetailsVisible = true;
            return;
        }

        if (state.Status == InstallerSessionStatus.Failed)
        {
            OutcomeDetailsTitle = GetLocalizedText("String.Installer.Outcome.FailedTitle", "安装失败");
            OutcomeDetailsText = CreateFailureDetails(state);
            IsOutcomeDetailsVisible = true;
            return;
        }

        if (state.Status == InstallerSessionStatus.CommittedNeedsVerification)
        {
            OutcomeDetailsTitle = GetLocalizedText(
                "String.Installer.Outcome.CommittedNeedsVerificationTitle",
                "已提交但待验证");
            OutcomeDetailsText = string.IsNullOrWhiteSpace(state.ErrorMessage)
                ? GetLocalizedText(
                    "String.Installer.Outcome.CommittedNeedsVerificationText",
                    "Core 已完成写入，但宿主构建或插件登记尚未完成。请修复构建问题后重新验证。")
                : state.ErrorMessage;
            IsOutcomeDetailsVisible = true;
            return;
        }

        ClearOutcomeDetails();
    }

    /// <summary>
    /// 清除当前事务的冲突或失败详情，避免把已经解决的前置错误继续显示给用户。
    /// </summary>
    private void ClearOutcomeDetails()
    {
        OutcomeDetailsTitle = string.Empty;
        OutcomeDetailsText = string.Empty;
        IsOutcomeDetailsVisible = false;
    }

    /// <summary>
    /// 创建包含错误说明和全部稳定冲突路径的文本。
    /// </summary>
    /// <param name="state">冲突状态。</param>
    /// <returns>多行冲突详情。</returns>
    private static string CreateConflictDetails(InstallerSessionState state)
    {
        List<string> lines = new() { state.ErrorMessage };
        if (state.ConflictPaths.Count > 0)
        {
            lines.Add(GetLocalizedText("String.Installer.Outcome.ConflictPaths", "冲突路径:"));
            lines.AddRange(state.ConflictPaths.Select(static path => "- " + path));
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// 创建包含失败说明、回滚结论和持久化诊断证据的文本。
    /// </summary>
    /// <param name="state">失败状态。</param>
    /// <returns>多行失败详情。</returns>
    private static string CreateFailureDetails(InstallerSessionState state)
    {
        List<string> lines = new() { state.ErrorMessage };
        if (state.RollbackSucceeded.HasValue)
        {
            lines.Add(state.RollbackSucceeded.Value
                ? GetLocalizedText("String.Installer.Outcome.RollbackSucceeded", "回滚成功，已恢复安装前状态。")
                : GetLocalizedText("String.Installer.Outcome.RollbackIncomplete", "回滚未完整完成，需要人工检查目标项目。"));
        }

        if (state.EvidencePaths.Count > 0)
        {
            lines.Add(GetLocalizedText("String.Installer.Outcome.EvidencePaths", "诊断证据:"));
            lines.AddRange(state.EvidencePaths.Select(static path => "- " + path));
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// 首次看到一个新计划时把来源、目标和动作列表追加到日志，形成可审阅预览。
    /// </summary>
}
