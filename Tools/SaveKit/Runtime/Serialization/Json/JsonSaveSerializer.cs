using System;
using System.IO;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 由宿主 JSON 编解码器驱动的默认 JSON SaveKit 序列化器。
    /// 每个模块 payload 自带 schema 版本；迁移由本类和注册表完成。
    /// </summary>
    public sealed class JsonSaveSerializer : ISaveSerializer, IModuleIdAwareSaveSerializer
    {
        private const int VERSION_PREFIX_BYTES = 4;
        private readonly IJsonSaveCodec codec;
        private readonly JsonSaveMigrationRegistry migrations;
        private readonly int currentSchemaVersion;

        /// <summary>创建 JSON SaveKit 序列化器。</summary>
        /// <param name="codec">宿主 JSON 编解码器。</param>
        /// <param name="currentSchemaVersion">当前 JSON schema 版本。</param>
        /// <param name="migrationRegistry">可选迁移注册表。</param>
        public JsonSaveSerializer(IJsonSaveCodec codec, int currentSchemaVersion, JsonSaveMigrationRegistry migrationRegistry = null)
        {
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            if (currentSchemaVersion < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(currentSchemaVersion));
            }

            this.currentSchemaVersion = currentSchemaVersion;
            migrations = migrationRegistry ?? new JsonSaveMigrationRegistry();
        }

        /// <inheritdoc />
        public string SerializerId
        {
            get { return "json"; }
        }

        /// <summary>获取当前 JSON schema 版本。</summary>
        public int CurrentSchemaVersion
        {
            get { return currentSchemaVersion; }
        }

        /// <summary>获取 JSON 迁移器注册表。</summary>
        public JsonSaveMigrationRegistry Migrations
        {
            get { return migrations; }
        }

        /// <inheritdoc />
        public byte[] Serialize<T>(T data)
        {
            return Pack(currentSchemaVersion, Encoding.UTF8.GetBytes(codec.Serialize(data) ?? string.Empty));
        }

        /// <inheritdoc />
        public T Deserialize<T>(byte[] bytes)
        {
            return Deserialize<T>(SaveModuleIdentity.GetId<T>(), bytes);
        }

        /// <inheritdoc />
        public T Deserialize<T>(string moduleId, byte[] bytes)
        {
            SaveModuleIdentity.ValidateId(moduleId);
            var json = UnpackAndMigrate(moduleId, bytes);
            return codec.Deserialize<T>(Encoding.UTF8.GetString(json));
        }

        /// <inheritdoc />
        public byte[] Serialize(object data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return Pack(currentSchemaVersion, Encoding.UTF8.GetBytes(codec.Serialize(data) ?? string.Empty));
        }

        /// <inheritdoc />
        public void ValidatePayload(string moduleId, byte[] bytes)
        {
            UnpackAndMigrate(moduleId, bytes);
        }

        /// <inheritdoc />
        public void DeserializeOverwrite(byte[] bytes, object target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            DeserializeOverwrite(SaveModuleIdentity.GetId(target.GetType()), bytes, target);
        }

        /// <inheritdoc />
        public void DeserializeOverwrite(string moduleId, byte[] bytes, object target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            SaveModuleIdentity.ValidateId(moduleId);
            var json = UnpackAndMigrate(moduleId, bytes);
            codec.DeserializeOverwrite(Encoding.UTF8.GetString(json), target);
        }

        /// <summary>读取版本前缀并执行严格的 JSON 迁移链。</summary>
        private byte[] UnpackAndMigrate(string moduleId, byte[] bytes)
        {
            if (bytes == null || bytes.Length < VERSION_PREFIX_BYTES)
            {
                throw new InvalidDataException("JSON save payload is missing its schema version.");
            }

            var version = BitConverter.ToInt32(bytes, 0);
            if (version < 0 || version > currentSchemaVersion)
            {
                throw new InvalidDataException("JSON save payload schema version is invalid.");
            }

            var json = new byte[bytes.Length - VERSION_PREFIX_BYTES];
            Buffer.BlockCopy(bytes, VERSION_PREFIX_BYTES, json, 0, json.Length);
            return version == currentSchemaVersion
                ? json
                : migrations.Migrate(moduleId, version, currentSchemaVersion, json);
        }

        /// <summary>把 schema 版本和 JSON 字节封装成模块 payload。</summary>
        private static byte[] Pack(int version, byte[] json)
        {
            using (var stream = new MemoryStream(VERSION_PREFIX_BYTES + json.Length))
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                writer.Write(version);
                writer.Write(json);
                writer.Flush();
                return stream.ToArray();
            }
        }
    }
}
