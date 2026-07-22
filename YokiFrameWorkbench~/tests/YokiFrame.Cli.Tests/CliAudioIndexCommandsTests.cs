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
    private static async Task<CliProcessResult> RunCliAsync(params string[] arguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(GetCliAssemblyPath());
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);
        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start YokiFrame.Cli process.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new CliProcessResult(process.ExitCode, await standardOutput, await standardError);
    }

    /// <summary>定位同一 solution 构建出的 CLI 程序。</summary>
    /// <returns>CLI 程序 DLL 路径。</returns>
    private static string GetCliAssemblyPath()
    {
        DirectoryInfo outputDirectory = new(AppContext.BaseDirectory);
        DirectoryInfo configurationDirectory = outputDirectory.Parent
            ?? throw new DirectoryNotFoundException("CLI test configuration directory is missing.");
        DirectoryInfo projectDirectory = configurationDirectory.Parent
            ?? throw new DirectoryNotFoundException("CLI test project directory is missing.");
        DirectoryInfo binDirectory = projectDirectory.Parent
            ?? throw new DirectoryNotFoundException("CLI test bin directory is missing.");
        string assemblyPath = Path.Combine(
            binDirectory.FullName, "YokiFrame.Cli", configurationDirectory.Name,
            outputDirectory.Name, "YokiFrame.Cli.dll");
        Assert.True(File.Exists(assemblyPath), "CLI assembly was not built: " + assemblyPath);
        return assemblyPath;
    }

    /// <summary>保存 CLI 子进程执行结果。</summary>
    /// <param name="ExitCode">进程退出码。</param>
    /// <param name="StandardOutput">标准输出。</param>
    /// <param name="StandardError">标准错误。</param>
    private sealed record CliProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
