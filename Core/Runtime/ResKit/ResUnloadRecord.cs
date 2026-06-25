namespace YokiFrame
{
    /// <summary>
    /// 描述一次资源卸载记录。
    /// </summary>
    public sealed class ResUnloadRecord
    {
        /// <summary>
        /// 资源路径。
        /// </summary>
        public string Path;

        /// <summary>
        /// 资源类型名称。
        /// </summary>
        public string TypeName;

        /// <summary>
        /// 加载该资源的 Provider 名称。
        /// </summary>
        public string ProviderName;

        /// <summary>
        /// 卸载发生的 UTC 时间。
        /// </summary>
        public string UnloadTimeUtc;
    }
}
