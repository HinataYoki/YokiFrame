using YokiFrame.Client.FileBridge.Diagnostics;

namespace YokiFrame.Client.Tests.FileBridge.Diagnostics;

/// <summary>
/// 覆盖 FileBridge status 的 compact JSON 输出契约。
/// </summary>
public sealed class FileBridgeStatusTests
{
    /// <summary>
    /// 验证 bridge status 输出旧版 Tauri 诊断页依赖的队列、存储、背压和错误摘要字段。
    /// </summary>
    [Fact]
    public void ToJsonIncludesDiagnosticSummaryFields()
    {
        var oldestUtc = DateTimeOffset.Parse("2026-06-20T11:50:00.0000000Z");
        FileBridgeStatus status = new(
            "unity-editor",
            "F:/Project/.yokiframe/engines/unity-editor",
            "F:/Project/.yokiframe/engines/unity-editor/commands",
            "F:/Project/.yokiframe/engines/unity-editor/results")
        {
            PendingCount = 2,
            ProcessingCount = 1,
            DeadletterCount = 3,
            ResultCount = 4,
            ProtocolFileCount = 12,
            ProtocolBytes = 4096,
            OldestProtocolFileUtc = oldestUtc,
            BackpressureActive = true,
            LastPollLimitReason = "MaxPendingCommands",
            BridgeBusyCount = 5,
            LastError = "Bridge busy"
        };

        var json = status.ToJson(DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(15));

        Assert.Equal(12, json["protocolFileCount"]?.GetValue<int>());
        Assert.Equal(4096, json["protocolBytes"]?.GetValue<long>());
        Assert.Equal("2026-06-20T11:50:00.0000000Z", json["oldestProtocolFileUtc"]?.GetValue<string>());
        Assert.True(json["backpressureActive"]?.GetValue<bool>());
        Assert.Equal("MaxPendingCommands", json["lastPollLimitReason"]?.GetValue<string>());
        Assert.Equal(5, json["bridgeBusyCount"]?.GetValue<int>());
        Assert.Equal("Bridge busy", json["lastError"]?.GetValue<string>());
    }

    /// <summary>
    /// 验证 FileBridge status 会从 engine 目录真实统计协议 JSON 文件数量、体积和最旧更新时间。
    /// </summary>
    [Fact]
    public void ReadBridgeStatusCollectsProtocolStorageDiagnostics()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-filebridge-status-tests", Guid.NewGuid().ToString("N"));
        var engineRoot = Path.Combine(projectRoot, ".yokiframe", "engines", "unity-editor");
        var oldestUtc = new DateTime(2026, 6, 20, 11, 50, 0, DateTimeKind.Utc);
        try
        {
            WriteJson(Path.Combine(engineRoot, "commands", "pending-a.json"), "{}");
            WriteJson(Path.Combine(engineRoot, "commands", "processing", "processing-a.json"), "{\"processing\":true}");
            WriteJson(Path.Combine(engineRoot, "commands", "archive", "archive-a.json"), "{\"archive\":true}");
            WriteJson(Path.Combine(engineRoot, "commands", "deadletter", "deadletter-a.json"), "{\"deadletter\":true}");
            WriteJson(Path.Combine(engineRoot, "results", "result-a.json"), "{\"result\":true}");
            WriteJson(Path.Combine(engineRoot, "snapshots", "System", "state.json"), "{\"status\":\"online\"}");
            WriteJson(Path.Combine(engineRoot, "status", "heartbeat.json"), "{\"protocolVersion\":2,\"engineId\":\"unity-editor\",\"createdAtUtc\":\"2026-07-01T00:00:00.0000000Z\"}");
            File.SetLastWriteTimeUtc(Path.Combine(engineRoot, "commands", "pending-a.json"), oldestUtc);

            var status = new YokiFrameClient(projectRoot).ReadBridgeStatus("unity-editor");
            var expectedBytes = Directory.EnumerateFiles(engineRoot, "*.json", SearchOption.AllDirectories)
                .Sum(static path => new FileInfo(path).Length);

            Assert.Equal(1, status.PendingCount);
            Assert.Equal(1, status.ProcessingCount);
            Assert.Equal(1, status.ArchiveCount);
            Assert.Equal(1, status.DeadletterCount);
            Assert.Equal(1, status.ResultCount);
            Assert.Equal(7, status.ProtocolFileCount);
            Assert.Equal(expectedBytes, status.ProtocolBytes);
            Assert.Equal(new DateTimeOffset(oldestUtc), status.OldestProtocolFileUtc);
        }
        finally
        {
            if (Directory.Exists(projectRoot))
            {
                Directory.Delete(projectRoot, true);
            }
        }
    }

    /// <summary>
    /// 验证 bridge status 输出包含当前自动清理保留策略，便于调用方了解证据生命周期。
    /// </summary>
    [Fact]
    public void ToJsonIncludesAutomaticRetentionPolicy()
    {
        FileBridgeStatus status = new(
            "unity-editor",
            "F:/Project/.yokiframe/engines/unity-editor",
            "F:/Project/.yokiframe/engines/unity-editor/commands",
            "F:/Project/.yokiframe/engines/unity-editor/results");

        var json = status.ToJson(DateTimeOffset.UnixEpoch, TimeSpan.FromSeconds(15));
        var retention = Assert.IsType<System.Text.Json.Nodes.JsonObject>(json["retention"]);

        Assert.Equal("7d-or-200", retention["archive"]?.GetValue<string>());
        Assert.Equal("30d-or-200", retention["deadletter"]?.GetValue<string>());
        Assert.Equal("7d-or-200", retention["results"]?.GetValue<string>());
        Assert.Equal("automatic-on-host-start-and-every-5m", retention["cleanup"]?.GetValue<string>());
    }

    /// <summary>
    /// 写入测试 JSON 文件，并自动创建父目录。
    /// </summary>
    /// <param name="path">目标文件路径。</param>
    /// <param name="json">JSON 内容。</param>
    private static void WriteJson(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }
}
