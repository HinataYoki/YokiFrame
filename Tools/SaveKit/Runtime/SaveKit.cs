using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace YokiFrame
{
    /// <summary>
    /// 引擎无关的 SaveKit 门面。目标寻址、容器格式和后端组合均集中在此处。
    /// </summary>
    public static partial class SaveKit
    {
        private const int CONTAINER_VERSION = 1;
        private static readonly object sBackendLock = new();
        private static ISaveSerializer sSerializer;
        private static ISaveEncryptor sEncryptor;
        private static ISaveStorage sStorage;
        private static Func<ISaveStorage> sDefaultStorageFactory;
        private static Func<ISaveSerializer> sDefaultSerializerFactory;
        private static bool sAutoSaveEnabled;
        private static SaveTarget sAutoSaveTarget;
        private static SaveData sAutoSaveData;
        private static Action sBeforeAutoSave;
        private static float sAutoSaveIntervalSeconds;
        private static float sAutoSaveElapsedSeconds;

        /// <summary>设置当前模块序列化器。</summary>
        /// <param name="saveSerializer">序列化器。</param>
        public static void SetSerializer(ISaveSerializer saveSerializer)
        {
            sSerializer = saveSerializer ?? throw new ArgumentNullException(nameof(saveSerializer));
#if UNITY_EDITOR || (GODOT && TOOLS)
            MarkInteractionStateChanged();
#endif
        }

        /// <summary>获取当前模块序列化器。</summary>
        public static ISaveSerializer GetSerializer()
        {
            EnsureBackend();
            return sSerializer;
        }

        /// <summary>设置 payload 加密器；传入空值表示不加密。</summary>
        /// <param name="saveEncryptor">加密器。</param>
        public static void SetEncryptor(ISaveEncryptor saveEncryptor)
        {
            sEncryptor = saveEncryptor;
#if UNITY_EDITOR || (GODOT && TOOLS)
            MarkInteractionStateChanged();
#endif
        }

        /// <summary>获取当前 payload 加密器。</summary>
        public static ISaveEncryptor GetEncryptor()
        {
            return sEncryptor;
        }

        /// <summary>设置槽位存储后端。</summary>
        /// <param name="saveStorage">存储后端。</param>
        public static void SetStorage(ISaveStorage saveStorage)
        {
            sStorage = saveStorage ?? throw new ArgumentNullException(nameof(saveStorage));
#if UNITY_EDITOR || (GODOT && TOOLS)
            MarkInteractionStateChanged();
#endif
        }

        /// <summary>注册宿主默认后端工厂；实际实例化延迟到首次业务调用。</summary>
        /// <param name="storageFactory">创建默认 Storage 的工厂。</param>
        /// <param name="serializerFactory">创建默认 Serializer 的工厂。</param>
        public static void RegisterDefaultBackendFactory(
            Func<ISaveStorage> storageFactory,
            Func<ISaveSerializer> serializerFactory)
        {
            if (storageFactory == null)
            {
                throw new ArgumentNullException(nameof(storageFactory));
            }

            if (serializerFactory == null)
            {
                throw new ArgumentNullException(nameof(serializerFactory));
            }

            lock (sBackendLock)
            {
                sDefaultStorageFactory = storageFactory;
                sDefaultSerializerFactory = serializerFactory;
            }
        }

        /// <summary>获取当前存储后端。</summary>
        public static ISaveStorage GetStorage()
        {
            EnsureBackend();
            return sStorage;
        }

        /// <summary>创建使用当前序列化器的保存数据容器。</summary>
        public static SaveData CreateSaveData()
        {
            EnsureBackend();
            var data = new SaveData();
            data.SetSerializer(sSerializer);
            return data;
        }

        /// <summary>保存到显式目标。</summary>
        /// <param name="target">槽位或 Global 目标。</param>
        /// <param name="data">保存数据。</param>
        /// <param name="displayName">可选显示名称。</param>
        /// <returns>写入成功时返回 true。</returns>
        public static bool Save(SaveTarget target, SaveData data, string displayName = null)
        {
            ValidateTarget(target);
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var meta = CreateOrUpdateMeta(target, displayName);
            var payload = SerializeSaveData(data, sSerializer);
            if (sEncryptor != null)
            {
                payload = sEncryptor.Encrypt(payload);
            }

            var header = meta.SerializeHeader(payload.Length);
            var fileBytes = new byte[header.Length + payload.Length];
            Buffer.BlockCopy(header, 0, fileBytes, 0, header.Length);
            Buffer.BlockCopy(payload, 0, fileBytes, header.Length, payload.Length);
            sStorage.Write(target, fileBytes);
            data.SetSerializer(sSerializer);
#if UNITY_EDITOR || (GODOT && TOOLS)
            MarkInteractionStateChanged();
#endif
            return true;
        }

        /// <summary>保存到数字槽位的便捷入口。</summary>
        /// <param name="slotId">槽位编号。</param>
        /// <param name="data">保存数据。</param>
        /// <param name="displayName">可选显示名称。</param>
        /// <returns>写入成功时返回 true。</returns>
        public static bool Save(int slotId, SaveData data, string displayName = null)
        {
            return Save(SaveTarget.Slot(slotId), data, displayName);
        }

        /// <summary>尝试读取显式目标并返回结构化状态。</summary>
        /// <param name="target">槽位或 Global 目标。</param>
        /// <returns>读档结果。</returns>
        public static SaveLoadResult TryLoad(SaveTarget target)
        {
            ValidateTarget(target);
            var fileBytes = sStorage.Read(target);
            if (fileBytes == null)
            {
                return new SaveLoadResult(SaveLoadStatus.Missing, null, default(SaveMeta), "Save target does not exist.");
            }

            if (!SaveMeta.TryDeserializeHeader(fileBytes, out var meta, out var headerSize, out var payloadLength))
            {
                return new SaveLoadResult(SaveLoadStatus.Invalid, null, default(SaveMeta), "Save container header is invalid.");
            }

            if (meta.Target != target)
            {
                return new SaveLoadResult(SaveLoadStatus.Invalid, null, meta, "Save target does not match its container header.");
            }

            if (!string.Equals(meta.SerializerId, sSerializer.SerializerId, StringComparison.Ordinal))
            {
                return new SaveLoadResult(SaveLoadStatus.SerializerMismatch, null, meta, "Save serializer does not match the active backend.");
            }

            var payload = new byte[payloadLength];
            Buffer.BlockCopy(fileBytes, headerSize, payload, 0, payloadLength);
            return DeserializePayload(payload, meta);
        }

        /// <summary>读取显式目标；失败时返回空数据，详细原因通过 TryLoad 获取。</summary>
        /// <param name="target">槽位或 Global 目标。</param>
        /// <returns>保存数据；不存在或无效时返回空。</returns>
        public static SaveData Load(SaveTarget target)
        {
            return TryLoad(target).Data;
        }

        /// <summary>读取数字槽位的便捷入口。</summary>
        /// <param name="slotId">槽位编号。</param>
        /// <returns>保存数据；不存在或无效时返回空。</returns>
        public static SaveData Load(int slotId)
        {
            return Load(SaveTarget.Slot(slotId));
        }

        /// <summary>读取数字槽位并返回结构化状态。</summary>
        /// <param name="slotId">槽位编号。</param>
        /// <returns>读档结果。</returns>
        public static SaveLoadResult TryLoad(int slotId)
        {
            return TryLoad(SaveTarget.Slot(slotId));
        }

        /// <summary>检查显式目标是否存在有效容器。</summary>
        /// <param name="target">槽位或 Global 目标。</param>
        /// <returns>容器有效时返回 true。</returns>
        public static bool Exists(SaveTarget target)
        {
            return TryLoadMetadata(target, out _);
        }

        /// <summary>检查数字槽位是否存在有效容器。</summary>
        /// <param name="slotId">槽位编号。</param>
        /// <returns>容器有效时返回 true。</returns>
        public static bool Exists(int slotId)
        {
            return Exists(SaveTarget.Slot(slotId));
        }

        /// <summary>删除显式目标。</summary>
        /// <param name="target">槽位或 Global 目标。</param>
        /// <returns>实际删除时返回 true。</returns>
        public static bool Delete(SaveTarget target)
        {
            ValidateTarget(target);
            bool deleted = sStorage.Delete(target);
#if UNITY_EDITOR || (GODOT && TOOLS)
            if (deleted)
            {
                MarkInteractionStateChanged();
            }
#endif
            return deleted;
        }

        /// <summary>删除数字槽位的便捷入口。</summary>
        /// <param name="slotId">槽位编号。</param>
        /// <returns>实际删除时返回 true。</returns>
        public static bool Delete(int slotId)
        {
            return Delete(SaveTarget.Slot(slotId));
        }

        /// <summary>获取显式目标元数据。</summary>
        /// <param name="target">槽位或 Global 目标。</param>
        /// <returns>有效头部元数据；无效时返回默认值。</returns>
        public static SaveMeta GetMeta(SaveTarget target)
        {
            return TryLoadMetadata(target, out var meta) ? meta : default(SaveMeta);
        }

        /// <summary>获取数字槽位元数据的便捷入口。</summary>
        /// <param name="slotId">槽位编号。</param>
        /// <returns>有效头部元数据；无效时返回默认值。</returns>
        public static SaveMeta GetMeta(int slotId)
        {
            return GetMeta(SaveTarget.Slot(slotId));
        }

        /// <summary>获取全部有效槽位元数据。</summary>
        /// <returns>按槽位编号排序的元数据。</returns>
        public static List<SaveMeta> GetAllSlots()
        {
            return GetAllTargets(SaveTargetKind.Slot);
        }

        /// <summary>获取全部有效 Global 文档元数据。</summary>
        /// <returns>按文档名称排序的元数据。</returns>
        public static List<SaveMeta> GetAllGlobals()
        {
            return GetAllTargets(SaveTargetKind.Global);
        }

        /// <summary>重置为内存后端和原始字节序列化器。</summary>
        public static void Reset()
        {
            DisableAutoSave();
            sSerializer = null;
            sEncryptor = null;
            sStorage = null;
#if UNITY_EDITOR || (GODOT && TOOLS)
            MarkInteractionStateChanged();
#endif
        }

        /// <summary>构造新存档或更新已有存档的头部元数据。</summary>
        private static SaveMeta CreateOrUpdateMeta(SaveTarget target, string displayName)
        {
            if (TryLoadMetadata(target, out var existing))
            {
                if (!string.Equals(existing.SerializerId, sSerializer.SerializerId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Save target already uses serializer " + existing.SerializerId + ". Delete it before switching backends.");
                }

                existing.UpdateSaveTime();
                existing.ContainerVersion = CONTAINER_VERSION;
                existing.SerializerId = sSerializer.SerializerId;
                if (displayName != null)
                {
                    existing.DisplayName = displayName;
                }

                return existing;
            }

            return SaveMeta.Create(target, CONTAINER_VERSION, sSerializer.SerializerId, displayName);
        }

        /// <summary>读取并验证目标元数据，不解析 payload。</summary>
        private static bool TryLoadMetadata(SaveTarget target, out SaveMeta meta)
        {
            ValidateTarget(target);
            var bytes = sStorage.Read(target);
            if (bytes == null || !SaveMeta.TryDeserializeHeader(bytes, out meta, out _, out _))
            {
                meta = default(SaveMeta);
                return false;
            }

            return meta.Target == target;
        }

        /// <summary>获取并解析指定目标类型的元数据列表。</summary>
        private static List<SaveMeta> GetAllTargets(SaveTargetKind kind)
        {
            EnsureBackend();
            var result = new List<SaveMeta>();
            var targets = sStorage.GetTargets(kind);
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (TryLoadMetadata(target, out var meta))
                {
                    result.Add(meta);
                }
            }

            result.Sort((left, right) => string.CompareOrdinal(left.Target.Name, right.Target.Name));
            return result;
        }

        /// <summary>验证目标是否处于当前 SaveKit 配置范围。</summary>
        private static void ValidateTarget(SaveTarget target)
        {
            if (!target.IsSlot && !target.IsGlobal)
            {
                throw new ArgumentException("Save target kind is invalid.", nameof(target));
            }

            EnsureBackend();
        }

        /// <summary>首次业务调用时创建显式注册的宿主后端，未注册时回退到纯 C# 内存/Raw 后端。</summary>
        private static void EnsureBackend()
        {
            if (sStorage != null && sSerializer != null)
            {
                return;
            }

            lock (sBackendLock)
            {
#if UNITY_EDITOR || (GODOT && TOOLS)
                bool interactionStateChanged = false;
#endif
                if (sStorage == null)
                {
                    sStorage = sDefaultStorageFactory == null
                        ? new MemorySaveStorage()
                        : sDefaultStorageFactory();
                    if (sStorage == null)
                    {
                        throw new InvalidOperationException("Default storage factory returned null.");
                    }
#if UNITY_EDITOR || (GODOT && TOOLS)
                    interactionStateChanged = true;
#endif
                }

                if (sSerializer == null)
                {
                    sSerializer = sDefaultSerializerFactory == null
                        ? new RawBytesSaveSerializer()
                        : sDefaultSerializerFactory();
                    if (sSerializer == null)
                    {
                        throw new InvalidOperationException("Default serializer factory returned null.");
                    }
#if UNITY_EDITOR || (GODOT && TOOLS)
                    interactionStateChanged = true;
#endif
                }

#if UNITY_EDITOR || (GODOT && TOOLS)
                if (interactionStateChanged)
                {
                    MarkInteractionStateChanged();
                }
#endif
            }
        }

        /// <summary>解密并解析容器 payload，同时把后端错误映射为稳定状态。</summary>
        private static SaveLoadResult DeserializePayload(byte[] payload, SaveMeta meta)
        {
            try
            {
                if (sEncryptor != null)
                {
                    payload = sEncryptor.Decrypt(payload);
                }

                var data = DeserializeSaveData(payload, sSerializer);
                data.ValidateRawModules(sSerializer);
                data.SetSerializer(sSerializer);
                return new SaveLoadResult(SaveLoadStatus.Success, data, meta, null);
            }
            catch (InvalidDataException exception)
            {
                return new SaveLoadResult(SaveLoadStatus.Invalid, null, meta, exception.Message);
            }
            catch (NotSupportedException exception)
            {
                return new SaveLoadResult(SaveLoadStatus.Unsupported, null, meta, exception.Message);
            }
            catch (CryptographicException exception)
            {
                return new SaveLoadResult(SaveLoadStatus.Invalid, null, meta, exception.Message);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                return new SaveLoadResult(SaveLoadStatus.MigrationFailed, null, meta, exception.Message);
            }
        }
    }
}
