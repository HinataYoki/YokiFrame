using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 验证 Workbench 命令目录与 Runtime 协议契约保持一致。
/// </summary>
public sealed class WorkbenchCommandCatalogContractTests
{
    /// <summary>
    /// 验证不安全 Kit 和 action 会被过滤，目录只保留可发送的 System/ping。
    /// </summary>
    [Fact]
    public void UpdateCommandCatalogJson_含不安全标识_只保留System和ping()
    {
        using var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);
        var catalogJson = "{\"kits\":[{\"kit\":\".hidden\",\"actions\":[{\"action\":\"ping\"}]},{\"kit\":\"System\",\"actions\":[{\"action\":\"ping\"},{\"action\":\"bad..name\"},{\"action\":\"tail.\"}]}]}";

        viewModel.UpdateCommandCatalogJson(catalogJson);

        Assert.Equal(new[] { "System" }, viewModel.CommandGroups);
        Assert.Equal("System", viewModel.CommandGroup);
        Assert.Equal(new[] { "ping" }, viewModel.CommandActions);
    }
}
