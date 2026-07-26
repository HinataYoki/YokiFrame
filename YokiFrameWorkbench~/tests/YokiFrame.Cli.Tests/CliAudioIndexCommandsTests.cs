using System.Diagnostics;
using System.Text.Json.Nodes;

namespace YokiFrame.Cli.Tests;

/// <summary>覆盖 AudioKit 稳定索引 CLI 的真实进程输出与 AOT JSON 元数据。</summary>
public sealed class CliAudioIndexCommandsTests
{
    /// <summary>验证只读扫描输出可解析的条目数组，而不是在 JSON 序列化阶段失败。</summary>
    [Fact]
    public async Task AudioIndexScanWritesEntriesAsSuccessJson()
    {
        string projectRoot = CreateProjectRoot();
        try
        {
            CliProcessResult result = await RunCliAsync(
                "audio", "index", "scan", "--project", projectRoot);
            JsonNode json = JsonNode.Parse(result.StandardOutput)
                ?? throw new InvalidOperationException("Audio index stdout is not JSON.");

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.StandardError.Trim());
            Assert.True(json["ok"]!.GetValue<bool>());
            Assert.Equal(1, json["entryCount"]!.GetValue<int>());
            Assert.Equal("SFX_CLICK", json["entries"]![0]!["constantName"]!.GetValue<string>());
        }
        finally
        {
            Directory.Delete(projectRoot, true);
        }
    }

    /// <summary>创建包含一个受支持音频文件的临时项目根。</summary>
    /// <returns>测试项目根绝对路径。</returns>
    private static string CreateProjectRoot()
    {
        string projectRoot = Path.Combine(
            Path.GetTempPath(), "yokiframe-cli-audio", Guid.NewGuid().ToString("N"));
        string audioFolder = Path.Combine(projectRoot, "Assets", "Art", "Audio", "Sfx");
        Directory.CreateDirectory(audioFolder);
        File.WriteAllBytes(Path.Combine(audioFolder, "Click.wav"), new byte[] { 1, 2, 3 });
        return projectRoot;
    }

    /// <summary>启动真实 CLI 程序并捕获标准输出和标准错误。</summary>
    /// <param name="arguments">传递给 CLI 的参数列表。</param>
    /// <returns>CLI 进程退出结果。</returns>
    private static Task<CliProcessResult> RunCliAsync(params string[] arguments)
        => CliTestHelpers.RunCliAsync(arguments);
}
