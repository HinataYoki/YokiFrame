using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>
/// 锁定 Workbench 程序集只通过 Tooling.Application 到达安装能力。
/// </summary>
public sealed class WorkbenchSkillApplicationBoundaryTests
{
    /// <summary>
    /// 验证已编译 Workbench 程序集直接引用 Tooling.Application，且不引用 Installer.Core。
    /// </summary>
    [Fact]
    public void GetReferencedAssemblies_AssemblyBoundary_引用Application且不引用InstallerCore()
    {
        var references = typeof(WorkbenchShellViewModel).Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name)
            .ToArray();

        Assert.Contains("YokiFrame.Tooling.Application", references);
        Assert.DoesNotContain("YokiFrame.Installer.Core", references);
    }
}
