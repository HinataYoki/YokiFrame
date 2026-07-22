using System;
using System.IO;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 容器头元数据。头部不包含真实模块 payload。
    /// </summary>
    public struct SaveMeta
    {
        private const uint MAGIC = 0x53464B59;
        private const int HEADER_VERSION = 1;
        private const int FIXED_HEADER_SIZE = 52;
        private const int MAX_DISPLAY_NAME_BYTES = 4096;
        private const int MAX_TARGET_NAME_BYTES = 256;
        private const int MAX_SERIALIZER_ID_BYTES = 128;
        private const int MAX_PAYLOAD_BYTES = 64 * 1024 * 1024;

        private readonly struct HeaderValues
        {
            public HeaderValues(int kind, int slotId, long created, long saved, int containerVersion, int displayLength, int targetLength, int serializerLength, int payloadLength)
            {
                Kind = kind;
                SlotId = slotId;
                Created = created;
                Saved = saved;
                ContainerVersion = containerVersion;
                DisplayLength = displayLength;
                TargetLength = targetLength;
                SerializerLength = serializerLength;
                PayloadLength = payloadLength;
            }

            public int Kind { get; }
            public int SlotId { get; }
            public long Created { get; }
            public long Saved { get; }
            public int ContainerVersion { get; }
            public int DisplayLength { get; }
            public int TargetLength { get; }
            public int SerializerLength { get; }
            public int PayloadLength { get; }
        }

        /// <summary>获取当前容器头格式版本。</summary>
        public static int HeaderVersion
        {
            get { return HEADER_VERSION; }
        }

        /// <summary>目标位置。</summary>
        public SaveTarget Target;

        /// <summary>SaveKit 容器格式版本。</summary>
        public int ContainerVersion;

        /// <summary>创建时间戳，单位为 Unix 秒。</summary>
        public long CreatedTimestamp;

        /// <summary>最近保存时间戳，单位为 Unix 秒。</summary>
        public long LastSavedTimestamp;

        /// <summary>用户可见的显示名称。</summary>
        public string DisplayName;

        /// <summary>序列化后端稳定 ID。</summary>
        public string SerializerId;

        /// <summary>创建新的容器元数据。</summary>
        /// <param name="target">保存目标。</param>
        /// <param name="containerVersion">容器格式版本。</param>
        /// <param name="serializerId">序列化后端 ID。</param>
        /// <param name="displayName">显示名称。</param>
        /// <returns>初始化后的元数据。</returns>
        public static SaveMeta Create(SaveTarget target, int containerVersion, string serializerId, string displayName)
        {
            if (string.IsNullOrEmpty(serializerId))
            {
                throw new ArgumentException("Serializer id cannot be empty.", nameof(serializerId));
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return new SaveMeta
            {
                Target = target,
                ContainerVersion = containerVersion,
                CreatedTimestamp = now,
                LastSavedTimestamp = now,
                DisplayName = displayName,
                SerializerId = serializerId
            };
        }

        /// <summary>更新最近保存时间，并保留创建时间。</summary>
        public void UpdateSaveTime()
        {
            LastSavedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>将头部写入字节数组。</summary>
        /// <param name="payloadLength">容器 payload 长度。</param>
        /// <returns>完整头部字节。</returns>
        public byte[] SerializeHeader(int payloadLength)
        {
            if (payloadLength < 0 || payloadLength > MAX_PAYLOAD_BYTES)
            {
                throw new ArgumentOutOfRangeException(nameof(payloadLength));
            }

            var displayBytes = EncodeLimited(DisplayName, MAX_DISPLAY_NAME_BYTES, nameof(DisplayName));
            var targetBytes = Encoding.UTF8.GetBytes(Target.Name);
            if (targetBytes.Length > MAX_TARGET_NAME_BYTES)
            {
                throw new ArgumentException("Save target name is too long.", nameof(Target));
            }

            var serializerBytes = EncodeLimited(SerializerId, MAX_SERIALIZER_ID_BYTES, nameof(SerializerId));
            using (var stream = new MemoryStream(FIXED_HEADER_SIZE + displayBytes.Length + targetBytes.Length + serializerBytes.Length))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(MAGIC);
                writer.Write(HEADER_VERSION);
                writer.Write((int)Target.Kind);
                writer.Write(Target.SlotId);
                writer.Write(CreatedTimestamp);
                writer.Write(LastSavedTimestamp);
                writer.Write(ContainerVersion);
                writer.Write(displayBytes.Length);
                writer.Write(targetBytes.Length);
                writer.Write(serializerBytes.Length);
                writer.Write(payloadLength);
                writer.Write(displayBytes);
                writer.Write(targetBytes);
                writer.Write(serializerBytes);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>解析完整容器头，并返回头部长度及 payload 长度。</summary>
        /// <param name="bytes">完整容器字节。</param>
        /// <param name="meta">解析后的元数据。</param>
        /// <param name="headerSize">头部长度。</param>
        /// <param name="payloadLength">payload 长度。</param>
        /// <returns>格式有效时返回 true。</returns>
        public static bool TryDeserializeHeader(byte[] bytes, out SaveMeta meta, out int headerSize, out int payloadLength)
        {
            meta = default(SaveMeta);
            headerSize = 0;
            payloadLength = 0;
            if (bytes == null || bytes.Length < FIXED_HEADER_SIZE)
            {
                return false;
            }

            try
            {
                using (var stream = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    return TryReadHeader(reader, bytes.Length, out meta, out headerSize, out payloadLength);
                }
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException || exception is EndOfStreamException)
            {
                return false;
            }
        }

        /// <summary>从当前位于容器开头的可读流解析头部，并按完整容器长度校验 payload 边界。</summary>
        /// <param name="stream">当前位置为容器头起点的可读流。</param>
        /// <param name="containerLength">完整容器的字节长度。</param>
        /// <param name="meta">解析后的元数据。</param>
        /// <param name="headerSize">头部长度。</param>
        /// <param name="payloadLength">payload 长度。</param>
        /// <returns>格式有效时返回 true；调用方仍拥有流生命周期。</returns>
        public static bool TryDeserializeHeader(
            Stream stream,
            long containerLength,
            out SaveMeta meta,
            out int headerSize,
            out int payloadLength)
        {
            meta = default(SaveMeta);
            headerSize = 0;
            payloadLength = 0;
            if (stream == null || !stream.CanRead || containerLength < FIXED_HEADER_SIZE)
            {
                return false;
            }

            try
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    return TryReadHeader(reader, containerLength, out meta, out headerSize, out payloadLength);
                }
            }
            catch (Exception exception) when (exception is ArgumentException
                                              || exception is IOException
                                              || exception is EndOfStreamException
                                              || exception is NotSupportedException
                                              || exception is ObjectDisposedException)
            {
                return false;
            }
        }

        /// <summary>读取固定字段和可变文本，并验证完整容器长度。</summary>
        private static bool TryReadHeader(
            BinaryReader reader,
            long containerLength,
            out SaveMeta meta,
            out int headerSize,
            out int payloadLength)
        {
            meta = default(SaveMeta);
            headerSize = 0;
            payloadLength = 0;
            if (!TryReadHeaderValues(reader, containerLength, out var values, out headerSize, out payloadLength))
            {
                return false;
            }

            if (!TryReadHeaderStrings(reader, values, out var displayName, out var targetName, out var serializerId))
            {
                return false;
            }

            if (!TryCreateTarget((SaveTargetKind)values.Kind, values.SlotId, targetName, out var target))
            {
                return false;
            }

            meta = new SaveMeta
            {
                Target = target,
                ContainerVersion = values.ContainerVersion,
                CreatedTimestamp = values.Created,
                LastSavedTimestamp = values.Saved,
                DisplayName = displayName,
                SerializerId = serializerId
            };
            return true;
        }

        /// <summary>读取固定头字段，验证长度边界并确认 payload 与完整容器长度一致。</summary>
        private static bool TryReadHeaderValues(
            BinaryReader reader,
            long containerLength,
            out HeaderValues values,
            out int headerSize,
            out int payloadLength)
        {
            values = default(HeaderValues);
            headerSize = 0;
            payloadLength = 0;
            if (reader.ReadUInt32() != MAGIC || reader.ReadInt32() != HEADER_VERSION)
            {
                return false;
            }

            values = new HeaderValues(
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            if (!AreLengthsValid(values))
            {
                return false;
            }

            headerSize = FIXED_HEADER_SIZE + values.DisplayLength + values.TargetLength + values.SerializerLength;
            payloadLength = values.PayloadLength;
            return headerSize <= containerLength && payloadLength == containerLength - headerSize;
        }

        /// <summary>读取头部三段 UTF-8 文本，并拒绝截断的可变字段。</summary>
        private static bool TryReadHeaderStrings(
            BinaryReader reader,
            HeaderValues values,
            out string displayName,
            out string targetName,
            out string serializerId)
        {
            displayName = string.Empty;
            targetName = string.Empty;
            serializerId = string.Empty;
            byte[] displayBytes = reader.ReadBytes(values.DisplayLength);
            byte[] targetBytes = reader.ReadBytes(values.TargetLength);
            byte[] serializerBytes = reader.ReadBytes(values.SerializerLength);
            if (displayBytes.Length != values.DisplayLength
                || targetBytes.Length != values.TargetLength
                || serializerBytes.Length != values.SerializerLength)
            {
                return false;
            }

            displayName = Encoding.UTF8.GetString(displayBytes);
            targetName = Encoding.UTF8.GetString(targetBytes);
            serializerId = Encoding.UTF8.GetString(serializerBytes);
            return true;
        }

        /// <summary>验证头部中的长度和目标类型字段。</summary>
        private static bool AreLengthsValid(HeaderValues values)
        {
            return values.Kind >= (int)SaveTargetKind.Slot && values.Kind <= (int)SaveTargetKind.Global &&
                   values.DisplayLength >= 0 && values.DisplayLength <= MAX_DISPLAY_NAME_BYTES &&
                   values.TargetLength >= 1 && values.TargetLength <= MAX_TARGET_NAME_BYTES &&
                   values.SerializerLength >= 1 && values.SerializerLength <= MAX_SERIALIZER_ID_BYTES &&
                   values.PayloadLength >= 0 && values.PayloadLength <= MAX_PAYLOAD_BYTES;
        }

        /// <summary>获取本地时区的创建时间。</summary>
        public DateTime GetCreatedDateTime()
        {
            return DateTimeOffset.FromUnixTimeSeconds(CreatedTimestamp).LocalDateTime;
        }

        /// <summary>获取本地时区的最近保存时间。</summary>
        public DateTime GetLastSavedDateTime()
        {
            return DateTimeOffset.FromUnixTimeSeconds(LastSavedTimestamp).LocalDateTime;
        }

        /// <summary>创建目标并验证头部中的名称。</summary>
        private static bool TryCreateTarget(SaveTargetKind kind, int slotId, string name, out SaveTarget target)
        {
            target = default(SaveTarget);
            try
            {
                target = kind == SaveTargetKind.Slot ? SaveTarget.Slot(slotId) : SaveTarget.Global(name);
                return string.Equals(target.Name, name, StringComparison.Ordinal);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>编码并限制显示名称长度。</summary>
        private static byte[] EncodeLimited(string value, int maxBytes, string parameterName)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > maxBytes)
            {
                throw new ArgumentException("Value is too long.", parameterName);
            }

            return bytes;
        }
    }
}
