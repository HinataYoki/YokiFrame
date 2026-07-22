using YokiFrame.Tooling.Application.Models.SaveKit;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>验证 SaveKit Runtime state 的强类型解析、有界元数据和坏 payload 回退。</summary>
public sealed class WorkbenchSaveKitStateTests
{
    /// <summary>验证后端、自动保存和 Slot/Global 容器头字段能完整进入 read model。</summary>
    [Fact]
    public void ParseState_ReadsSafeRuntimeMetadata()
    {
        WorkbenchSaveKitState state = WorkbenchSaveKitStateParser.Parse(CreateSource(
            "{\"schemaVersion\":1,\"version\":12,\"backend\":{\"storageConfigured\":true,\"serializerConfigured\":true,\"ready\":true,\"storageType\":\"FileSaveStorage\",\"serializerId\":\"json\",\"encryptorId\":\"aes-cbc-hmac\"},\"autoSave\":{\"enabled\":true,\"target\":{\"kind\":\"Slot\",\"name\":\"3\",\"slotId\":3},\"intervalSeconds\":5,\"elapsedSeconds\":2.5},\"slots\":[{\"target\":{\"kind\":\"Slot\",\"name\":\"3\",\"slotId\":3},\"displayName\":\"Checkpoint\",\"containerVersion\":1,\"createdTimestamp\":10,\"lastSavedTimestamp\":20,\"serializerId\":\"json\"}],\"slotCount\":1,\"slotTotal\":1,\"slotsTruncated\":false,\"globals\":[{\"target\":{\"kind\":\"Global\",\"name\":\"settings\",\"slotId\":-1},\"displayName\":\"\",\"containerVersion\":1,\"createdTimestamp\":11,\"lastSavedTimestamp\":21,\"serializerId\":\"json\"}],\"globalCount\":1,\"globalTotal\":1,\"globalsTruncated\":false,\"metadataAvailable\":true,\"metadataReadFailed\":false}"));

        Assert.Equal(12L, state.Version);
        Assert.True(state.Backend.Ready);
        Assert.Equal("FileSaveStorage", state.Backend.StorageType);
        Assert.Equal("json", state.Backend.SerializerId);
        Assert.True(state.AutoSave.Enabled);
        Assert.NotNull(state.AutoSave.Target);
        Assert.Equal(3, state.AutoSave.Target!.SlotId);
        Assert.Single(state.Slots);
        Assert.Equal("Checkpoint", state.Slots[0].DisplayName);
        Assert.Single(state.Globals);
        Assert.Equal("settings", state.Globals[0].Target.Name);
        Assert.True(state.MetadataAvailable);
        Assert.False(state.MetadataReadFailed);
    }

    /// <summary>验证错误 schema 回落为空状态，并把原因保留给页面的 stale 提示。</summary>
    [Fact]
    public void ParseState_RejectsUnknownSchema()
    {
        WorkbenchSaveKitState state = WorkbenchSaveKitStateParser.Parse(CreateSource("{\"schemaVersion\":2}"));

        Assert.False(state.Backend.Ready);
        Assert.Empty(state.Slots);
        Assert.Empty(state.Globals);
        Assert.Contains("schemaVersion", state.StaleReason, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>创建携带固定宿主身份的 state 解析输入。</summary>
    private static WorkbenchSaveKitDataSource CreateSource(string payload)
    {
        return new WorkbenchSaveKitDataSource(
            "unity-runtime",
            "savekit-session",
            9L,
            "PlayMode",
            DateTimeOffset.UtcNow,
            "snapshot",
            string.Empty,
            Array.Empty<string>(),
            string.Empty,
            payload);
    }
}
