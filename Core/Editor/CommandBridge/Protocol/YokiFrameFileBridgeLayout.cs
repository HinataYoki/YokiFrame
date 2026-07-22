#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 定义 FileBridge 在宿主项目内使用的相对目录和固定文件名。
    /// </summary>
    public static class YokiFrameFileBridgeLayout
    {
        /// <summary>
        /// YokiFrame 本地控制面根目录名。
        /// </summary>
        public const string YOKIFRAME_DIRECTORY = ".yokiframe";

        /// <summary>
        /// engine registry 根目录名。
        /// </summary>
        public const string ENGINES_DIRECTORY = "engines";

        /// <summary>
        /// 待处理命令目录名。
        /// </summary>
        public const string COMMANDS_DIRECTORY = "commands";

        /// <summary>
        /// 可选的已认领命令证据目录名；宿主未实现 claim 时可以为空。
        /// </summary>
        public const string PROCESSING_DIRECTORY = "processing";

        /// <summary>
        /// 已完成命令归档目录名。
        /// </summary>
        public const string ARCHIVE_DIRECTORY = "archive";

        /// <summary>
        /// 无法消费命令的死信目录名。
        /// </summary>
        public const string DEADLETTER_DIRECTORY = "deadletter";

        /// <summary>
        /// terminal response 目录名。
        /// </summary>
        public const string RESULTS_DIRECTORY = "results";

        /// <summary>
        /// Kit snapshot 根目录名。
        /// </summary>
        public const string SNAPSHOTS_DIRECTORY = "snapshots";

        /// <summary>
        /// engine 状态目录名。
        /// </summary>
        public const string STATUS_DIRECTORY = "status";

        /// <summary>
        /// engine registry 固定文件名。
        /// </summary>
        public const string ENGINE_REGISTRY_FILE_NAME = "engine.json";

        /// <summary>
        /// heartbeat 固定文件名。
        /// </summary>
        public const string HEARTBEAT_FILE_NAME = "heartbeat.json";

        /// <summary>
        /// FileBridge JSON 文件扩展名。
        /// </summary>
        public const string JSON_EXTENSION = ".json";

        /// <summary>
        /// terminal response 文件名后缀。
        /// </summary>
        public const string RESPONSE_FILE_SUFFIX = "-response.json";
    }
}
#endif
