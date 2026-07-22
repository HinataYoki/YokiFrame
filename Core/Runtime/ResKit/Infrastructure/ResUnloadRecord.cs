#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>描述一次已完成的资源卸载，供诊断工具读取隔离副本。</summary>
    public sealed class ResUnloadRecord
    {
        /// <summary>获取资源路径。</summary>
        public string Path;

        /// <summary>获取资源的完整 CLR 类型名。</summary>
        public string TypeName;

        /// <summary>获取创建并释放该资源的 Provider 名称。</summary>
        public string ProviderName;

        /// <summary>获取 ISO 8601 格式的 UTC 卸载时间。</summary>
        public string UnloadTimeUtc;
    }
}
#endif
