using System.Text.Json;
using YokiFrame.Packaging.Services;

namespace YokiFrame.Packaging.Tests;

/// <summary>
/// 覆盖 manifest 合并读取器对损坏和结构无效缓存的降级行为。
/// </summary>
public sealed class RuntimeManifestReaderTests
{
    private const string VALID_HASH = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    /// 验证无法解析的 JSON 被视为不存在的旧 manifest，而不是中断新发布。
    /// </summary>
    [Fact]
    public void ReadReturnsNullForMalformedJson()
    {
        var path = WriteManifest("{broken");

        Assert.Null(new RuntimeManifestReader().ReadIfExists(path));
    }

    /// <summary>
    /// 验证缺少 platforms 的对象不能进入跨平台合并流程。
    /// </summary>
    [Fact]
    public void ReadReturnsNullWhenPlatformsAreMissing()
    {
        var path = WriteManifest("{\"manifestVersion\":1,\"layoutVersion\":2,\"product\":\"Tool\",\"runtimeRoot\":\".\"}");

        Assert.Null(new RuntimeManifestReader().ReadIfExists(path));
    }

    /// <summary>
    /// 验证文件大小累计溢出不会逃逸为异常，而会按损坏 manifest 处理。
    /// </summary>
    [Fact]
    public void ReadReturnsNullWhenFileSummaryOverflows()
    {
        var path = WriteManifest(CreateManifestJson(new[] { long.MaxValue, 1L }, long.MaxValue));

        Assert.Null(new RuntimeManifestReader().ReadIfExists(path));
    }

    /// <summary>
    /// 验证摘要内部一致的完整结构仍可被读取并用于后续物理完整性检查。
    /// </summary>
    [Fact]
    public void ReadAcceptsStructurallyValidManifest()
    {
        var path = WriteManifest(CreateManifestJson(new[] { 3L, 4L }, 7L));

        var manifest = new RuntimeManifestReader().ReadIfExists(path);

        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.Platforms[0].FileCount);
    }

    /// <summary>
    /// 生成指定文件大小摘要的结构化 manifest JSON。
    /// </summary>
    /// <param name="sizes">文件大小列表。</param>
    /// <param name="totalBytes">声明总大小。</param>
    /// <returns>manifest JSON。</returns>
    private static string CreateManifestJson(IReadOnlyList<long> sizes, long totalBytes)
    {
        var files = sizes.Select((size, index) => new
        {
            relativePath = "win-x64/file-" + index,
            sizeBytes = size,
            sha256 = VALID_HASH
        }).ToArray();
        return JsonSerializer.Serialize(new
        {
            manifestVersion = 1,
            layoutVersion = 2,
            product = "YokiFrameTool",
            runtimeRoot = ".",
            platforms = new[]
            {
                new
                {
                    platform = "win-x64",
                    runtimeIdentifier = "win-x64",
                    entrypoint = files[0].relativePath,
                    guiEntry = files[0].relativePath,
                    cliEntry = files[1].relativePath,
                    fileCount = files.Length,
                    totalBytes,
                    files
                }
            }
        });
    }

    /// <summary>
    /// 把 manifest 文本写入隔离临时文件。
    /// </summary>
    /// <param name="content">文件内容。</param>
    /// <returns>manifest 完整路径。</returns>
    private static string WriteManifest(string content)
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-manifest-reader-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "tool-manifest.json");
        File.WriteAllText(path, content);
        return path;
    }
}
