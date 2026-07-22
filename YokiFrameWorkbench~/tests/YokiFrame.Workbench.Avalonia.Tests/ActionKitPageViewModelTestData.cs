using YokiFrame.Tooling.Application.Models.ActionKit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>提供 ActionKit 页面测试共用的默认强类型模型。</summary>
public sealed partial class ActionKitPageViewModelTests
{
    /// <summary>创建两个具有稳定 ID、可选子节点和调用帧的默认根动作。</summary>
    /// <param name="includeChildren">是否为两个根加入同一个 Delay 子节点模型。</param>
    /// <param name="rootStatus">两个根的生命周期状态。</param>
    /// <param name="childDebugInfo">Delay 子节点的诊断摘要。</param>
    /// <returns>供页面状态工厂使用的两个根动作。</returns>
    private static IReadOnlyList<WorkbenchActionKitRoot> CreateDefaultRoots(
        bool includeChildren,
        string rootStatus,
        string childDebugInfo)
    {
        WorkbenchActionKitNode child = new(
            "12", "Delay", "Started", false, false, childDebugInfo,
            Array.Empty<WorkbenchActionKitNode>());
        IReadOnlyList<WorkbenchActionKitNode> children = includeChildren
            ? new[] { child }
            : Array.Empty<WorkbenchActionKitNode>();
        return new WorkbenchActionKitRoot[]
        {
            new("41", "Parallel", rootStatus, false, false, "Parallel(2)",
                "ScaledDeltaTime", false, Array.Empty<WorkbenchActionKitStackFrame>(), children),
            new("9007199254740993", "Sequence", rootStatus, false, false, "Sequence(2)",
                "UnscaledDeltaTime", false,
                new[] { new WorkbenchActionKitStackFrame("Sample.Start", "Sample.cs", 18) },
                children)
        };
    }

    /// <summary>创建指定数量、ID 与帧号稳定递增的默认完成事件。</summary>
    /// <param name="eventCount">需要创建的事件数量。</param>
    /// <returns>最新页面状态使用的完成事件。</returns>
    private static IReadOnlyList<WorkbenchActionKitEvent> CreateDefaultEvents(int eventCount)
    {
        return Enumerable.Range(0, eventCount)
            .Select(index => new WorkbenchActionKitEvent(
                (20 + index).ToString(), "Callback", "Completed", 100 + index, string.Empty))
            .ToArray();
    }
}
