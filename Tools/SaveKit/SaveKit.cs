using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 存档系统静态入口类
    /// 提供存档的读写、删除、管理等操作
    /// </summary>
    public static partial class SaveKit
    {
        #region 配置字段

        /// <summary>
        /// 序列化器，负责将数据对象转换为字节数组
        /// </summary>
        private static ISaveSerializer sSerializer = new JsonSaveSerializer();

        /// <summary>
        /// 加密器，负责对存档数据进行加密/解密，为 null 时不加密
        /// </summary>
        private static ISaveEncryptor sEncryptor;

        /// <summary>
        /// 存档文件保存路径
        /// </summary>
        private static string sSavePath;

        /// <summary>
        /// 当前数据版本号，用于版本迁移判断
        /// </summary>
        private static int sCurrentVersion = 1;

        /// <summary>
        /// 最大存档槽位数量
        /// </summary>
        private static int sMaxSlots = 10;

        /// <summary>
        /// 版本迁移器字典，key 为 fromVersion * 10000 + toVersion
        /// </summary>
        private static readonly Dictionary<int, ISaveMigrator> sMigrators = new();

        #endregion

        #region 配置方法

        /// <summary>
        /// 设置序列化器
        /// </summary>
        public static void SetSerializer(ISaveSerializer serializer)
        {
            sSerializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            KitLogger.Log($"[SaveKit] 序列化器已切换为: {serializer.GetType().Name}");
        }

        /// <summary>
        /// 获取当前序列化器
        /// </summary>
        public static ISaveSerializer GetSerializer() => sSerializer;

        /// <summary>
        /// 设置加密器（设置为 null 则禁用加密）
        /// </summary>
        public static void SetEncryptor(ISaveEncryptor encryptor)
        {
            sEncryptor = encryptor;
            KitLogger.Log(encryptor != null
                ? $"[SaveKit] 加密器已切换为: {encryptor.GetType().Name}"
                : "[SaveKit] 加密已禁用");
        }

        /// <summary>
        /// 获取当前加密器
        /// </summary>
        public static ISaveEncryptor GetEncryptor() => sEncryptor;

        /// <summary>
        /// 设置存档路径
        /// </summary>
        public static void SetSavePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            sSavePath = path;
            EnsureDirectoryExists(sSavePath);
            KitLogger.Log($"[SaveKit] 存档路径已设置为: {path}");
        }

        /// <summary>
        /// 获取存档路径
        /// </summary>
        public static string GetSavePath()
        {
            if (string.IsNullOrEmpty(sSavePath))
            {
                sSavePath = Path.Combine(Application.persistentDataPath, "Saves");
                EnsureDirectoryExists(sSavePath);
            }
            return sSavePath;
        }

        /// <summary>
        /// 设置当前数据版本
        /// </summary>
        public static void SetCurrentVersion(int version)
        {
            if (version < 1)
                throw new ArgumentOutOfRangeException(nameof(version), "Version must be >= 1");
            sCurrentVersion = version;
        }

        /// <summary>
        /// 获取当前数据版本
        /// </summary>
        public static int GetCurrentVersion() => sCurrentVersion;

        /// <summary>
        /// 设置最大槽位数
        /// </summary>
        public static void SetMaxSlots(int maxSlots)
        {
            if (maxSlots < 1)
                throw new ArgumentOutOfRangeException(nameof(maxSlots), "MaxSlots must be >= 1");
            sMaxSlots = maxSlots;
        }

        /// <summary>
        /// 获取最大槽位数
        /// </summary>
        public static int GetMaxSlots() => sMaxSlots;

        /// <summary>
        /// 注册版本迁移器
        /// </summary>
        public static void RegisterMigrator(ISaveMigrator migrator)
        {
            if (migrator == null)
                throw new ArgumentNullException(nameof(migrator));

            var key = GetMigratorKey(migrator.FromVersion, migrator.ToVersion);
            sMigrators[key] = migrator;
            KitLogger.Log($"[SaveKit] 已注册迁移器: v{migrator.FromVersion} -> v{migrator.ToVersion}");
        }

        #endregion

        #region 核心操作

        /// <summary>
        /// 保存数据到指定槽位
        /// </summary>
        public static bool Save(int slotId, SaveData data)
        {
            ValidateSlotId(slotId);
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            try
            {
                // 准备元数据
                var meta = GetMeta(slotId);
                if (meta.SlotId == 0 && meta.Version == 0)
                {
                    meta = SaveMeta.Create(slotId, sCurrentVersion);
                }
                else
                {
                    meta.UpdateSaveTime();
                    meta.Version = sCurrentVersion;
                }

                // 序列化数据
                var dataBytes = SerializeSaveData(data);

                // 可选加密
                if (sEncryptor != null)
                {
                    dataBytes = sEncryptor.Encrypt(dataBytes);
                }

                // 写入文件
                var dataPath = GetDataFilePath(slotId);
                var metaPath = GetMetaFilePath(slotId);

                File.WriteAllBytes(dataPath, dataBytes);

                var metaBytes = sSerializer.Serialize(meta);
                File.WriteAllBytes(metaPath, metaBytes);

                KitLogger.Log($"[SaveKit] 存档保存成功: 槽位 {slotId}");
                return true;
            }
            catch (Exception ex)
            {
                KitLogger.Error($"[SaveKit] 存档保存失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从指定槽位加载数据
        /// </summary>
        public static SaveData Load(int slotId)
        {
            ValidateSlotId(slotId);

            if (!Exists(slotId))
            {
                return null;
            }

            try
            {
                var dataPath = GetDataFilePath(slotId);
                var dataBytes = File.ReadAllBytes(dataPath);

                // 可选解密
                if (sEncryptor != null)
                {
                    try
                    {
                        dataBytes = sEncryptor.Decrypt(dataBytes);
                    }
                    catch (Exception ex)
                    {
                        KitLogger.Error($"[SaveKit] 解密失败: {ex.Message}");
                        return null;
                    }
                }

                // 反序列化
                var data = DeserializeSaveData(dataBytes);
                if (data == null)
                {
                    KitLogger.Error("[SaveKit] 反序列化失败");
                    return null;
                }

                // 检查版本迁移
                var meta = GetMeta(slotId);
                if (meta.Version < sCurrentVersion)
                {
                    data = MigrateData(data, meta.Version, sCurrentVersion);
                    if (data != null)
                    {
                        Save(slotId, data);
                    }
                }

                data.SetSerializer(sSerializer);
                KitLogger.Log($"[SaveKit] 存档加载成功: 槽位 {slotId}");
                return data;
            }
            catch (Exception ex)
            {
                KitLogger.Error($"[SaveKit] 存档加载失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建新的 SaveData 实例
        /// </summary>
        public static SaveData CreateSaveData()
        {
            var data = new SaveData();
            data.SetSerializer(sSerializer);
            return data;
        }

        #endregion

        #region 槽位管理

        /// <summary>
        /// 检查槽位是否存在
        /// </summary>
        public static bool Exists(int slotId)
        {
            ValidateSlotId(slotId);
            var dataPath = GetDataFilePath(slotId);
            return File.Exists(dataPath);
        }

        /// <summary>
        /// 删除指定槽位
        /// </summary>
        public static bool Delete(int slotId)
        {
            ValidateSlotId(slotId);

            try
            {
                var dataPath = GetDataFilePath(slotId);
                var metaPath = GetMetaFilePath(slotId);

                if (File.Exists(dataPath))
                    File.Delete(dataPath);
                if (File.Exists(metaPath))
                    File.Delete(metaPath);

                KitLogger.Log($"[SaveKit] 存档删除成功: 槽位 {slotId}");
                return true;
            }
            catch (Exception ex)
            {
                KitLogger.Error($"[SaveKit] 存档删除失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取指定槽位的元数据
        /// </summary>
        public static SaveMeta GetMeta(int slotId)
        {
            ValidateSlotId(slotId);

            var metaPath = GetMetaFilePath(slotId);
            if (!File.Exists(metaPath))
            {
                return default;
            }

            try
            {
                var metaBytes = File.ReadAllBytes(metaPath);
                return sSerializer.Deserialize<SaveMeta>(metaBytes);
            }
            catch (Exception ex)
            {
                KitLogger.Error($"[SaveKit] 读取元数据失败: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// 获取所有存在的槽位元数据
        /// </summary>
        public static List<SaveMeta> GetAllSlots()
        {
            var result = new List<SaveMeta>(sMaxSlots);

            for (int i = 0; i < sMaxSlots; i++)
            {
                if (Exists(i))
                {
                    var meta = GetMeta(i);
                    if (meta.SlotId != 0 || meta.Version != 0)
                    {
                        result.Add(meta);
                    }
                }
            }

            return result;
        }

        #endregion

        #region 内部方法

        internal static void ValidateSlotId(int slotId)
        {
            if (slotId < 0 || slotId >= sMaxSlots)
                throw new ArgumentOutOfRangeException(nameof(slotId),
                    $"SlotId must be between 0 and {sMaxSlots - 1}");
        }

        internal static string GetDataFilePath(int slotId) 
            => Path.Combine(GetSavePath(), $"save_{slotId}.dat");

        internal static string GetMetaFilePath(int slotId) 
            => Path.Combine(GetSavePath(), $"save_{slotId}.meta");

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        internal static int GetMigratorKey(int fromVersion, int toVersion) 
            => fromVersion * 10000 + toVersion;

        internal static byte[] SerializeSaveData(SaveData data)
        {
            // 序列化模块数据字典
            var moduleDict = new Dictionary<int, byte[]>();
            foreach (var key in data.GetModuleKeys())
            {
                moduleDict[key] = data.GetRawModule(key);
            }

            // 使用简单的二进制格式
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write(moduleDict.Count);
            foreach (var kvp in moduleDict)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value.Length);
                writer.Write(kvp.Value);
            }

            return ms.ToArray();
        }

        internal static SaveData DeserializeSaveData(byte[] bytes)
        {
            var data = new SaveData();

            using var ms = new MemoryStream(bytes);
            using var reader = new BinaryReader(ms);

            var count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var key = reader.ReadInt32();
                var length = reader.ReadInt32();
                var value = reader.ReadBytes(length);
                data.SetRawModule(key, value);
            }

            return data;
        }

        internal static SaveData MigrateData(SaveData data, int fromVersion, int toVersion)
        {
            var currentVersion = fromVersion;

            while (currentVersion < toVersion)
            {
                var nextVersion = currentVersion + 1;
                var key = GetMigratorKey(currentVersion, nextVersion);

                if (sMigrators.TryGetValue(key, out var migrator))
                {
                    if (migrator is IRawByteMigrator rawMigrator)
                    {
                        data = MigrateWithRawByteMigrator(data, rawMigrator);
                    }
                    else
                    {
                        data = migrator.Migrate(data);
                    }
                    KitLogger.Log($"[SaveKit] 数据迁移: v{currentVersion} -> v{nextVersion}");
                }
                else
                {
                    KitLogger.Warning($"[SaveKit] 未找到迁移器: v{currentVersion} -> v{nextVersion}，尝试继续");
                }

                currentVersion = nextVersion;
            }

            return data;
        }

        private static SaveData MigrateWithRawByteMigrator(SaveData data, IRawByteMigrator migrator)
        {
            // 使用池化 List 避免 GC
            Pool.List<int>(keys =>
            {
                keys.AddRange(data.GetModuleKeys());
                
                Pool.List<int>(keysToRemove =>
                {
                    Pool.List<(int key, byte[] bytes)>(dataToAdd =>
                    {
                        foreach (var oldTypeKey in keys)
                        {
                            var rawBytes = data.GetRawModule(oldTypeKey);
                            if (rawBytes == null) continue;

                            var migratedBytes = migrator.MigrateBytes(oldTypeKey, rawBytes, out var newTypeKey);
                            
                            if (migratedBytes != null)
                            {
                                if (newTypeKey != oldTypeKey)
                                {
                                    keysToRemove.Add(oldTypeKey);
                                    dataToAdd.Add((newTypeKey, migratedBytes));
                                }
                                else
                                {
                                    data.SetRawModule(oldTypeKey, migratedBytes);
                                }
                            }
                        }

                        foreach (var key in keysToRemove)
                        {
                            data.RemoveRawModule(key);
                        }

                        foreach (var (key, bytes) in dataToAdd)
                        {
                            data.SetRawModule(key, bytes);
                        }
                    });
                });
            });

            return migrator.Migrate(data);
        }

        #endregion

        #region 重置（测试用）

        /// <summary>
        /// 重置所有配置（仅用于测试）
        /// </summary>
        public static void Reset()
        {
            DisableAutoSave();
            sSerializer = new JsonSaveSerializer();
            sEncryptor = null;
            sSavePath = null;
            sCurrentVersion = 1;
            sMaxSlots = 10;
            sMigrators.Clear();
        }

        #endregion
    }
}
