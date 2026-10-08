using YokiFrame.Tooling.Application.Installer;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 锁定 Installer 应用编排层的项目依赖边界。
/// </summary>
public sealed class InstallerApplicationArchitectureTests
{
    /// <summary>
    /// 源码结构守卫：csproj 直接引用 Installer.Core，且不把 Avalonia 带入应用层。
    /// </summary>
    [Fact]
    public void ToolingApplicationCsproj_引用InstallerCore_SourceGuard不含Avalonia()
    {
        var projectSource = File.ReadAllText(FindApplicationProject()).Replace('\\', '/');

        Assert.Contains("../YokiFrame.Installer.Core/YokiFrame.Installer.Core.csproj", projectSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Avalonia", projectSource, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 程序集边界：只检查 Installer 公开类型表面，不把 Installer.Core 类型泄漏给 Avalonia 或 CLI。
    /// </summary>
    [Fact]
    public void Installer公开类型_程序集边界_AssemblyBoundary不暴露Core类型()
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
    /// 验证 Godot 脚本引用当前源包没有的 Kit 时，预览把 Core 拒绝投影为 Application 冲突。
    /// </summary>
    [Fact]
    public async Task CreatePlanAsync_Godot脚本引用缺失Kit_投影为安装冲突()
    {
        using InstallerApplicationFixture fixture = InstallerApplicationFixture.Create();
        File.WriteAllText(
            Path.Combine(fixture.GodotProjectRoot, "LegacyAudio.cs"),
            "using YokiFrame;\npublic sealed class LegacyAudio { void Run() => AudioKit.Play(); }\n");
        InstallerInstallOptions options = InstallerInstallOptions.CreateGodotLocal(
            fixture.SourcePackageRoot,
            fixture.GodotProjectRoot,
            new GodotInstallOptions(repairProjectSettings: false, enablePlugin: false),
            InstallerLegacyPackagePolicy.Reject);

        InstallerConflictException exception = await Assert.ThrowsAsync<InstallerConflictException>(() =>
            new YokiFrame.Tooling.Application.Installer.InstallerCoreWorkflowGateway().CreatePlanAsync(
                options,
                CancellationToken.None));

        Assert.Equal("LegacyAudio.cs:2 [AudioKit]", Assert.Single(exception.ConflictPaths));
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
}
