using System.Xml.Linq;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 守卫 GUI、CLI 与可移植工具项目的 Native AOT 编译开关边界。
/// </summary>
public sealed class PublishScriptTests
{
    /// <summary>
    /// 验证可执行入口只在显式开关为真时映射 PublishAot。
    /// </summary>
    /// <param name="projectName">可执行入口项目名。</param>
    [Theory]
    [InlineData("YokiFrame.Workbench.Avalonia")]
    [InlineData("YokiFrame.Cli")]
    public void AssemblyBoundary_可执行入口_仅在开关为真时映射PublishAot(string projectName)
    {
        XDocument project = ReadSourceProject(projectName);
        XElement publishAot = Assert.Single(project.Descendants("PublishAot"));

        Assert.Equal("true", publishAot.Value);
        Assert.Equal("'$(YokiFramePublishAot)' == 'true'", (string?)publishAot.Attribute("Condition"));
    }

    /// <summary>
    /// 验证承载生成器的 Application 项目不映射 PublishAot。
    /// </summary>
    [Fact]
    public void AssemblyBoundary_可移植工具项目_不映射PublishAot()
    {
        XDocument project = ReadSourceProject("YokiFrame.Tooling.Application");

        Assert.Empty(project.Descendants("PublishAot"));
    }

    /// <summary>
    /// 读取 Workbench src 下指定项目文件。
    /// </summary>
    /// <param name="projectName">目录名及项目名。</param>
    /// <returns>已解析的项目 XML。</returns>
    private static XDocument ReadSourceProject(string projectName)
    {
        var path = Path.Combine(
            FindPackageRoot(),
            "YokiFrameWorkbench~",
            "src",
            projectName,
            projectName + ".csproj");
        Assert.True(File.Exists(path), "Missing Workbench project: " + path);
        return XDocument.Load(path);
    }

    /// <summary>
    /// 从当前测试进程目录向上定位 YokiFrame 包根。
    /// </summary>
    /// <returns>YokiFrame 包根目录。</returns>
    private static string FindPackageRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            var packageRoot = Path.Combine(current.FullName, "Assets", "YokiFrame");
            if (Directory.Exists(Path.Combine(packageRoot, "YokiFrameWorkbench~", "scripts")))
            {
                return packageRoot;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate YokiFrame package root.");
    }
}
