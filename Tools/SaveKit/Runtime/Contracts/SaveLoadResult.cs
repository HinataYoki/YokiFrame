namespace YokiFrame
{
    /// <summary>描述一次读档尝试的结果状态。</summary>
    public enum SaveLoadStatus
    {
        /// <summary>读取成功。</summary>
        Success = 0,
        /// <summary>目标不存在。</summary>
        Missing = 1,
        /// <summary>容器头或 payload 损坏。</summary>
        Invalid = 2,
        /// <summary>文件使用了不同的序列化后端。</summary>
        SerializerMismatch = 3,
        /// <summary>后端迁移失败。</summary>
        MigrationFailed = 4,
        /// <summary>当前后端不支持该 payload。</summary>
        Unsupported = 5
    }

    /// <summary>SaveKit 读档结果，避免把损坏数据伪装成空 SaveData。</summary>
    public readonly struct SaveLoadResult
    {
        /// <summary>创建读档结果。</summary>
        /// <param name="status">结果状态。</param>
        /// <param name="data">成功时的保存数据。</param>
        /// <param name="meta">成功解析的元数据。</param>
        /// <param name="error">失败时的诊断消息。</param>
        public SaveLoadResult(SaveLoadStatus status, SaveData data, SaveMeta meta, string error)
        {
            Status = status;
            Data = data;
            Meta = meta;
            Error = error;
        }

        /// <summary>获取结果状态。</summary>
        public SaveLoadStatus Status { get; }

        /// <summary>获取成功时的保存数据。</summary>
        public SaveData Data { get; }

        /// <summary>获取解析到的元数据。</summary>
        public SaveMeta Meta { get; }

        /// <summary>获取失败时的诊断消息。</summary>
        public string Error { get; }

        /// <summary>判断是否读取成功。</summary>
        public bool Succeeded
        {
            get { return Status == SaveLoadStatus.Success; }
        }
    }
}
