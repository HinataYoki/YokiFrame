#if GODOT && TOOLS
using System;

namespace YokiFrame
{
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

    /// <summary>表示 Godot Editor 返回的 System/get_environment 结果。</summary>
    internal sealed class GodotEditorEnvironmentResult
    {
        /// <summary>获取当前 Godot 项目的用户数据根目录。</summary>
        public string UserDataDir { get; set; } = string.Empty;
    }
}
#endif
