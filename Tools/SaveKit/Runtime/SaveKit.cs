using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 引擎无关保存系统门面。
    /// </summary>
    public static partial class SaveKit
    {
        private const int DEFAULT_VERSION = 1;
        private const int DEFAULT_MAX_SLOTS = 10;

        private static ISaveSerializer sSerializer = new RawSaveSerializer();
        private static ISaveEncryptor sEncryptor;
        private static ISaveStorage sStorage = new MemorySaveStorage();
        private static int sCurrentVersion = DEFAULT_VERSION;
        private static int sMaxSlots = DEFAULT_MAX_SLOTS;
        private static readonly Dictionary<int, ISaveMigrator> sMigrators = new();
        private static bool sAutoSaveEnabled;
        private static int sAutoSaveSlotId;
        private static SaveData sAutoSaveData;
        private static Action sBeforeAutoSave;
        private static float sAutoSaveIntervalSeconds;
        private static float sAutoSaveElapsedSeconds;

        /// <summary>
        /// 设置 SaveKit 使用的序列化器。
        /// </summary>
        /// <param name="saveSerializer">用于保存数据序列化和反序列化的序列化器。</param>
        public static void SetSerializer(ISaveSerializer saveSerializer)
        {
            if (saveSerializer == null)
            {
                throw new ArgumentNullException(nameof(saveSerializer));
            }

            // 序列化由宿主或项目注入，Tools 层只认接口，避免把 Unity/Godot JSON 细节写进 SaveKit。
            sSerializer = saveSerializer;
        }

        /// <summary>
        /// 获取当前序列化器。
        /// </summary>
        /// <returns>当前序列化器。</returns>
        public static ISaveSerializer GetSerializer()
        {
            return sSerializer;
        }

        /// <summary>
        /// 设置 SaveKit 使用的加密器。
        /// </summary>
        /// <param name="saveEncryptor">保存载荷加密器；传入空值表示不加密。</param>
        public static void SetEncryptor(ISaveEncryptor saveEncryptor)
        {
            sEncryptor = saveEncryptor;
        }

        /// <summary>
        /// 获取当前加密器。
        /// </summary>
        /// <returns>当前加密器；未启用加密时返回空。</returns>
        public static ISaveEncryptor GetEncryptor()
        {
            return sEncryptor;
        }

        /// <summary>
        /// 设置 SaveKit 使用的存储后端。
        /// </summary>
        /// <param name="saveStorage">保存槽位存储后端。</param>
        public static void SetStorage(ISaveStorage saveStorage)
        {
            if (saveStorage == null)
            {
                throw new ArgumentNullException(nameof(saveStorage));
            }

            // 存储路径属于 Adapter 边界：Unity/Godot 各自决定目录，业务仍使用同一个 SaveKit 静态入口。
            sStorage = saveStorage;
        }

        /// <summary>
        /// 获取当前存储后端。
        /// </summary>
        /// <returns>当前存储后端。</returns>
        public static ISaveStorage GetStorage()
        {
            return sStorage;
        }

        /// <summary>
        /// 设置当前保存数据版本。
        /// </summary>
        /// <param name="version">当前保存数据版本，必须大于等于 1。</param>
        public static void SetCurrentVersion(int version)
        {
            if (version < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(version), "Version must be >= 1.");
            }

            sCurrentVersion = version;
        }

        /// <summary>
        /// 获取当前保存数据版本。
        /// </summary>
        /// <returns>当前保存数据版本。</returns>
        public static int GetCurrentVersion()
        {
            return sCurrentVersion;
        }

        /// <summary>
        /// 设置最大保存槽位数量。
        /// </summary>
        /// <param name="slots">最大槽位数量，必须大于等于 1。</param>
        public static void SetMaxSlots(int slots)
        {
            if (slots < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(slots), "Max slots must be >= 1.");
            }

            sMaxSlots = slots;
        }

        /// <summary>
        /// 获取最大保存槽位数量。
        /// </summary>
        /// <returns>最大保存槽位数量。</returns>
        public static int GetMaxSlots()
        {
            return sMaxSlots;
        }

        /// <summary>
        /// 注册保存数据迁移器。
        /// </summary>
        /// <param name="migrator">从一个版本迁移到下一个版本的迁移器。</param>
        public static void RegisterMigrator(ISaveMigrator migrator)
        {
            if (migrator == null)
            {
                throw new ArgumentNullException(nameof(migrator));
            }

            sMigrators[GetMigratorKey(migrator.FromVersion, migrator.ToVersion)] = migrator;
        }

        /// <summary>
        /// 创建使用当前序列化器的保存数据容器。
        /// </summary>
        /// <returns>新的保存数据容器。</returns>
        public static SaveData CreateSaveData()
        {
            SaveData data = new();
            data.SetSerializer(sSerializer);
            return data;
        }

        /// <summary>
        /// 保存指定槽位的数据。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <param name="data">需要保存的数据。</param>
        /// <param name="displayName">可选的显示名称。</param>
        /// <returns>保存成功时返回 true。</returns>
        public static bool Save(int slotId, SaveData data, string displayName = null)
        {
            ValidateSlotId(slotId);
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var existingMeta = GetMeta(slotId);
            SaveMeta meta;
            if (!Exists(slotId))
            {
                meta = SaveMeta.Create(slotId, sCurrentVersion, displayName);
            }
            else
            {
                meta = existingMeta;
                meta.UpdateSaveTime();
                meta.Version = sCurrentVersion;
                if (displayName != null)
                {
                    meta.DisplayName = displayName;
                }
            }

            var payload = SerializeSaveData(data, sSerializer);
            if (sEncryptor != null)
            {
                payload = sEncryptor.Encrypt(payload);
            }

            var header = meta.SerializeHeader();
            var fileBytes = new byte[header.Length + payload.Length];
            Buffer.BlockCopy(header, 0, fileBytes, 0, header.Length);
            Buffer.BlockCopy(payload, 0, fileBytes, header.Length, payload.Length);
            sStorage.Write(slotId, fileBytes);
            return true;
        }

        /// <summary>
        /// 读取指定槽位的保存数据。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <returns>读取到的保存数据；槽位不存在或内容无效时返回空。</returns>
        public static SaveData Load(int slotId)
        {
            ValidateSlotId(slotId);
            if (!Exists(slotId))
            {
                return null;
            }

            var fileBytes = sStorage.Read(slotId);
            SaveMeta meta;
            int headerSize;
            if (!SaveMeta.TryDeserializeHeader(fileBytes, out meta, out headerSize))
            {
                return null;
            }

            var payloadLength = fileBytes.Length - headerSize;
            if (payloadLength < 0)
            {
                return null;
            }

            var payload = new byte[payloadLength];
            Buffer.BlockCopy(fileBytes, headerSize, payload, 0, payloadLength);
            if (sEncryptor != null)
            {
                payload = sEncryptor.Decrypt(payload);
            }

            var data = DeserializeSaveData(payload, sSerializer);
            if (data == null)
            {
                return null;
            }

            if (meta.Version < sCurrentVersion)
            {
                data = MigrateData(data, meta.Version, sCurrentVersion);
                if (data != null)
                {
                    data.SetSerializer(sSerializer);
                    Save(slotId, data, meta.DisplayName);
                }
            }

            return data;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步保存指定槽位的数据。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <param name="data">需要保存的数据。</param>
        /// <param name="displayName">可选的显示名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>保存成功时返回 true。</returns>
        public static UniTask<bool> SaveAsync(
#else
        /// <summary>
        /// 异步保存指定槽位的数据。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <param name="data">需要保存的数据。</param>
        /// <param name="displayName">可选的显示名称。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>保存成功时返回 true。</returns>
        public static Task<bool> SaveAsync(
#endif
            int slotId,
            SaveData data,
            string displayName = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
#if YOKIFRAME_UNITASK_SUPPORT
                return UniTask.FromCanceled<bool>(cancellationToken);
#else
                return Task.FromCanceled<bool>(cancellationToken);
#endif
            }

#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.RunOnThreadPool(() =>
#else
            return Task.Run(() =>
#endif
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Save(slotId, data, displayName);
#if YOKIFRAME_UNITASK_SUPPORT
            }, cancellationToken: cancellationToken);
#else
            }, cancellationToken);
