namespace YokiFrame
{
    /// <summary>
    /// 保存模块的稳定 ID 与原始 payload。
    /// </summary>
    internal readonly struct SaveModuleRecord
    {
        /// <summary>创建模块记录。</summary>
        /// <param name="id">稳定模块 ID。</param>
        /// <param name="bytes">模块 payload。</param>
        public SaveModuleRecord(string id, byte[] bytes)
        {
            Id = id;
            Bytes = bytes;
        }

        /// <summary>模块稳定 ID。</summary>
        public string Id { get; }

        /// <summary>模块 payload 字节。</summary>
        public byte[] Bytes { get; }
    }
}
