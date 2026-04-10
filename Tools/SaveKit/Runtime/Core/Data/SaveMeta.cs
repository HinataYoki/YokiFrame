using System;
using System.Text;

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
        /// 文件魔数，用于识别有效存档文件
        /// </summary>
        public const uint MAGIC = 0x494B4F59; // "YOKI" 的小端序

        /// <summary>
        /// 固定头部大小（不含 DisplayName）：魔数4 + 版本4 + SlotId4 + 创建时间8 + 保存时间8 + 名称长度4 = 32字节
        /// </summary>
        public const int FIXED_HEADER_SIZE = 32;

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
            => DateTimeOffset.FromUnixTimeSeconds(CreatedTimestamp).LocalDateTime;

        /// <summary>
        /// 获取最后保存时间的 DateTime
        /// </summary>
        public DateTime GetLastSavedDateTime()
            => DateTimeOffset.FromUnixTimeSeconds(LastSavedTimestamp).LocalDateTime;

        /// <summary>
        /// 计算头部总大小（含 DisplayName）
        /// </summary>
        public int GetHeaderSize()
        {
            var nameBytes = string.IsNullOrEmpty(DisplayName) ? 0 : Encoding.UTF8.GetByteCount(DisplayName);
            return FIXED_HEADER_SIZE + nameBytes;
        }

        /// <summary>
        /// 序列化头部到字节数组
        /// </summary>
        public unsafe byte[] SerializeHeader()
        {
            var nameBytes = string.IsNullOrEmpty(DisplayName) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(DisplayName);
            var headerSize = FIXED_HEADER_SIZE + nameBytes.Length;
            var result = new byte[headerSize];

            fixed (byte* pResult = result)
            {
                var ptr = pResult;
                
                *(uint*)ptr = MAGIC;
                ptr += 4;
                *(int*)ptr = Version;
                ptr += 4;
                *(int*)ptr = SlotId;
                ptr += 4;
                *(long*)ptr = CreatedTimestamp;
                ptr += 8;
                *(long*)ptr = LastSavedTimestamp;
                ptr += 8;
                *(int*)ptr = nameBytes.Length;
                ptr += 4;
                
                if (nameBytes.Length > 0)
                {
                    fixed (byte* pName = nameBytes)
                    {
                        Buffer.MemoryCopy(pName, ptr, nameBytes.Length, nameBytes.Length);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 从字节数组反序列化头部（仅读取固定部分 + 名称）
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <param name="meta">输出的元数据</param>
        /// <param name="headerSize">输出的头部总大小</param>
        /// <returns>是否成功（魔数匹配）</returns>
        public static unsafe bool TryDeserializeHeader(byte[] bytes, out SaveMeta meta, out int headerSize)
        {
            meta = default;
            headerSize = 0;

            if (bytes == null || bytes.Length < FIXED_HEADER_SIZE)
                return false;

            fixed (byte* pBytes = bytes)
            {
                var ptr = pBytes;
                
                // 检查魔数
                var magic = *(uint*)ptr;
                if (magic != MAGIC)
                    return false;
                ptr += 4;

                meta.Version = *(int*)ptr;
                ptr += 4;
                meta.SlotId = *(int*)ptr;
                ptr += 4;
                meta.CreatedTimestamp = *(long*)ptr;
                ptr += 8;
                meta.LastSavedTimestamp = *(long*)ptr;
                ptr += 8;
                
                var nameLength = *(int*)ptr;
                ptr += 4;

                headerSize = FIXED_HEADER_SIZE + nameLength;

                // 检查是否有足够的数据读取名称
                if (bytes.Length < headerSize)
                    return false;

                if (nameLength > 0)
                {
                    meta.DisplayName = Encoding.UTF8.GetString(bytes, FIXED_HEADER_SIZE, nameLength);
                }
            }

            return true;
        }

        /// <summary>
        /// 仅读取固定头部（用于快速验证文件）
        /// </summary>
        public static unsafe bool TryReadFixedHeader(byte[] bytes, out SaveMeta meta, out int nameLength)
        {
            meta = default;
            nameLength = 0;

            if (bytes == null || bytes.Length < FIXED_HEADER_SIZE)
                return false;

            fixed (byte* pBytes = bytes)
            {
                var ptr = pBytes;
                
                var magic = *(uint*)ptr;
                if (magic != MAGIC)
                    return false;
                ptr += 4;

                meta.Version = *(int*)ptr;
                ptr += 4;
                meta.SlotId = *(int*)ptr;
                ptr += 4;
                meta.CreatedTimestamp = *(long*)ptr;
                ptr += 8;
                meta.LastSavedTimestamp = *(long*)ptr;
                ptr += 8;
                nameLength = *(int*)ptr;
            }

            return true;
        }
    }
}
