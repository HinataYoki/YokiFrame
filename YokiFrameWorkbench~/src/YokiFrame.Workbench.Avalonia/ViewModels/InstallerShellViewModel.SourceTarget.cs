using YokiFrame.Tooling.Application.Installer;
using YokiFrame.Workbench.Avalonia.Services;

namespace YokiFrame.Workbench.Avalonia.ViewModels;

public sealed partial class InstallerShellViewModel
{
    private bool mRejectsOverlappingSource;

    /// <summary>
    /// 清空尚未选择的目标，并保持等待选择状态，避免把空路径显示成检测异常。
    /// </summary>
    private void ApplyMissingTarget()
    {
        mTargetKind = InstallerTargetKind.Unknown;
        ClearOverlappingSourceRejection();
        EngineStatusText = GetEngineText(InstallerTargetKind.Unknown);
        TargetStatusText = GetLocalizedText("String.Installer.TargetWaiting", "等待选择目录");
        SessionStatusText = GetLocalizedText("String.Installer.Session.Ready", "安装器已就绪");
        NotifyTargetPresentationChanged();
        RaiseCommandStates();
    }

    /// <summary>
    /// 拒绝源目录位于目标项目内的安装计划，防止覆盖开发项目中的 YokiFrame 源码。
    /// </summary>
    private void ApplyOverlappingSourceRejection()
    {
        mRejectsOverlappingSource = true;
        SessionStatusText = GetOverlappingSourceStatusText();
        AppendLocalLog(SessionStatusText);
        NotifyTargetPresentationChanged();
        RaiseCommandStates();
    }

    /// <summary>
    /// 清除源目录重叠拒绝标记，让后续合法目标可以重新生成计划。
    /// </summary>
    private void ClearOverlappingSourceRejection()
    {
        mRejectsOverlappingSource = false;
    }

    /// <summary>
    /// 返回当前语言下“不能安装到开发项目自身”的状态说明。
    /// </summary>
    /// <returns>面向安装页面的拒绝说明。</returns>
    private static string GetOverlappingSourceStatusText()
    {
        return GetLocalizedText(
            "String.Installer.SourceInsideTarget",
            InstallerSourceTargetRules.OverlapMessage);
    }
}
