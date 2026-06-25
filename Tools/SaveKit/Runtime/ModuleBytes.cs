namespace YokiFrame
{
    /// <summary>
    /// 保存模块的类型键与序列化字节。
    /// </summary>
    internal readonly struct ModuleBytes
    {
        /// <summary>
        /// 创建模块字节记录。
        /// </summary>
        /// <param name="key">模块类型键。</param>
        /// <param name="bytes">模块序列化字节。</param>
        public ModuleBytes(int key, byte[] bytes)
        {
            Key = key;
            Bytes = bytes;
        }

        /// <summary>
        /// 模块类型键。
        /// </summary>
        public int Key { get; }

        /// <summary>
        /// 模块序列化字节。
        /// </summary>
        public byte[] Bytes { get; }
    }
}
