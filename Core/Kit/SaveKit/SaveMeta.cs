using System;

namespace YokiFrame
{
    /// <summary>
    /// 存档元数据 - 包含存档的基本信息
    /// 使用 struct 保持轻量，避免 GC
    /// </summary>
    [Serializable]
    public struct SaveMeta
    {
        /// <summary>
        /// 存档槽位 ID
        /// </summary>
        public int SlotId;

        /// <summary>
        /// 数据格式版本号
        /// </summary>
        public int Version;

        /// <summary>
        /// 创建时间戳 (Unix 时间戳，秒)
        /// </summary>
        public long CreatedTimestamp;

        /// <summary>
        /// 最后保存时间戳 (Unix 时间戳，秒)
        /// </summary>
        public long LastSavedTimestamp;

        /// <summary>
        /// 可选的显示名称
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// 创建新的存档元数据
        /// </summary>
        public static SaveMeta Create(int slotId, int version, string displayName = null)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return new SaveMeta
            {
                SlotId = slotId,
                Version = version,
                CreatedTimestamp = now,
                LastSavedTimestamp = now,
                DisplayName = displayName
            };
        }

        /// <summary>
        /// 更新最后保存时间
        /// </summary>
        public void UpdateSaveTime()
        {
            LastSavedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// 获取创建时间的 DateTime
        /// </summary>
        public DateTime GetCreatedDateTime()
        {
            return DateTimeOffset.FromUnixTimeSeconds(CreatedTimestamp).LocalDateTime;
        }

        /// <summary>
        /// 获取最后保存时间的 DateTime
        /// </summary>
        public DateTime GetLastSavedDateTime()
        {
            return DateTimeOffset.FromUnixTimeSeconds(LastSavedTimestamp).LocalDateTime;
        }
    }
}
