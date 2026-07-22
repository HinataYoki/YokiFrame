using YokiFrame.Tooling.Application.Models.EventKit.Scan;
using YokiFrame.Workbench.Avalonia.ViewModels.EventKit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 EventKit 静态发送、注册与注销关系的可见健康状态。</summary>
public sealed class EventKitHealthStatusTests
{
    /// <summary>验证常见调用点组合映射为明确且不只依赖颜色的状态文本。</summary>
    [Theory]
    [InlineData(1, 0, 0, "仅发送，未发现注册", "无需判断注册注销", false)]
    [InlineData(0, 1, 0, "仅注册，未发现发送", "已注册，未发现注销", true)]
    [InlineData(1, 1, 1, "发送与注册均存在", "注册/注销数量平衡", false)]
    [InlineData(1, 2, 1, "发送与注册均存在", "注册多于注销", false)]
    [InlineData(1, 1, 2, "发送与注册均存在", "注销多于注册", false)]
    public void StaticRelationCountsCreateExpectedHealthText(
        int senderCount,
        int receiverCount,
        int unregisterCount,
        string expectedCoverage,
        string expectedBalance,
        bool expectedMissingUnregister)
    {
        var relation = new WorkbenchEventKitCodeRelation(
            "Type",
            "Demo.DamageEvent",
            "Demo.DamageEvent",
            CreateLocations(senderCount),
            CreateLocations(receiverCount),
            CreateLocations(unregisterCount));

        EventKitEventListItemViewModel item = new(relation);

        Assert.Equal(expectedCoverage, item.FlowCoverageText);
        Assert.Equal(expectedBalance, item.LifetimeBalanceText);
        Assert.Equal(expectedMissingUnregister, item.HasMissingUnregister);
    }

    /// <summary>创建指定数量且行号稳定的同文件调用点。</summary>
    private static WorkbenchEventKitCodeLocation[] CreateLocations(int count)
    {
        WorkbenchEventKitCodeLocation[] result = new WorkbenchEventKitCodeLocation[count];
        for (var index = 0; index < count; index++)
        {
            result[index] = new WorkbenchEventKitCodeLocation("Assets/Combat/DamageFlow.cs", index + 1);
        }

        return result;
    }
}
