namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 验证 Workbench 页面使用共享视觉组件语义，避免列表几何再次按 Kit 漂移。
/// </summary>
public sealed class WorkbenchVisualStyleContractTests
{
    /// <summary>
    /// 确认共享样式声明了选择列表、数据列表、时间线和执行树四种基础组件。
    /// </summary>
    [Fact]
    public void SharedKitStylesDefineVisualComponentSemantics()
    {
        var styles = WorkbenchContractTestFiles.ReadSource("Styles", "KitPage.axaml");

        Assert.Contains("ListBox.kit-selection-list ListBoxItem", styles);
        Assert.Contains("ListBox.kit-selection-list.rich ListBoxItem", styles);
        Assert.Contains("ListBox.kit-data-list ListBoxItem", styles);
        Assert.Contains("ListBox.kit-timeline-list ListBoxItem", styles);
        Assert.Contains("TreeView.kit-flow-tree", styles);
        Assert.Contains("Border.kit-flow-row", styles);
        Assert.Contains("Border.kit-data-row", styles);
    }

    /// <summary>
    /// 确认代表 Kit 页面已将领域列表映射到统一的交互密度，而不是继续依赖私有几何。
    /// </summary>
    [Fact]
    public void WorkbenchKitPagesUseSharedListSemantics()
    {
        var expectedClasses = new Dictionary<string, string[]>
        {
            ["EventKitPageView.axaml"] = ["kit-selection-list rich", "kit-timeline-list"],
            ["FsmKitPageView.axaml"] = ["kit-selection-list", "kit-timeline-list"],
            ["PoolKitPageView.axaml"] = ["kit-data-list", "kit-timeline-list"],
            ["ResKitPageView.axaml"] = ["kit-data-list", "kit-timeline-list"],
            ["ActionKitPageView.axaml"] = ["kit-data-list", "kit-flow-tree", "kit-flow-row"],
            ["LocalizationKitPageView.axaml"] = ["kit-selection-list", "kit-data-row"],
            ["AudioKitPageView.axaml"] = ["kit-data-list", "kit-timeline-list"],
            ["SpatialKitPageView.axaml"] = ["kit-selection-list"],
            ["UIKitPageView.axaml"] = ["kit-data-list"],
            ["TableKitDataView.axaml"] = ["kit-data-list"],
            ["SaveKitPageView.axaml"] = ["kit-data-list"]
        };

        foreach (var pair in expectedClasses)
        {
            var page = WorkbenchContractTestFiles.ReadSource("Views", "Pages", pair.Key);
            foreach (var className in pair.Value)
            {
                Assert.Contains(className, page);
            }
        }
    }
}
