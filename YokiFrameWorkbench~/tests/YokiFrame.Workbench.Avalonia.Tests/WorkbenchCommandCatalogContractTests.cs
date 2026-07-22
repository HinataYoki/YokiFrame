using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 验证 Workbench 命令目录与 Runtime 协议契约保持一致。
/// </summary>
public sealed class WorkbenchCommandCatalogContractTests
{
    /// <summary>
    /// 验证命令目录过滤不向用户展示随后无法通过 FileBridge 发送的不安全标识。
    /// </summary>
    [Fact]
    public void CommandCatalogRejectsUnsafeIdentifiers()
    {
        var viewModel = new WorkbenchShellViewModel(() => { }, _ => { }, _ => Task.CompletedTask);
        var catalogJson = "{\"kits\":[{\"kit\":\".hidden\",\"actions\":[{\"action\":\"ping\"}]},{\"kit\":\"System\",\"actions\":[{\"action\":\"ping\"},{\"action\":\"bad..name\"},{\"action\":\"tail.\"}]}]}";

        viewModel.UpdateCommandCatalogJson(catalogJson);

        Assert.DoesNotContain(".hidden", viewModel.CommandGroups);
        viewModel.CommandGroup = "System";
        Assert.Contains("ping", viewModel.CommandActions);
        Assert.DoesNotContain("bad..name", viewModel.CommandActions);
        Assert.DoesNotContain("tail.", viewModel.CommandActions);
    }
}
