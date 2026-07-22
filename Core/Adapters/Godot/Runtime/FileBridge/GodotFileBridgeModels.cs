#if GODOT && TOOLS
using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示 godot-runtime commands 目录中的命令信封。
    /// </summary>
    internal sealed class GodotCommandEnvelope
    {
        public int ProtocolVersion { get; set; }
        public string EngineId { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string Kit { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = "{}";
        public int TimeoutMs { get; set; }
    }

    /// <summary>
    /// 表示写入 results 目录的 terminal response。
    /// </summary>
    internal sealed class GodotCommandResponse
    {
        public int ProtocolVersion { get; set; } = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string RequestId { get; set; } = string.Empty;
        public string EngineId { get; set; } = GodotFileBridgeHost.ENGINE_ID;
        public string Status { get; set; } = string.Empty;
        public string ResultJson { get; set; } = "{}";
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string CompletedAtUtc { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表示 Godot Runtime 返回的 System/list_commands 目录。
    /// </summary>
    internal sealed class GodotCommandCatalogResult
    {
        public string EngineId { get; set; } = GodotFileBridgeHost.ENGINE_ID;
        public string Mode { get; set; } = "Runtime";
        public string SessionId { get; set; } = string.Empty;
        public long Generation { get; set; }
        public long Sequence { get; set; }
        public GodotCommandCatalogKit[] Kits { get; set; } = Array.Empty<GodotCommandCatalogKit>();
    }

    /// <summary>
    /// 表示命令目录中的一个 Kit。
    /// </summary>
    internal sealed class GodotCommandCatalogKit
    {
        public string Kit { get; set; } = string.Empty;
        public GodotCommandCatalogAction[] Actions { get; set; } = Array.Empty<GodotCommandCatalogAction>();
    }

    /// <summary>
    /// 表示命令目录中的一个 action。
    /// </summary>
    internal sealed class GodotCommandCatalogAction
    {
        public string Action { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
    }

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
    /// 表示 Godot Runtime heartbeat。
    /// </summary>
    internal sealed class GodotHeartbeat
    {
        public int ProtocolVersion { get; set; } = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string EngineId { get; set; } = GodotFileBridgeHost.ENGINE_ID;
        public string SessionId { get; set; } = string.Empty;
        public long Generation { get; set; }
        public string Mode { get; set; } = "Runtime";
        public long Sequence { get; set; }
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string WrittenAtUtc { get; set; } = string.Empty;
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

    /// <summary>
    /// 表示 System/ping 成功结果。
    /// </summary>
    internal sealed class GodotPingResult
    {
        public string Message { get; set; } = "pong";
        public string EngineId { get; set; } = GodotFileBridgeHost.ENGINE_ID;
        public string Mode { get; set; } = "Runtime";
        public string SessionId { get; set; } = string.Empty;
        public long Generation { get; set; }
        public long Sequence { get; set; }
    }

    /// <summary>
    /// 表示 System/bridge_status 的协议存储和来源状态结果。
    /// </summary>
    internal sealed class GodotBridgeStatusResult
    {
        public string EngineId { get; set; } = GodotFileBridgeHost.ENGINE_ID;
        public string Mode { get; set; } = "Runtime";
        public string SessionId { get; set; } = string.Empty;
        public long Generation { get; set; }
        public long Sequence { get; set; }
        public int Pending { get; set; }
        public int Archive { get; set; }
        public int Deadletter { get; set; }
        public int Results { get; set; }
        public int ProtocolFileCount { get; set; }
        public long ProtocolBytes { get; set; }
        public string OldestProtocolFileUtc { get; set; } = string.Empty;
        public bool BackpressureActive { get; set; }
        public string LastError { get; set; } = string.Empty;
        public string FastChannel { get; set; } = "filebridge-fallback";
    }

    /// <summary>
    /// 表示 engine 协议目录 JSON 文件的只读存储诊断。
    /// </summary>
    internal sealed class GodotProtocolStorageInfo
    {
        public int FileCount { get; set; }
        public long TotalBytes { get; set; }
        public string OldestFileUtc { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表示无法消费命令时写入的 deadletter 诊断。
    /// </summary>
    internal sealed class GodotDeadletterInfo
    {
        public int ProtocolVersion { get; set; } = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string EngineId { get; set; } = GodotFileBridgeHost.ENGINE_ID;
        public string SourcePath { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string WrittenAtUtc { get; set; } = string.Empty;
    }
}
#endif