#endif
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步保存指定槽位的数据。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <param name="data">需要保存的数据。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>保存成功时返回 true。</returns>
        public static UniTask<bool> SaveAsync(
#else
        /// <summary>
        /// 异步保存指定槽位的数据。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <param name="data">需要保存的数据。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>保存成功时返回 true。</returns>
        public static Task<bool> SaveAsync(
#endif
            int slotId,
            SaveData data,
            CancellationToken cancellationToken)
        {
            return SaveAsync(slotId, data, null, cancellationToken);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 异步读取指定槽位的保存数据。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>读取到的保存数据；槽位不存在或内容无效时返回空。</returns>
        public static UniTask<SaveData> LoadAsync(
#else
        /// <summary>
        /// 异步读取指定槽位的保存数据。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>读取到的保存数据；槽位不存在或内容无效时返回空。</returns>
        public static Task<SaveData> LoadAsync(
#endif
            int slotId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
#if YOKIFRAME_UNITASK_SUPPORT
                return UniTask.FromCanceled<SaveData>(cancellationToken);
#else
                return Task.FromCanceled<SaveData>(cancellationToken);
#endif
            }

#if YOKIFRAME_UNITASK_SUPPORT
            return UniTask.RunOnThreadPool(() =>
#else
            return Task.Run(() =>
#endif
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Load(slotId);
#if YOKIFRAME_UNITASK_SUPPORT
            }, cancellationToken: cancellationToken);
#else
            }, cancellationToken);
#endif
        }
    }
}
