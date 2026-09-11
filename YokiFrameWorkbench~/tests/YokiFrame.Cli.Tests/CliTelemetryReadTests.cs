using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.Json.Nodes;
using YokiFrame.Protocol.Telemetry.SharedMemory;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// 覆盖 CLI telemetry read 命令的真实进程输出，验证默认分层只暴露状态摘要。
/// </summary>
public sealed class CliTelemetryReadTests
{
    private const long GENERATION = 77L;

    /// <summary>
    /// 验证 telemetry segment 缺失时 CLI 返回 Unavailable 状态和可执行的下一步，而不是伪装成成功。
    /// </summary>
    [Fact]
    public async Task MissingTelemetrySegmentFailsWithUnavailableState()
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

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput.Trim());
        var json = JsonNode.Parse(result.StandardError)!;
        Assert.False(json["ok"]!.GetValue<bool>());
        Assert.Equal("Unavailable", json["state"]!.GetValue<string>());
        Assert.Equal("KitUnavailable", json["error"]!["code"]!.GetValue<string>());
        Assert.Equal("TelemetrySegmentUnavailable", json["issues"]![0]!["code"]!.GetValue<string>());
        Assert.True(json["issues"]![0]!["retryable"]!.GetValue<bool>());
        Assert.Contains("kit status", json["nextActions"]!.AsArray().Select(node => node!.GetValue<string>()));
    }

    /// <summary>
    /// 验证 CLI 可以从 Windows named memory map 读取已提交帧，并把 payload 展开为默认 summary。
    /// </summary>
    [Fact]
    public async Task ExistingTelemetrySegmentProjectsSummary()
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
        Assert.True(json["ok"]!.GetValue<bool>(), result.StandardOutput + Environment.NewLine + result.StandardError);
        Assert.Equal("Ready", json["state"]!.GetValue<string>());
        Assert.Equal("telemetry", json["source"]!.GetValue<string>());
        Assert.Equal("online", json["summary"]!["status"]!.GetValue<string>());
        Assert.Equal(5L, json["sequence"]!.GetValue<long>());
        Assert.Empty(json["issues"]!.AsArray());
    }

    /// <summary>
    /// 验证 --detail full 仍然返回协议原始字段，供定位问题和 Workbench 使用。
    /// </summary>
    [Fact]
    public async Task FullDetailRestoresRawTelemetryFields()
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
            "--detail",
            "full",
            "--project",
            projectRoot);

        Assert.Equal(0, result.ExitCode);
        var json = JsonNode.Parse(result.StandardOutput)!;
        Assert.Equal("{\"status\":\"online\"}", json["result"]!["payloadJson"]!.GetValue<string>());
        Assert.Equal("Committed", json["result"]!["header"]!["writeState"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(json["segment"]!.GetValue<string>()));
    }

    /// <summary>启动真实 CLI 程序并捕获输出。</summary>
    /// <param name="arguments">CLI 参数。</param>
    /// <returns>进程执行结果。</returns>
    private static Task<CliProcessResult> RunCliAsync(params string[] arguments)
        => CliTestHelpers.RunCliAsync(arguments);

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
}
