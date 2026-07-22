using System.Text.Json.Nodes;
using YokiFrame.Client.FileBridge.Diagnostics;

namespace YokiFrame.Client.Tests.FileBridge.Diagnostics;

/// <summary>
/// 覆盖 heartbeat stale 判断。
/// </summary>
public sealed class HeartbeatInfoTests
{
    /// <summary>
    /// 验证超过阈值的 heartbeat 会被标记 stale。
    /// </summary>
    [Fact]
    public void OldHeartbeatIsStale()
    {
        var nowUtc = DateTimeOffset.Parse("2026-07-08T00:00:30Z");
        var heartbeat = new HeartbeatInfo("heartbeat.json", "unity-editor", DateTimeOffset.Parse("2026-07-08T00:00:00Z"));

        Assert.True(heartbeat.IsStale(nowUtc, TimeSpan.FromSeconds(15)));
    }

    /// <summary>
    /// 验证未超过阈值的 heartbeat 会保持 fresh。
    /// </summary>
    [Fact]
    public void RecentHeartbeatIsFresh()
    {
        var nowUtc = DateTimeOffset.Parse("2026-07-08T00:00:10Z");
        var heartbeat = new HeartbeatInfo("heartbeat.json", "unity-editor", DateTimeOffset.Parse("2026-07-08T00:00:00Z"));

        Assert.False(heartbeat.IsStale(nowUtc, TimeSpan.FromSeconds(15)));
    }

    /// <summary>
    /// 验证旧协议 timestamp 字段仍可转换为 heartbeat 时间。
    /// </summary>
    [Fact]
    public void TimestampHeartbeatCanBeParsed()
    {
        var node = JsonNode.Parse("{\"engineId\":\"unity-editor\",\"timestamp\":1783299815}")!;
        var heartbeat = HeartbeatInfo.FromJson("heartbeat.json", node);

        Assert.Equal("unity-editor", heartbeat.EngineId);
        Assert.NotEqual(DateTimeOffset.MinValue, heartbeat.CreatedAtUtc);
    }

    /// <summary>
    /// 验证当前新项目写出的 writtenAtUtc heartbeat 字段可作为 stale 时间来源。
    /// </summary>
    [Fact]
    public void WrittenAtHeartbeatCanBeParsed()
    {
        var node = JsonNode.Parse("{\"engineId\":\"unity-editor\",\"writtenAtUtc\":\"2026-07-08T00:00:00Z\"}")!;
        var heartbeat = HeartbeatInfo.FromJson("heartbeat.json", node);

        Assert.Equal(DateTimeOffset.Parse("2026-07-08T00:00:00Z"), heartbeat.CreatedAtUtc);
    }

    /// <summary>
    /// 验证 Unity adapter 写出的 session、generation、mode 和 sequence 会进入工具侧模型。
    /// </summary>
    [Fact]
    public void UnityHeartbeatMetadataCanBeParsed()
    {
        var node = JsonNode.Parse("{\"engineId\":\"unity-editor\",\"createdAtUtc\":\"2026-07-08T00:00:00Z\",\"sessionId\":\"session-a\",\"generation\":42,\"mode\":\"PlayMode\",\"sequence\":7}")!;
        var heartbeat = HeartbeatInfo.FromJson("heartbeat.json", node);

        Assert.Equal("session-a", heartbeat.SessionId);
        Assert.Equal(42, heartbeat.Generation);
        Assert.Equal("PlayMode", heartbeat.Mode);
        Assert.Equal(7, heartbeat.Sequence);
    }
}
