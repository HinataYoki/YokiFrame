using YokiFrame.Tooling.Application.Models.UIKit;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 UIKit Workbench 只读文案与生成请求投影。
/// </summary>
public sealed class WorkbenchUIKitPresentationTests
{
    /// <summary>
    /// 验证面板键使用稳定分隔符，并与类型/名称组合唯一。
    /// </summary>
    [Fact]
    public void CreatePanelKeyUsesUnitSeparator()
    {
        WorkbenchUIKitPanel panel = new(
            "MainPanel",
            "Main",
            "Open",
            "Normal",
            0,
            0,
            "Reusable",
            false,
            null);
        string key = WorkbenchUIKitPresentation.CreatePanelKey(panel);
        Assert.Equal("MainPanel" + '' + "Main", key);
        Assert.Equal(string.Empty, WorkbenchUIKitPresentation.CreatePanelKey(null));
        Assert.Equal("显示 1 / 3", WorkbenchUIKitPresentation.CreateCoverageText(1, 3));
    }

    /// <summary>
    /// 验证生成请求字段从表单装入，并补默认模板名。
    /// </summary>
    [Fact]
    public void CreateGenerationRequestMapsFields()
    {
        WorkbenchUIKitPanelGenerationRequest request = WorkbenchUIKitPresentation.CreateGenerationRequest(
            "ShopPanel",
            "Assets/UI",
            "Assets/Scripts/UI",
            "Game.UI",
            "Assembly-CSharp",
            string.Empty,
            12L,
            "gid");
        Assert.Equal("ShopPanel", request.PanelName);
        Assert.Equal("Default", request.CodeTemplate);
        Assert.Equal(12L, request.ExpectedContextRevision);
        Assert.Equal(
            "创建面板预制体",
            WorkbenchUIKitPresentation.GetEditorActionDisplayName(WorkbenchUIKitEditorAction.CreatePanelPrefab));
    }
}
