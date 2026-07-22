#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 定义所有宿主与工具共同遵守的 FileBridge 协议版本和命令限制。
    /// </summary>
    public static class YokiFrameFileBridgeContract
    {
        /// <summary>
        /// 当前 FileBridge wire contract 版本。
        /// </summary>
        public const int PROTOCOL_VERSION = 2;

        /// <summary>
        /// 命令允许的最小超时时间，单位毫秒。
        /// </summary>
        public const int COMMAND_TIMEOUT_MIN_MS = 1000;

        /// <summary>
        /// 命令允许的最大超时时间，单位毫秒。
        /// </summary>
        public const int COMMAND_TIMEOUT_MAX_MS = 30000;

        /// <summary>
        /// 单个命令 payload JSON 的最大 UTF-8 字节数。
        /// </summary>
        public const int PAYLOAD_MAX_BYTES = 64 * 1024;

        /// <summary>
        /// 单个命令文件的最大 UTF-8 字节数。
        /// </summary>
        public const int COMMAND_FILE_MAX_BYTES = 128 * 1024;
    }

    /// <summary>
    /// 定义 FileBridge 命令的稳定审计来源标识；来源不是认证凭据，授权仍由 CommandPolicy 决定。
    /// </summary>
    public static class YokiFrameCommandSourceContract
    {
        /// <summary>CLI 用户或脚本入口来源。</summary>
        public const string CLI = "cli";

        /// <summary>Workbench 用户界面来源。</summary>
        public const string WORKBENCH = "workbench";

        /// <summary>Codex 自动化来源。</summary>
        public const string CODEX = "codex";

        /// <summary>YokiFrame 之外的通用自动化工具来源，不绑定具体产品或供应商。</summary>
        public const string EXTERNAL_AUTOMATION = "external-automation";
    }
}
#endif
