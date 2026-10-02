#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示各宿主 commands 目录共用的命令信封。
    /// 字段只描述协议形状；engineId 由写入方填入，不在类型默认值里绑定某一个宿主。
    /// </summary>
    internal sealed class YokiFrameFileBridgeCommandEnvelope
    {
        /// <summary>获取或设置协议版本。</summary>
        public int ProtocolVersion { get; set; }

        /// <summary>获取或设置目标 engine ID。</summary>
        public string EngineId { get; set; } = string.Empty;

        /// <summary>获取或设置命令来源。</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>获取或设置命令创建时间。</summary>
        public string CreatedAtUtc { get; set; } = string.Empty;

        /// <summary>获取或设置请求标识。</summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>获取或设置目标 Kit。</summary>
        public string Kit { get; set; } = string.Empty;

        /// <summary>获取或设置目标 action。</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>获取或设置业务 payload JSON。</summary>
        public string PayloadJson { get; set; } = "{}";

        /// <summary>获取或设置命令超时毫秒数。</summary>
        public int TimeoutMs { get; set; }
    }

    /// <summary>
    /// 表示各宿主写入 results 目录的 terminal response。
    /// </summary>
    internal sealed class YokiFrameFileBridgeCommandResponse
    {
        /// <summary>获取或设置协议版本。</summary>
        public int ProtocolVersion { get; set; } = YokiFrameFileBridgeContract.PROTOCOL_VERSION;

        /// <summary>获取或设置请求标识。</summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>获取或设置响应所属 engine ID。</summary>
        public string EngineId { get; set; } = string.Empty;

        /// <summary>获取或设置终态状态。</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>获取或设置业务结果 JSON。</summary>
        public string ResultJson { get; set; } = "{}";

        /// <summary>获取或设置错误码。</summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>获取或设置错误说明。</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>获取或设置完成时间。</summary>
        public string CompletedAtUtc { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表示各宿主共用的 heartbeat。
    /// Mode 必须由当前宿主写入，避免类型默认值把不同宿主心跳标成同一种模式。
    /// </summary>
    internal sealed class YokiFrameFileBridgeHeartbeat
    {
        /// <summary>获取或设置协议版本。</summary>
        public int ProtocolVersion { get; set; } = YokiFrameFileBridgeContract.PROTOCOL_VERSION;

        /// <summary>获取或设置 engine ID。</summary>
        public string EngineId { get; set; } = string.Empty;

        /// <summary>获取或设置会话标识。</summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>获取或设置会话代际。</summary>
        public long Generation { get; set; }

        /// <summary>获取或设置宿主模式。</summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>获取或设置心跳序号。</summary>
        public long Sequence { get; set; }

        /// <summary>获取或设置创建时间。</summary>
        public string CreatedAtUtc { get; set; } = string.Empty;

        /// <summary>获取或设置写入时间。</summary>
        public string WrittenAtUtc { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表示 System/list_commands 的共享目录结果。
    /// </summary>
    internal sealed class YokiFrameFileBridgeCommandCatalogResult
    {
        /// <summary>获取或设置 engine ID。</summary>
        public string EngineId { get; set; } = string.Empty;

        /// <summary>获取或设置宿主模式。</summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>获取或设置会话标识。</summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>获取或设置会话代际。</summary>
        public long Generation { get; set; }

        /// <summary>获取或设置目录序号。</summary>
        public long Sequence { get; set; }

        /// <summary>获取或设置按 Kit 分组的命令。</summary>
        public YokiFrameFileBridgeCommandCatalogKit[] Kits { get; set; } = Array.Empty<YokiFrameFileBridgeCommandCatalogKit>();
    }

    /// <summary>
    /// 表示命令目录中的一个 Kit。
    /// </summary>
    internal sealed class YokiFrameFileBridgeCommandCatalogKit
    {
        /// <summary>获取或设置 Kit 标识。</summary>
        public string Kit { get; set; } = string.Empty;

        /// <summary>获取或设置该 Kit 的 action。</summary>
        public YokiFrameFileBridgeCommandCatalogAction[] Actions { get; set; } = Array.Empty<YokiFrameFileBridgeCommandCatalogAction>();
    }

    /// <summary>
    /// 表示命令目录中的一个 action。
    /// </summary>
    internal sealed class YokiFrameFileBridgeCommandCatalogAction
    {
        /// <summary>获取或设置 action 标识。</summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>获取或设置 action 种类。</summary>
        public string Kind { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表示各宿主共用的 System/ping 结果。
    /// </summary>
    internal sealed class YokiFrameFileBridgePingResult
    {
        /// <summary>获取或设置固定响应文本。</summary>
        public string Message { get; set; } = "pong";

        /// <summary>获取或设置 engine ID。</summary>
        public string EngineId { get; set; } = string.Empty;

        /// <summary>获取或设置宿主模式。</summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>获取或设置会话标识。</summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>获取或设置会话代际。</summary>
        public long Generation { get; set; }

        /// <summary>获取或设置响应序号。</summary>
        public long Sequence { get; set; }
    }

    /// <summary>
    /// 表示各宿主共用的 System/bridge_status 结果。
    /// FastChannel 由宿主填入真实通道状态，不在共享类型里预设 fallback。
    /// </summary>
    internal sealed class YokiFrameFileBridgeStatusResult
    {
        /// <summary>获取或设置 engine ID。</summary>
        public string EngineId { get; set; } = string.Empty;

        /// <summary>获取或设置宿主模式。</summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>获取或设置会话标识。</summary>
        public string SessionId { get; set; } = string.Empty;

        /// <summary>获取或设置会话代际。</summary>
        public long Generation { get; set; }

        /// <summary>获取或设置状态序号。</summary>
        public long Sequence { get; set; }

        /// <summary>获取或设置待处理命令数量。</summary>
        public int Pending { get; set; }

        /// <summary>获取或设置归档命令数量。</summary>
        public int Archive { get; set; }

        /// <summary>获取或设置 deadletter 数量。</summary>
        public int Deadletter { get; set; }

        /// <summary>获取或设置 terminal response 数量。</summary>
        public int Results { get; set; }

        /// <summary>获取或设置协议 JSON 文件数量。</summary>
        public int ProtocolFileCount { get; set; }

        /// <summary>获取或设置协议 JSON 总字节数。</summary>
        public long ProtocolBytes { get; set; }

        /// <summary>获取或设置最旧协议文件的 UTC 更新时间。</summary>
        public string OldestProtocolFileUtc { get; set; } = string.Empty;

        /// <summary>获取或设置当前轮是否触发背压。</summary>
        public bool BackpressureActive { get; set; }

        /// <summary>获取或设置最近一次轮询限制原因。</summary>
        public string LastPollLimitReason { get; set; } = string.Empty;

        /// <summary>获取或设置最近一次宿主错误。</summary>
        public string LastError { get; set; } = string.Empty;

        /// <summary>获取或设置当前 FastChannel 状态。</summary>
        public string FastChannel { get; set; } = string.Empty;
    }

    /// <summary>
    /// 表示无法消费命令时写入的 deadletter 诊断。
    /// </summary>
    internal sealed class YokiFrameFileBridgeDeadletterInfo
    {
        /// <summary>获取或设置协议版本。</summary>
        public int ProtocolVersion { get; set; } = YokiFrameFileBridgeContract.PROTOCOL_VERSION;

        /// <summary>获取或设置 engine ID。</summary>
        public string EngineId { get; set; } = string.Empty;

        /// <summary>获取或设置原始命令路径。</summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>获取或设置错误码。</summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>获取或设置错误说明。</summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>获取或设置写入时间。</summary>
        public string WrittenAtUtc { get; set; } = string.Empty;
    }
}
#endif
