#if UNITY_EDITOR

using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示 `.yokiframe/engines/unity-editor/commands` 中的命令信封。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorCommandEnvelope
    {
        public int protocolVersion;
        public string engineId = string.Empty;
        public string source = string.Empty;
        public string createdAtUtc = string.Empty;
        public string requestId = string.Empty;
        public string kit = string.Empty;
        public string action = string.Empty;
        public string payloadJson = "{}";
        public int timeoutMs;
    }

    /// <summary>
    /// 表示写入 results 目录的 terminal response。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorCommandResponse
    {
        public int protocolVersion = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string requestId = string.Empty;
        public string engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID;
        public string status = string.Empty;
        public string resultJson = "{}";
        public string errorCode = string.Empty;
        public string errorMessage = string.Empty;
        public string completedAtUtc = string.Empty;
    }

    /// <summary>
    /// 表示 Unity Editor engine registry 文件。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorEngineRegistry
    {
        public int protocolVersion = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID;
        public string engine = "Unity";
        public string engineKind = "Unity";
        public string displayName = "Unity Editor";
        public string version = string.Empty;
        public string projectPath = string.Empty;
        public string adapterVersion = "phase1-editor-filebridge";
        public string sessionId = string.Empty;
        public long generation;
        public string mode = string.Empty;
        public string startedAtUtc = string.Empty;
        public string registeredAtUtc = string.Empty;
        public string[] capabilities = Array.Empty<string>();
        public YokiFrameEditorFastChannelEndpoint[] fastChannels = Array.Empty<YokiFrameEditorFastChannelEndpoint>();
    }

    /// <summary>
    /// 表示 Unity Editor 在 engine registry 中发布的可选 FastChannel endpoint。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorFastChannelEndpoint
    {
        public int protocolVersion = YokiFrameFastChannelContract.PROTOCOL_VERSION;
        public string engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID;
        public string sessionId = string.Empty;
        public long generation;
        public string transport = "none";
        public string endpoint = string.Empty;
        public bool enabled;
        public string fallback = "filebridge";
        public string[] readOnlyCommands = Array.Empty<string>();
    }

    /// <summary>
    /// 表示 FastChannel Hello 与 HelloAck 使用的 Unity JSON 身份 payload。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorFastChannelIdentity
    {
        public string engineId = string.Empty;
        public string sessionId = string.Empty;
        public long generation;
    }

    /// <summary>
    /// 表示 FastChannel Error frame 使用的最小错误 payload。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorFastChannelError
    {
        public string code = string.Empty;
        public string message = string.Empty;
    }

    /// <summary>
    /// 表示 Unity Editor heartbeat 文件。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorHeartbeat
    {
        public int protocolVersion = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID;
        public string sessionId = string.Empty;
        public long generation;
        public string mode = string.Empty;
        public long sequence;
        public string createdAtUtc = string.Empty;
        public string writtenAtUtc = string.Empty;
    }

    /// <summary>
    /// 表示最小 snapshot 文件。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorSnapshot
    {
        public int protocolVersion = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID;
        public string kit = string.Empty;
        public string name = "state";
        public long generation;
        public long sequence;
        public string writtenAtUtc = string.Empty;
        public string payloadJson = "{}";
    }

    /// <summary>
    /// 表示 System/ping 的业务结果。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorPingResult
    {
        public string message = "pong";
        public string engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID;
        public string mode = string.Empty;
        public string sessionId = string.Empty;
        public long generation;
        public long sequence;
    }

    /// <summary>
    /// 表示 System/bridge_status 的业务结果。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorBridgeStatusResult
    {
        public string engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID;
        public string mode = string.Empty;
        public string sessionId = string.Empty;
        public long generation;
        public long sequence;
        public int pending;
        public int archive;
        public int deadletter;
        public int results;
        public int protocolFileCount;
        public long protocolBytes;
        public string oldestProtocolFileUtc = string.Empty;
        // reserved：Unity 宿主尚未实现命令轮询背压，以下三个字段恒为默认值，消费方不应据此判断故障。
        public bool backpressureActive;
        public string lastPollLimitReason = string.Empty;
        public int bridgeBusyCount;
        public string lastError = string.Empty;
        public YokiFrameEditorBridgeRetentionInfo retention = new YokiFrameEditorBridgeRetentionInfo();
    }

    /// <summary>
    /// 表示 Editor 侧扫描得到的 FileBridge 协议目录存储诊断。
    /// </summary>
    internal sealed class YokiFrameEditorProtocolStorageInfo
    {
        public int fileCount;
        public long totalBytes;
        public string oldestFileUtc = string.Empty;
    }

    /// <summary>
    /// 表示 FileBridge 证据目录保留策略，只提示策略，不执行自动清理。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorBridgeRetentionInfo
    {
        public string archive = "manual";
        public string deadletter = "manual";
        public string results = "manual";
        public string cleanup = "explicit-maintenance";
    }

    /// <summary>
    /// 表示 System/list_commands 的业务结果。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorCommandCatalogResult
    {
        public string engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID;
        public string mode = string.Empty;
        public string sessionId = string.Empty;
        public long generation;
        public long sequence;
        public YokiFrameEditorCommandCatalogKit[] kits = Array.Empty<YokiFrameEditorCommandCatalogKit>();
    }

    /// <summary>
    /// 表示命令目录中的 Kit 分组。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorCommandCatalogKit
    {
        public string kit = string.Empty;
        public YokiFrameEditorCommandCatalogAction[] actions = Array.Empty<YokiFrameEditorCommandCatalogAction>();
    }

    /// <summary>
    /// 表示命令目录中的单个 action。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorCommandCatalogAction
    {
        public string action = string.Empty;
        public string kind = string.Empty;
    }

    /// <summary>
    /// 表示 System/refresh_snapshots 的业务结果。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorRefreshSnapshotsResult
    {
        public string engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID;
        public string mode = string.Empty;
        public string sessionId = string.Empty;
        public long generation;
        public long sequence;
        public string[] refreshedKits = Array.Empty<string>();
        public int snapshotCount;
        public string telemetry = string.Empty;
    }

    /// <summary>
    /// 表示 System/get_environment 的业务结果。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorEnvironmentResult
    {
        public string engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID;
        public string engineKind = "Unity";
        public string displayName = "Unity Editor";
        public string mode = string.Empty;
        public string sessionId = string.Empty;
        public long generation;
        public long sequence;
        public string unityVersion = string.Empty;
        public string platform = string.Empty;
        public string activeBuildTarget = string.Empty;
        public bool isBatchMode;
        public string projectPath = string.Empty;
        public string dataPath = string.Empty;
        public string persistentDataPath = string.Empty;
        public string temporaryCachePath = string.Empty;
        public string yokiFrameRoot = string.Empty;
        public string engineRoot = string.Empty;
        public string telemetry = string.Empty;
    }

    /// <summary>
    /// 表示 System/open_project_folder 与 System/open_log 的业务结果。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorOpenPathResult
    {
        public string engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID;
        public string action = string.Empty;
        public string path = string.Empty;
        public string mode = string.Empty;
        public string sessionId = string.Empty;
        public long generation;
        public long sequence;
        public bool opened;
    }

    /// <summary>表示 System/open_code_location 的输入 payload。</summary>
    [Serializable]
    internal sealed class YokiFrameEditorCodeLocationRequest
    {
        public string filePath = string.Empty;
        public int line = 1;
    }

    /// <summary>表示 Unity 外部代码编辑器已经接受的源码位置。</summary>
    [Serializable]
    internal sealed class YokiFrameEditorOpenCodeLocationResult
    {
        public string engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID;
        public string filePath = string.Empty;
        public int line;
        public string mode = string.Empty;
        public string sessionId = string.Empty;
        public long generation;
        public long sequence;
        public bool opened;
    }

    /// <summary>
    /// 表示最小状态 snapshot 的 payload。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorStatePayload
    {
        public string status = "online";
        public string bridge = "filebridge";
        public string workbench = "avalonia";
        public string runtime = "unity-editor";
        public string kit = string.Empty;
        public string sessionId = string.Empty;
        public long generation;
        public long sequence;
        public string mode = string.Empty;
        public string fastChannel = "shared-memory-v1";
    }

    /// <summary>
    /// 表示无法消费命令时写入 deadletter 的诊断信息。
    /// </summary>
    [Serializable]
    internal sealed class YokiFrameEditorDeadletterInfo
    {
        public int protocolVersion = YokiFrameFileBridgeContract.PROTOCOL_VERSION;
        public string engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID;
        public string sourcePath = string.Empty;
        public string errorCode = string.Empty;
        public string errorMessage = string.Empty;
        public string writtenAtUtc = string.Empty;
    }
}

#endif
