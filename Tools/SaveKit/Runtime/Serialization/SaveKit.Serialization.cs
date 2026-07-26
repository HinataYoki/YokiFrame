using System;
using System.IO;
using System.Text;

namespace YokiFrame
{
    /// <summary>SaveKit 容器模块表的严格序列化实现。</summary>
    public static partial class SaveKit
    {
        private const int MIN_MODULE_TABLE_BYTES = 4;
        private const int MAX_MODULE_COUNT = 10000;
        private const int MAX_MODULE_ID_BYTES = 1024;
        private const int MAX_MODULE_PAYLOAD_BYTES = 64 * 1024 * 1024;

        /// <summary>把 SaveData 序列化为模块表 payload。</summary>
        /// <param name="data">保存数据。</param>
        /// <param name="serializer">模块序列化器。</param>
        /// <returns>模块表 payload。</returns>
        private static byte[] SerializeSaveData(SaveData data, ISaveSerializer serializer)
        {
            var records = data.SerializeModules(serializer);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(records.Length);
                for (var i = 0; i < records.Length; i++)
                {
                    var idBytes = Encoding.UTF8.GetBytes(records[i].Id);
                    var bytes = records[i].Bytes ?? Array.Empty<byte>();
                    if (idBytes.Length > MAX_MODULE_ID_BYTES || bytes.Length > MAX_MODULE_PAYLOAD_BYTES)
                    {
                        throw new InvalidDataException("Save module exceeds the configured size limit.");
                    }

                    writer.Write(idBytes.Length);
                    writer.Write(idBytes);
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>严格解析模块表，遇到截断、重复 ID 或多余字节时失败。</summary>
        /// <param name="bytes">模块表 payload。</param>
        /// <param name="serializer">模块序列化器。</param>
        /// <returns>解析后的保存数据。</returns>
        private static SaveData DeserializeSaveData(byte[] bytes, ISaveSerializer serializer)
        {
            if (bytes == null || bytes.Length < MIN_MODULE_TABLE_BYTES)
            {
                throw new InvalidDataException("Save module table is empty or truncated.");
            }

            var data = new SaveData();
            data.SetSerializer(serializer);
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                var count = reader.ReadInt32();
                if (count < 0 || count > MAX_MODULE_COUNT)
                {
                    throw new InvalidDataException("Save module count is outside the configured limit.");
                }

                for (var i = 0; i < count; i++)
                {
                    var idLength = reader.ReadInt32();
                    if (idLength < 1 || idLength > MAX_MODULE_ID_BYTES || stream.Length - stream.Position < idLength)
                    {
                        throw new InvalidDataException("Save module id is invalid or truncated.");
                    }

                    var id = Encoding.UTF8.GetString(reader.ReadBytes(idLength));
                    try
                    {
                        SaveModuleIdentity.ValidateId(id);
                    }
                    catch (ArgumentException exception)
                    {
                        throw new InvalidDataException("Save module id is invalid or truncated.", exception);
                    }
                    var payloadLength = reader.ReadInt32();
                    if (payloadLength < 0 || payloadLength > MAX_MODULE_PAYLOAD_BYTES || stream.Length - stream.Position < payloadLength)
                    {
                        throw new InvalidDataException("Save module payload is invalid or truncated.");
                    }

                    if (data.ContainsRawModule(id))
                    {
                        throw new InvalidDataException("Save module id is duplicated: " + id);
                    }

                    data.SetRawModuleOwned(id, reader.ReadBytes(payloadLength));
                }

                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("Save module table contains trailing bytes.");
                }
            }

            return data;
        }

        /// <summary>仅供原始字节回退序列化使用。</summary>
        private sealed class RawBytesSaveSerializer : ISaveSerializer
        {
            /// <inheritdoc />
            public string SerializerId
            {
                get { return "raw"; }
            }

            /// <inheritdoc />
            public byte[] Serialize<T>(T data)
            {
                return Serialize((object)data);
            }

            /// <inheritdoc />
            public T Deserialize<T>(byte[] bytes)
            {
                if (typeof(T) == typeof(byte[]))
                {
                    return (T)(object)CopyBytes(bytes);
                }

                throw new NotSupportedException("Set a JSON or project serializer before reading typed modules.");
            }

            /// <inheritdoc />
            public byte[] Serialize(object data)
            {
                if (!(data is byte[] bytes))
                {
                    throw new NotSupportedException("Set a JSON or project serializer before writing typed modules.");
                }

                return CopyBytes(bytes);
            }

            /// <inheritdoc />
            public void ValidatePayload(string moduleId, byte[] bytes)
            {
                if (bytes == null)
                {
                    throw new InvalidDataException("Raw save payload cannot be null.");
                }
            }

            /// <inheritdoc />
            public void DeserializeOverwrite(byte[] bytes, object target)
            {
                throw new NotSupportedException("Raw serializer cannot overwrite typed modules.");
            }

            /// <summary>复制原始字节。</summary>
            private static byte[] CopyBytes(byte[] bytes)
            {
                if (bytes == null)
                {
                    throw new ArgumentNullException(nameof(bytes));
                }

                var copy = new byte[bytes.Length];
                Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
                return copy;
            }
        }
    }
}
