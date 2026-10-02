#if GODOT && TOOLS
using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示 Godot Runtime engine registry。
    /// </summary>
    internal sealed class GodotEngineRegistry
    {
        public int ProtocolVersion { get; set; } = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string EngineId { get; set; } = GodotFileBridgeHost.ENGINE_ID;
        public string Engine { get; set; } = "Godot";
        public string EngineKind { get; set; } = "Godot";
        public string DisplayName { get; set; } = "Godot Runtime";
        public string Version { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public string AdapterVersion { get; set; } = "godot-runtime-filebridge-v1";
        public string SessionId { get; set; } = string.Empty;
        public long Generation { get; set; }
        public string Mode { get; set; } = "Runtime";
        public string StartedAtUtc { get; set; } = string.Empty;
        public string RegisteredAtUtc { get; set; } = string.Empty;
        public string[] Capabilities { get; set; } = Array.Empty<string>();
        public GodotFastChannelEndpoint[] FastChannels { get; set; } = Array.Empty<GodotFastChannelEndpoint>();
    }

    /// <summary>
    /// 表示指定 Kit 的 state snapshot 外层信封。
    /// </summary>
    internal sealed class GodotStateSnapshot
    {
        public int ProtocolVersion { get; set; } = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string EngineId { get; set; } = GodotFileBridgeHost.ENGINE_ID;
        public string Kit { get; set; } = string.Empty;
        public string Name { get; set; } = "state";
        public long Generation { get; set; }
        public long Sequence { get; set; }
        public string WrittenAtUtc { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = "{}";
    }

    /// <summary>
    /// 表示四个首批 Kit 共用的最小在线状态 payload。
    /// </summary>
    internal sealed class GodotStatePayload
    {
        public string Status { get; set; } = "online";
        public string Bridge { get; set; } = "filebridge";
        public string Runtime { get; set; } = GodotFileBridgeHost.ENGINE_ID;
        public string Kit { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public long Generation { get; set; }
        public long Sequence { get; set; }
        public string Mode { get; set; } = "Runtime";
        public string FastChannel { get; set; } = "filebridge-fallback";
    }
}
#endif
