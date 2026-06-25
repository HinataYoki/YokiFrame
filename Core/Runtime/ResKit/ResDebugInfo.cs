namespace YokiFrame
{
    /// <summary>
    /// 描述当前已加载资源的调试信息。
    /// </summary>
    public sealed class ResDebugInfo
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
        /// 当前引用计数。
        /// </summary>
        public int RefCount;

        /// <summary>
        /// 资源加载是否已完成。
        /// </summary>
        public bool IsDone;

        /// <summary>
        /// 加载该资源的 Provider 名称。
        /// </summary>
        public string ProviderName;

        /// <summary>
        /// 资源加载调用来源展示名。
        /// </summary>
        public string Source;

        /// <summary>
        /// 资源加载调用来源文件。
        /// </summary>
        public string SourceFile;

        /// <summary>
        /// 资源加载调用来源行号。
        /// </summary>
        public int SourceLine;
    }
}
