namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 锁定 Installer 应用编排层的项目依赖边界。
/// </summary>
public sealed class InstallerApplicationArchitectureTests
{
    /// <summary>
    /// 验证 Tooling.Application 直接依赖 Installer.Core，且不把 Avalonia 带入应用层。
    /// </summary>
    [Fact]
    public void ToolingApplicationReferencesInstallerCoreWithoutAvalonia()
    {
        var projectSource = File.ReadAllText(FindApplicationProject()).Replace('\\', '/');

        Assert.Contains("../YokiFrame.Installer.Core/YokiFrame.Installer.Core.csproj", projectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Avalonia", projectSource, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 验证 Installer 公开契约不会把 Installer.Core 类型泄漏给 Avalonia 或 CLI。
    /// </summary>
    [Fact]
    public void InstallerPublicApiDoesNotExposeInstallerCoreTypes()
    {
        var installerTypes = typeof(YokiFrame.Tooling.Application.Installer.InstallerSessionService)
            .Assembly
            .GetExportedTypes()
            .Where(static type => type.Namespace == "YokiFrame.Tooling.Application.Installer")
            .ToArray();

        foreach (var type in installerTypes)
        {
            AssertNotCoreType(type.BaseType);
            Assert.All(type.GetConstructors(), static constructor =>
                Assert.All(constructor.GetParameters(), static parameter => AssertNotCoreType(parameter.ParameterType)));
            Assert.All(type.GetProperties(), static property => AssertNotCoreType(property.PropertyType));
            Assert.All(type.GetMethods(), static method =>
            {
                AssertNotCoreType(method.ReturnType);
                Assert.All(method.GetParameters(), static parameter => AssertNotCoreType(parameter.ParameterType));
            });
        }
    }

    /// <summary>
    /// 验证 Core 发现未迁移 Kit 引用时，Application 将其投影为可在 CLI 和 UI 展示的安装冲突。
    /// </summary>
    [Fact]
    public void UnsupportedKitReferencesAreProjectedAsInstallerConflicts()
    {
        var source = File.ReadAllText(FindApplicationSource("InstallerCoreWorkflowGateway.cs"));

        Assert.Contains("UnsupportedKitReferenceException", source, StringComparison.Ordinal);
        Assert.Contains("CreateConflict(exception)", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// 递归检查公开泛型、数组和普通类型是否来自 Installer.Core。
    /// </summary>
    /// <param name="type">待检查公开 API 类型。</param>
    private static void AssertNotCoreType(Type? type)
    {
        if (type == null)
        {
            return;
        }

        Assert.DoesNotContain("YokiFrame.Installer.Core", type.FullName ?? string.Empty, StringComparison.Ordinal);
        if (type.IsArray)
        {
            AssertNotCoreType(type.GetElementType());
        }

        foreach (var argument in type.GetGenericArguments())
        {
            AssertNotCoreType(argument);
        }
    }

    /// <summary>
    /// 从测试输出目录向上定位 Tooling.Application 项目文件。
    /// </summary>
    /// <returns>项目文件绝对路径。</returns>
    private static string FindApplicationProject()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "YokiFrame.Tooling.Application", "YokiFrame.Tooling.Application.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 YokiFrame.Tooling.Application.csproj。");
    }

    /// <summary>
    /// 从测试输出目录向上定位 Tooling.Application 源文件。
    /// </summary>
    /// <param name="fileName">需要定位的源文件名。</param>
    /// <returns>源文件绝对路径。</returns>
    private static string FindApplicationSource(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "YokiFrame.Tooling.Application",
                "Installer",
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("无法定位 Tooling.Application 源文件: " + fileName);
    }
}
