#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame.RuntimeCache
{
    /// <summary>
    /// Runtime manifest 的协议版本、布局版本与缓存版本常量权威源。
    /// </summary>
    public static class RuntimeManifestContract
    {
        /// <summary>manifest JSON 格式的主版本号。</summary>
        public const int MANIFEST_VERSION = 1;

        /// <summary>传统单入口布局版本（仅 guiEntry 或 entrypoint）。</summary>
        public const int LEGACY_LAYOUT_VERSION = 1;

        /// <summary>双入口布局版本（支持 guiEntry 和 cliEntry）。</summary>
        public const int DUAL_ENTRY_LAYOUT_VERSION = 2;

        /// <summary>Runtime 缓存目录结构的布局版本。</summary>
        public const int RUNTIME_CACHE_LAYOUT_VERSION = 1;

        /// <summary>Runtime 状态目录名称。</summary>
        public const string RUNTIME_STATE_DIRECTORY_NAME = ".yokiframe";
    }
}
#endif
