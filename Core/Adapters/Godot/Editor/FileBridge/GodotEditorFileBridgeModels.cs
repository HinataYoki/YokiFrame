#if GODOT && TOOLS
using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示 `godot-editor` commands 目录中的命令信封。
    /// </summary>
    internal sealed class GodotEditorCommandEnvelope
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
    /// 表示 Editor Host 写入 results 目录的 terminal response。
    /// </summary>
    internal sealed class GodotEditorCommandResponse
    {
        public int ProtocolVersion { get; set; } = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string RequestId { get; set; } = string.Empty;
        public string EngineId { get; set; } = GodotEditorFileBridgeHost.ENGINE_ID;
        public string Status { get; set; } = string.Empty;
        public string ResultJson { get; set; } = "{}";
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string CompletedAtUtc { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表示正式 Godot Editor Host 的 engine registry。
    /// </summary>
    internal sealed class GodotEditorEngineRegistry
    {
        public int ProtocolVersion { get; set; } = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string EngineId { get; set; } = GodotEditorFileBridgeHost.ENGINE_ID;
        public string Engine { get; set; } = "Godot";
        public string EngineKind { get; set; } = "Godot";
        public string HostKind { get; set; } = "editor";
        public string DisplayName { get; set; } = "Godot Editor";
        public string Version { get; set; } = string.Empty;
        public string ProjectPath { get; set; } = string.Empty;
        public string AdapterVersion { get; set; } = "godot-editor-filebridge-v1";
        public string SessionId { get; set; } = string.Empty;
        public long Generation { get; set; }
        public string Mode { get; set; } = "Editor";
        public string StartedAtUtc { get; set; } = string.Empty;
        public string RegisteredAtUtc { get; set; } = string.Empty;
        public string[] Capabilities { get; set; } = Array.Empty<string>();
        public object[] FastChannels { get; set; } = Array.Empty<object>();
    }

    /// <summary>
    /// 表示正式 Godot Editor Host 的在线心跳。
    /// </summary>
    internal sealed class GodotEditorHeartbeat
    {
        public int ProtocolVersion { get; set; } = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string EngineId { get; set; } = GodotEditorFileBridgeHost.ENGINE_ID;
        public string SessionId { get; set; } = string.Empty;
        public long Generation { get; set; }
        public string Mode { get; set; } = "Editor";
        public long Sequence { get; set; }
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string WrittenAtUtc { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表示 Godot Editor 返回的 System/list_commands 目录。
    /// </summary>
    internal sealed class GodotEditorCommandCatalogResult
    {
        public string EngineId { get; set; } = GodotEditorFileBridgeHost.ENGINE_ID;
        public string Mode { get; set; } = "Editor";
        public string SessionId { get; set; } = string.Empty;
        public long Generation { get; set; }
        public long Sequence { get; set; }
        public GodotEditorCommandCatalogKit[] Kits { get; set; } = Array.Empty<GodotEditorCommandCatalogKit>();
    }

    /// <summary>
    /// 表示 Editor 命令目录中的一个 Kit。
    /// </summary>
    internal sealed class GodotEditorCommandCatalogKit
    {
        public string Kit { get; set; } = string.Empty;
        public GodotEditorCommandCatalogAction[] Actions { get; set; } = Array.Empty<GodotEditorCommandCatalogAction>();
    }

    /// <summary>
    /// 表示 Editor 命令目录中的一个 action。
    /// </summary>
    internal sealed class GodotEditorCommandCatalogAction
    {
        public string Action { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表示 System/ping 的 Editor 身份结果。
    /// </summary>
    internal sealed class GodotEditorPingResult
    {
        public string Message { get; set; } = "pong";
        public string EngineId { get; set; } = GodotEditorFileBridgeHost.ENGINE_ID;
        public string Mode { get; set; } = "Editor";
        public string SessionId { get; set; } = string.Empty;
        public long Generation { get; set; }
        public long Sequence { get; set; }
    }

    /// <summary>
    /// 表示 System/bridge_status 的 Editor FileBridge 诊断。
    /// </summary>
    internal sealed class GodotEditorBridgeStatusResult
    {
        public string EngineId { get; set; } = GodotEditorFileBridgeHost.ENGINE_ID;
        public string Mode { get; set; } = "Editor";
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
        public string LastPollLimitReason { get; set; } = string.Empty;
        public string LastError { get; set; } = string.Empty;
        public string FastChannel { get; set; } = "filebridge-only";
    }

    /// <summary>
    /// 表示 Editor engine 协议目录的只读存储统计。
    /// </summary>
    internal sealed class GodotEditorProtocolStorageInfo
    {
        public int FileCount { get; set; }
        public long TotalBytes { get; set; }
        public string OldestFileUtc { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表示无法消费 Editor 命令时写入的 deadletter 诊断。
    /// </summary>
    internal sealed class GodotEditorDeadletterInfo
    {
        public int ProtocolVersion { get; set; } = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string EngineId { get; set; } = GodotEditorFileBridgeHost.ENGINE_ID;
        public string SourcePath { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string WrittenAtUtc { get; set; } = string.Empty;
    }
}
#endif
