using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 保存数据序列化、反序列化和迁移逻辑。
    /// </summary>
    public static partial class SaveKit
    {
        private const int MIN_SERIALIZED_SAVE_BYTES = 4;
        private const int MAX_SERIALIZED_MODULE_COUNT = 10000;
        private const int MODULE_RECORD_HEADER_BYTES = 8;
        private const int MIGRATOR_KEY_MULTIPLIER = 10000;

        internal static byte[] SerializeSaveData(SaveData data, ISaveSerializer saveSerializer)
        {
            return SerializeModulesToBytes(data.SerializeRegisteredModules(saveSerializer));
        }

        internal static SaveData DeserializeSaveData(byte[] bytes, ISaveSerializer saveSerializer)
        {
            SaveData data = new();
            data.SetSerializer(saveSerializer);

            if (bytes == null || bytes.Length < MIN_SERIALIZED_SAVE_BYTES)
            {
                return data;
            }

            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                var count = reader.ReadInt32();
                if (count < 0 || count > MAX_SERIALIZED_MODULE_COUNT)
                {
                    return data;
                }

                for (var i = 0; i < count; i++)
                {
                    if (stream.Length - stream.Position < MODULE_RECORD_HEADER_BYTES)
                    {
                        break;
                    }

                    var key = reader.ReadInt32();
                    var length = reader.ReadInt32();
                    if (length < 0 || stream.Length - stream.Position < length)
                    {
                        break;
                    }

                    data.SetRawModule(key, reader.ReadBytes(length));
                }
            }

            return data;
        }

        internal static SaveData MigrateData(SaveData data, int fromVersion, int toVersion)
        {
            var version = fromVersion;
            while (version < toVersion)
            {
                var nextVersion = version + 1;
                ISaveMigrator migrator;
                if (sMigrators.TryGetValue(GetMigratorKey(version, nextVersion), out migrator))
                {
                    IRawByteMigrator rawByteMigrator = migrator as IRawByteMigrator;
                    data = rawByteMigrator != null
                        ? MigrateWithRawByteMigrator(data, rawByteMigrator)
                        : migrator.Migrate(data);
                }

                version = nextVersion;
            }

            return data;
        }

        private static SaveData MigrateWithRawByteMigrator(SaveData data, IRawByteMigrator migrator)
        {
            List<int> keys = new(data.GetModuleKeys());
            for (var i = 0; i < keys.Count; i++)
            {
                var oldKey = keys[i];
                var rawBytes = data.GetRawModule(oldKey);
                int newKey;
                var migratedBytes = migrator.MigrateBytes(oldKey, rawBytes, out newKey);
                if (migratedBytes == null)
                {
                    continue;
                }

                if (newKey != oldKey)
                {
                    data.RemoveRawModule(oldKey);
                }

                data.SetRawModule(newKey, migratedBytes);
            }

            return migrator.Migrate(data);
        }

        private static byte[] SerializeModulesToBytes(ModuleBytes[] modules)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(modules.Length);
                for (var i = 0; i < modules.Length; i++)
                {
                    var bytes = modules[i].Bytes ?? Array.Empty<byte>();
                    writer.Write(modules[i].Key);
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void ValidateSlotId(int slotId)
        {
            if (slotId < 0 || slotId >= sMaxSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(slotId), "Slot id must be inside configured max slots.");
            }
        }

        private static int GetMigratorKey(int fromVersion, int toVersion)
        {
            return fromVersion * MIGRATOR_KEY_MULTIPLIER + toVersion;
        }

        private sealed class RawSaveSerializer : ISaveSerializer
        {
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
                    var value = (object)bytes;
                    return (T)value;
                }

                throw new NotSupportedException("Set an engine or project serializer before deserializing " + typeof(T).FullName + ".");
            }

            /// <inheritdoc />
            public byte[] Serialize(object data)
            {
                var bytes = data as byte[];
                if (bytes != null)
                {
                    return bytes;
                }

                throw new NotSupportedException("Set an engine or project serializer before serializing " + data.GetType().FullName + ".");
            }

            /// <inheritdoc />
            public void DeserializeOverwrite(byte[] bytes, object target)
            {
                throw new NotSupportedException("DeserializeOverwrite requires an engine or project serializer.");
            }
        }
    }
}
