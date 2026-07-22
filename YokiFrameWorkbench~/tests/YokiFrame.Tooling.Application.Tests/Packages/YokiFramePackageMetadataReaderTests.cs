using YokiFrame.Tooling.Application.Packages;

namespace YokiFrame.Tooling.Application.Tests.Packages;

/// <summary>
/// 覆盖 YokiFrame 包版本和仓库主页从 package.json 到强类型模型的解析契约。
/// </summary>
public sealed class YokiFramePackageMetadataReaderTests
{
    /// <summary>
    /// 验证对象形式的 repository.url 被读取，并移除只适用于 Git 克隆的 .git 后缀。
    /// </summary>
    [Fact]
    public void ReadParsesVersionAndRepositoryHomepage()
    {
        var packageRoot = CreatePackageRoot(
            "{\"version\":\"2.0.0-test\",\"repository\":{\"type\":\"git\",\"url\":\"https://github.com/HinataYoki/YokiFrame.git\"}}");

        var metadata = YokiFramePackageMetadataReader.Read(packageRoot);

        Assert.Equal("2.0.0-test", metadata.Version);
        Assert.Equal("https://github.com/HinataYoki/YokiFrame", metadata.RepositoryUri.AbsoluteUri.TrimEnd('/'));
    }

    /// <summary>
    /// 验证字符串形式的 repository 仍按 Unity package.json 常见写法解析。
    /// </summary>
    [Fact]
    public void ReadSupportsStringRepository()
    {
        var packageRoot = CreatePackageRoot(
            "{\"version\":\"2.0.0-test\",\"repository\":\"https://github.com/HinataYoki/YokiFrame\"}");

        var metadata = YokiFramePackageMetadataReader.Read(packageRoot);

        Assert.Equal("https://github.com/HinataYoki/YokiFrame", metadata.RepositoryUri.AbsoluteUri.TrimEnd('/'));
    }

    /// <summary>
    /// 验证非 HTTPS 仓库地址不能进入外部浏览器启动边界。
    /// </summary>
    [Fact]
    public void ReadRejectsNonHttpsRepository()
    {
        var packageRoot = CreatePackageRoot(
            "{\"version\":\"2.0.0-test\",\"repository\":{\"url\":\"file:///tmp/YokiFrame\"}}");

        var exception = Assert.Throws<InvalidDataException>(
            () => YokiFramePackageMetadataReader.Read(packageRoot));

        Assert.Contains("HTTPS", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 创建带指定 package.json 的独立临时包根，避免测试依赖工作区实际版本。
    /// </summary>
    /// <param name="packageJson">需要写入的完整 JSON。</param>
    /// <returns>临时包根绝对路径。</returns>
    private static string CreatePackageRoot(string packageJson)
    {
        var packageRoot = Path.Combine(
            Path.GetTempPath(),
            "yokiframe-package-metadata-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(packageRoot);
        File.WriteAllText(Path.Combine(packageRoot, "package.json"), packageJson);
        return packageRoot;
    }
}
