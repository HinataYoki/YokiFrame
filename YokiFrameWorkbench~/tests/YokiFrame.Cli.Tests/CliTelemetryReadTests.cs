using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json.Nodes;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// 覆盖 CLI telemetry read 命令的真实进程输出。
/// </summary>
public sealed class CliTelemetryReadTests
{
    private const long GENERATION = 77L;

    /// <summary>
    /// 验证缺失 telemetry segment 时 CLI 返回 ok=true 和 Unavailable 状态。
    /// </summary>
    [Fact]
    public async Task MissingTelemetrySegmentReturnsUnavailableStatus()
    {
        var engineId = "test-" + Guid.NewGuid().ToString("N");
        var result = await RunCliAsync(
            "telemetry",
            "read",
            "--engine",
            engineId,
            "--kit",
            "System",
            "--name",
            "state",
            "--generation",
            GENERATION.ToString());

        Assert.Equal(0, result.ExitCode);
        var json = JsonNode.Parse(result.StandardOutput)!;
        Assert.True(json["ok"]!.GetValue<bool>());
        Assert.Equal("Unavailable", json["result"]!["status"]!.GetValue<string>());
    }

    /// <summary>
    /// 验证 CLI 可以从 Windows named memory map 读取已提交 telemetry 帧。
    /// </summary>
    [Fact]
    public async Task ExistingTelemetrySegmentReturnsAcceptedPayload()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var engineId = "test-" + Guid.NewGuid().ToString("N");
        var projectRoot = CreateProjectRoot();
        var segmentName = SharedMemoryTelemetrySegmentName.Create(projectRoot, engineId, "System", "state");
        var frame = CreateFrame("{\"status\":\"online\"}", engineId, GENERATION, 5L);
        using var memoryMap = MemoryMappedFile.CreateNew(segmentName, frame.Length, MemoryMappedFileAccess.ReadWrite);
        using var accessor = memoryMap.CreateViewAccessor(0, frame.Length, MemoryMappedFileAccess.Write);
        accessor.WriteArray(0, frame, 0, frame.Length);

        var result = await RunCliAsync(
            "telemetry",
            "read",
            "--engine",
            engineId,
            "--kit",
            "System",
            "--name",
            "state",
            "--generation",
            GENERATION.ToString(),
            "--project",
            projectRoot);

        Assert.Equal(0, result.ExitCode);
        var json = JsonNode.Parse(result.StandardOutput)!;
        Assert.True(
            json["result"]!["accepted"]!.GetValue<bool>(),
            result.StandardOutput + Environment.NewLine + result.StandardError);
        Assert.Equal("{\"status\":\"online\"}", json["result"]!["payloadJson"]!.GetValue<string>());
    }

    /// <summary>
    /// 启动真实 CLI 程序并捕获输出。
    /// </summary>
    /// <param name="arguments">CLI 参数。</param>
    /// <returns>进程执行结果。</returns>
    private static async Task<CliProcessResult> RunCliAsync(params string[] arguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(GetCliAssemblyPath());
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start YokiFrame.Cli process.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new CliProcessResult(process.ExitCode, await standardOutputTask, await standardErrorTask);
    }

    /// <summary>
    /// 根据测试输出目录定位同一 solution 构建出的 CLI 程序。
    /// </summary>
    /// <returns>CLI 程序 DLL 路径。</returns>
    private static string GetCliAssemblyPath()
    {
        var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var configurationDirectory = outputDirectory.Parent
            ?? throw new DirectoryNotFoundException("CLI test output configuration directory is missing.");
        var projectDirectory = configurationDirectory.Parent
            ?? throw new DirectoryNotFoundException("CLI test output project directory is missing.");
        var binDirectory = projectDirectory.Parent
            ?? throw new DirectoryNotFoundException("CLI test output bin directory is missing.");
        var cliAssemblyPath = Path.Combine(
            binDirectory.FullName,
            "YokiFrame.Cli",
            configurationDirectory.Name,
            outputDirectory.Name,
            "YokiFrame.Cli.dll");

        Assert.True(File.Exists(cliAssemblyPath), "CLI assembly was not built: " + cliAssemblyPath);
        return cliAssemblyPath;
    }

    /// <summary>
    /// 创建测试专用项目根，使 CLI 与 Named Map 使用同一项目作用域。
    /// </summary>
    /// <returns>无需真实协议文件的唯一项目根。</returns>
    private static string CreateProjectRoot()
    {
        return Path.Combine(Path.GetTempPath(), "yokiframe-cli-telemetry-tests", Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// 创建测试用 telemetry 帧。
    /// </summary>
    /// <param name="payloadJson">payload JSON。</param>
    /// <param name="engineId">帧所属的安全 engine 标识。</param>
    /// <param name="generation">engine generation。</param>
    /// <param name="sequence">帧序号。</param>
    /// <returns>帧字节。</returns>
    private static byte[] CreateFrame(
        string payloadJson,
        string engineId,
        long generation,
        long sequence)
    {
        var payload = Encoding.UTF8.GetBytes(payloadJson);
        var frame = new byte[SharedMemoryTelemetryFrameHeader.HEADER_SIZE + payload.Length];
        var header = new SharedMemoryTelemetryFrameHeader(
            SharedMemoryTelemetryFrameHeader.MAGIC,
            SharedMemoryTelemetryFrameHeader.PROTOCOL_VERSION,
            YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(engineId),
            generation,
            sequence,
            DateTimeOffset.UtcNow.UtcTicks,
            payload.Length,
            SharedMemoryTelemetryCrc32.Compute(payload),
            SharedMemoryTelemetryWriteState.Committed);
        header.WriteTo(frame.AsSpan(0, SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        payload.CopyTo(frame.AsSpan(SharedMemoryTelemetryFrameHeader.HEADER_SIZE));
        return frame;
    }

    /// <summary>
    /// 表示 CLI 子进程执行结果。
    /// </summary>
    /// <param name="ExitCode">进程退出码。</param>
    /// <param name="StandardOutput">标准输出。</param>
    /// <param name="StandardError">标准错误。</param>
    private sealed record CliProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
