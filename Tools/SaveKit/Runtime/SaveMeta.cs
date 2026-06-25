using System;
using System.IO;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 存档头元数据。
    /// </summary>
    public struct SaveMeta
    {
        /// <summary>
        /// 获取 SaveKit 存档头魔数。
        /// </summary>
        public const uint MAGIC = 0x494B4F59;

        /// <summary>
        /// 获取固定头部字节数。
        /// </summary>
        public const int FIXED_HEADER_SIZE = 32;

        /// <summary>
        /// 获取 SaveKit 存档头魔数。
        /// </summary>
        public static uint Magic => MAGIC;

        /// <summary>
        /// 获取固定头部字节数。
        /// </summary>
        public static int FixedHeaderSize => FIXED_HEADER_SIZE;

        /// <summary>
        /// 存档槽位编号。
        /// </summary>
        public int SlotId;

        /// <summary>
        /// 存档数据版本。
        /// </summary>
        public int Version;

        /// <summary>
        /// 创建时间戳，单位为 Unix 秒。
        /// </summary>
        public long CreatedTimestamp;

        /// <summary>
        /// 最近保存时间戳，单位为 Unix 秒。
        /// </summary>
        public long LastSavedTimestamp;

        /// <summary>
        /// 存档展示名称。
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// 创建新的存档头元数据。
        /// </summary>
        /// <param name="slotId">存档槽位编号。</param>
        /// <param name="version">存档数据版本。</param>
        /// <param name="displayName">存档展示名称。</param>
        /// <returns>初始化后的存档头元数据。</returns>
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
        /// 更新最近保存时间戳。
        /// </summary>
        public void UpdateSaveTime()
        {
            LastSavedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>
        /// 获取本地时区下的创建时间。
        /// </summary>
        /// <returns>本地时区创建时间。</returns>
        public DateTime GetCreatedDateTime()
        {
            return DateTimeOffset.FromUnixTimeSeconds(CreatedTimestamp).LocalDateTime;
        }

        /// <summary>
        /// 获取本地时区下的最近保存时间。
        /// </summary>
        /// <returns>本地时区最近保存时间。</returns>
        public DateTime GetLastSavedDateTime()
        {
            return DateTimeOffset.FromUnixTimeSeconds(LastSavedTimestamp).LocalDateTime;
        }

        /// <summary>
        /// 获取完整存档头字节数。
        /// </summary>
        /// <returns>固定头部加展示名称字节数。</returns>
        public int GetHeaderSize()
        {
            return FIXED_HEADER_SIZE + GetDisplayNameBytes().Length;
        }

        /// <summary>
        /// 序列化存档头。
        /// </summary>
        /// <returns>存档头字节数组。</returns>
        public byte[] SerializeHeader()
        {
            var nameBytes = GetDisplayNameBytes();
            using (var stream = new MemoryStream(FIXED_HEADER_SIZE + nameBytes.Length))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(MAGIC);
                writer.Write(Version);
                writer.Write(SlotId);
                writer.Write(CreatedTimestamp);
                writer.Write(LastSavedTimestamp);
                writer.Write(nameBytes.Length);
                writer.Write(nameBytes);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 尝试从完整头部字节中反序列化存档元数据。
        /// </summary>
        /// <param name="bytes">待解析的字节数组。</param>
        /// <param name="meta">解析成功时返回存档元数据。</param>
        /// <param name="headerSize">解析成功时返回完整头部字节数。</param>
        /// <returns>解析成功返回 true，否则返回 false。</returns>
        public static bool TryDeserializeHeader(byte[] bytes, out SaveMeta meta, out int headerSize)
        {
            meta = default(SaveMeta);
            headerSize = 0;

            if (bytes == null || bytes.Length < FIXED_HEADER_SIZE)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    var magic = reader.ReadUInt32();
                    if (magic != MAGIC)
                    {
                        return false;
                    }

                    meta.Version = reader.ReadInt32();
                    meta.SlotId = reader.ReadInt32();
                    meta.CreatedTimestamp = reader.ReadInt64();
                    meta.LastSavedTimestamp = reader.ReadInt64();
                    int nameLength = reader.ReadInt32();
                    if (nameLength < 0)
                    {
                        return false;
                    }

                    headerSize = FIXED_HEADER_SIZE + nameLength;
                    if (bytes.Length < headerSize)
                    {
                        return false;
                    }

                    if (nameLength > 0)
                    {
                        byte[] nameBytes = reader.ReadBytes(nameLength);
                        if (nameBytes.Length != nameLength)
                        {
                            return false;
                        }

                        meta.DisplayName = Encoding.UTF8.GetString(nameBytes);
                    }

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 尝试读取固定头部，不读取展示名称正文。
        /// </summary>
        /// <param name="bytes">待解析的字节数组。</param>
        /// <param name="meta">解析成功时返回存档元数据。</param>
        /// <param name="nameLength">解析成功时返回展示名称字节数。</param>
        /// <returns>解析成功返回 true，否则返回 false。</returns>
        public static bool TryReadFixedHeader(byte[] bytes, out SaveMeta meta, out int nameLength)
        {
            meta = default(SaveMeta);
            nameLength = 0;

            if (bytes == null || bytes.Length < FIXED_HEADER_SIZE)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    var magic = reader.ReadUInt32();
                    if (magic != MAGIC)
                    {
                        return false;
                    }

                    meta.Version = reader.ReadInt32();
                    meta.SlotId = reader.ReadInt32();
                    meta.CreatedTimestamp = reader.ReadInt64();
                    meta.LastSavedTimestamp = reader.ReadInt64();
                    nameLength = reader.ReadInt32();
                    return nameLength >= 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private byte[] GetDisplayNameBytes()
        {
            return string.IsNullOrEmpty(DisplayName)
                ? Array.Empty<byte>()
                : Encoding.UTF8.GetBytes(DisplayName);
        }
    }
}
