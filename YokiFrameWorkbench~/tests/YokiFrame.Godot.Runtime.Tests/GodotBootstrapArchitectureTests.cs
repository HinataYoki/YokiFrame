namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 验证 Godot Runtime 宿主源码的整文件宏边界，防止其它宿主误编译 Godot API。
/// </summary>
public sealed class GodotBootstrapArchitectureTests
{
    /// <summary>
    /// 验证每个 Godot Runtime 宿主文件都以整文件 GODOT 宏包裹。
    /// </summary>
    [Fact]
    public void RuntimeHostFiles_WholeFileGodotMacro_SourceGuard()
    {
        string runtimeRoot = Path.Combine(
            FindPackageRoot(),
            "Core",
            "Adapters",
            "Godot",
            "Runtime");
        string[] hostFiles = Directory.EnumerateFiles(runtimeRoot, "Godot*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.EndsWith("AssemblyInfo.cs", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(hostFiles);
        foreach (string path in hostFiles)
        {
            string source = File.ReadAllText(path);
            Assert.StartsWith("#if GODOT", source.TrimStart(), StringComparison.Ordinal);
            Assert.EndsWith("#endif", source.TrimEnd(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 从测试输出目录向上定位 YokiFrame 包根，支持本地构建和独立包布局。
    /// </summary>
    /// <returns>YokiFrame 包根绝对路径。</returns>
    private static string FindPackageRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "Assets", "YokiFrame");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法定位 YokiFrame 包根。");
    }
}
