using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests;

/// <summary>
/// 覆盖 Installer 对项目 Runtime 缓存完整性与源码指纹绑定的强制门禁。
/// </summary>
public sealed class RuntimeCacheBindingValidatorTests
{
    private const string RUNTIME_PROFILE = "win-x64";

    /// <summary>
    /// 验证源码指纹、指针、manifest 和物理文件均一致时允许继续安装。
    /// </summary>
    [Fact]
    public void ValidateAcceptsCompleteRuntimeCache()
    {
        var fixture = CreateFixture();

        new RuntimeCacheBindingValidator().Validate(fixture.ProjectRoot, fixture.SourceRoot, RUNTIME_PROFILE);
    }

    /// <summary>
    /// 验证各种摘要和入口破坏都会转换为带恢复动作的 bootstrap 前置条件错误。
    /// </summary>
    /// <param name="corruption">待施加的 manifest 损坏类型。</param>
    [Theory]
    [InlineData("hash")]
    [InlineData("size")]
    [InlineData("fileCount")]
    [InlineData("totalBytes")]
    [InlineData("entry")]
    public void ValidateRejectsCorruptedManifest(string corruption)
    {
        var fixture = CreateFixture();
        CorruptManifest(fixture.Manifest, corruption);
        File.WriteAllText(fixture.ManifestPath, fixture.Manifest.ToJsonString());

        var exception = Assert.Throws<InvalidDataException>(() =>
            new RuntimeCacheBindingValidator().Validate(fixture.ProjectRoot, fixture.SourceRoot, RUNTIME_PROFILE));

        Assert.Contains("Runtime 缓存 manifest 无效", exception.Message, StringComparison.Ordinal);
        Assert.Contains("构建 Runtime", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证无法解析的 manifest 同样要求重新 bootstrap，而不会泄漏 JSON 异常。
    /// </summary>
    [Fact]
    public void ValidateRejectsMalformedManifest()
    {
        var fixture = CreateFixture();
        File.WriteAllText(fixture.ManifestPath, "{broken");

        var exception = Assert.Throws<InvalidDataException>(() =>
            new RuntimeCacheBindingValidator().Validate(fixture.ProjectRoot, fixture.SourceRoot, RUNTIME_PROFILE));

        Assert.Contains("Runtime 缓存 manifest 无效", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 创建指纹、指针与双入口 manifest 完整一致的最小项目缓存。
    /// </summary>
    /// <returns>可供单项破坏的测试现场。</returns>
    private static RuntimeCacheFixture CreateFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "yokiframe-installer-cache-tests", Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(root, "source", "YokiFrame");
        var projectRoot = Path.Combine(root, "project");
        var sourceFile = Path.Combine(sourceRoot, "YokiFrameWorkbench~", "src", "Fixture.cs");
        Directory.CreateDirectory(projectRoot);
        WriteText(sourceFile, "namespace Fixture; internal sealed class Marker { }");
        var fingerprint = YokiFrameWorkbenchSourceFingerprint.Compute(sourceRoot);
        var runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(projectRoot, fingerprint);
        var guiEntry = RUNTIME_PROFILE + "/Workbench.exe";
        var cliEntry = RUNTIME_PROFILE + "/yoki.exe";
        var guiPath = Path.Combine(runtimeRoot, guiEntry.Replace('/', Path.DirectorySeparatorChar));
        var cliPath = Path.Combine(runtimeRoot, cliEntry.Replace('/', Path.DirectorySeparatorChar));
        WriteText(guiPath, "runtime-gui");
        WriteText(cliPath, "runtime-cli");
        var manifest = CreateManifest(guiEntry, guiPath, cliEntry, cliPath);
        var manifestPath = Path.Combine(runtimeRoot, "tool-manifest.json");
        WriteText(manifestPath, manifest.ToJsonString());
        WriteText(
            YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(projectRoot),
            JsonSerializer.Serialize(new { layoutVersion = 1, sourceFingerprint = fingerprint }));
        return new RuntimeCacheFixture(sourceRoot, projectRoot, manifestPath, manifest);
    }

    /// <summary>
    /// 创建包含两个入口文件完整摘要的 manifest JSON 对象。
    /// </summary>
    /// <param name="guiEntry">GUI 相对入口。</param>
    /// <param name="guiPath">GUI 完整路径。</param>
    /// <param name="cliEntry">CLI 相对入口。</param>
    /// <param name="cliPath">CLI 完整路径。</param>
    /// <returns>可编辑的 manifest 对象。</returns>
    private static JsonObject CreateManifest(string guiEntry, string guiPath, string cliEntry, string cliPath)
    {
        var files = new[]
        {
            new { relativePath = guiEntry, sizeBytes = new FileInfo(guiPath).Length, sha256 = ComputeSha256(guiPath) },
            new { relativePath = cliEntry, sizeBytes = new FileInfo(cliPath).Length, sha256 = ComputeSha256(cliPath) }
        };
        return JsonSerializer.SerializeToNode(new
        {
            manifestVersion = 1,
            layoutVersion = 2,
            product = "YokiFrameTool",
            runtimeRoot = ".",
            platforms = new[]
            {
                new
                {
                    platform = RUNTIME_PROFILE,
                    runtimeIdentifier = RUNTIME_PROFILE,
                    entrypoint = guiEntry,
                    guiEntry,
                    cliEntry,
                    fileCount = files.Length,
                    totalBytes = files.Sum(static file => file.sizeBytes),
                    files
                }
            }
        })!.AsObject();
    }

    /// <summary>
    /// 仅破坏指定 manifest 字段，确保每个测试都验证单一失败原因。
    /// </summary>
    /// <param name="manifest">待修改 manifest。</param>
    /// <param name="corruption">损坏类型。</param>
    private static void CorruptManifest(JsonObject manifest, string corruption)
    {
        var platform = manifest["platforms"]![0]!.AsObject();
        var files = platform["files"]!.AsArray();
        switch (corruption)
        {
            case "hash":
                files[0]!["sha256"] = new string('0', 64);
                break;
            case "size":
                files[0]!["sizeBytes"] = files[0]!["sizeBytes"]!.GetValue<long>() + 1L;
                break;
            case "fileCount":
                platform["fileCount"] = files.Count + 1;
                break;
            case "totalBytes":
                platform["totalBytes"] = platform["totalBytes"]!.GetValue<long>() + 1L;
                break;
            case "entry":
                platform["guiEntry"] = RUNTIME_PROFILE + "/not-listed.exe";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(corruption), corruption, "Unknown corruption type.");
        }
    }

    /// <summary>
    /// 写入测试文本并自动建立父目录。
    /// </summary>
    /// <param name="path">目标完整路径。</param>
    /// <param name="content">测试文本。</param>
    private static void WriteText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// 计算测试运行文件 SHA-256。
    /// </summary>
    /// <param name="path">文件完整路径。</param>
    /// <returns>小写十六进制 SHA-256。</returns>
    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// 保存 Installer Runtime 缓存测试的路径与可编辑 manifest。
    /// </summary>
    /// <param name="SourceRoot">源码包根。</param>
    /// <param name="ProjectRoot">目标项目根。</param>
    /// <param name="ManifestPath">manifest 完整路径。</param>
    /// <param name="Manifest">可编辑 manifest。</param>
    private sealed record RuntimeCacheFixture(
        string SourceRoot,
        string ProjectRoot,
        string ManifestPath,
        JsonObject Manifest);
}
