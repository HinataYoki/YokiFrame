using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 存档系统静态入口类
    /// 提供存档的读写、删除、管理等操作
    /// </summary>
    public static class SaveKit
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
                    // 新存档
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
                        // 保存迁移后的数据
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
            var result = new List<SaveMeta>();
            var savePath = GetSavePath();

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

        private static void ValidateSlotId(int slotId)
        {
            if (slotId < 0 || slotId >= sMaxSlots)
                throw new ArgumentOutOfRangeException(nameof(slotId),
                    $"SlotId must be between 0 and {sMaxSlots - 1}");
        }

        private static string GetDataFilePath(int slotId)
        {
            return Path.Combine(GetSavePath(), $"save_{slotId}.dat");
        }

        private static string GetMetaFilePath(int slotId)
        {
            return Path.Combine(GetSavePath(), $"save_{slotId}.meta");
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static int GetMigratorKey(int fromVersion, int toVersion)
        {
            return fromVersion * 10000 + toVersion;
        }

        private static byte[] SerializeSaveData(SaveData data)
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

        private static SaveData DeserializeSaveData(byte[] bytes)
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

        private static SaveData MigrateData(SaveData data, int fromVersion, int toVersion)
        {
            var currentVersion = fromVersion;

            while (currentVersion < toVersion)
            {
                var nextVersion = currentVersion + 1;
                var key = GetMigratorKey(currentVersion, nextVersion);

                if (sMigrators.TryGetValue(key, out var migrator))
                {
                    // 检查是否是原始字节迁移器
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
            // 复用 List 避免 GC
            var keys = new List<int>(data.GetModuleKeys());
            var keysToRemove = new List<int>();
            var dataToAdd = new List<(int key, byte[] bytes)>();
            
            foreach (var oldTypeKey in keys)
            {
                var rawBytes = data.GetRawModule(oldTypeKey);
                if (rawBytes == null) continue;

                // 调用迁移器处理原始字节
                var migratedBytes = migrator.MigrateBytes(oldTypeKey, rawBytes, out var newTypeKey);
                
                if (migratedBytes != null)
                {
                    // 如果 key 改变了，需要删除旧 key 并添加新 key
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

            // 删除旧 key
            foreach (var key in keysToRemove)
            {
                data.RemoveRawModule(key);
            }

            // 添加新 key
            foreach (var (key, bytes) in dataToAdd)
            {
                data.SetRawModule(key, bytes);
            }

            // 也调用标准 Migrate 方法，允许添加/删除模块
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

        #region Architecture 集成

        /// <summary>
        /// 从 Architecture 收集所有 IModel 数据
        /// </summary>
        /// <typeparam name="T">Architecture 类型</typeparam>
        /// <param name="data">要填充的 SaveData</param>
        public static void CollectFromArchitecture<T>(SaveData data) where T : Architecture<T>, new()
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var architecture = Architecture<T>.Interface;
            var models = new List<IModel>();

            // 获取所有 IModel 服务
            CollectModelsFromArchitecture(architecture, models);

            foreach (var model in models)
            {
                var modelType = model.GetType();
                var typeKey = modelType.FullName.GetHashCode();

                // 使用 JsonUtility 序列化 Model
                var jsonData = JsonUtility.ToJson(model);
                var modelWrapper = new SerializableModelData
                {
                    TypeName = modelType.AssemblyQualifiedName,
                    Data = jsonData
                };

                var bytes = sSerializer.Serialize(modelWrapper);
                data.SetRawModule(typeKey, bytes);
            }

            KitLogger.Log($"[SaveKit] 从 Architecture 收集了 {models.Count} 个 Model");
        }

        /// <summary>
        /// 将 SaveData 应用到 Architecture 的 IModel
        /// </summary>
        /// <typeparam name="T">Architecture 类型</typeparam>
        /// <param name="data">包含数据的 SaveData</param>
        public static void ApplyToArchitecture<T>(SaveData data) where T : Architecture<T>, new()
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var architecture = Architecture<T>.Interface;
            var models = new List<IModel>();
            CollectModelsFromArchitecture(architecture, models);

            var appliedCount = 0;

            foreach (var model in models)
            {
                var modelType = model.GetType();
                var typeKey = modelType.FullName.GetHashCode();

                if (!data.HasRawModule(typeKey))
                {
                    continue;
                }

                try
                {
                    var bytes = data.GetRawModule(typeKey);
                    var modelWrapper = sSerializer.Deserialize<SerializableModelData>(bytes);

                    if (modelWrapper.TypeName != modelType.AssemblyQualifiedName)
                    {
                        KitLogger.Warning($"[SaveKit] 类型不匹配: {modelWrapper.TypeName} vs {modelType.AssemblyQualifiedName}");
                        continue;
                    }

                    // 使用 JsonUtility 覆盖对象数据
                    JsonUtility.FromJsonOverwrite(modelWrapper.Data, model);
                    appliedCount++;
                }
                catch (Exception ex)
                {
                    KitLogger.Warning($"[SaveKit] 应用数据到 {modelType.Name} 失败: {ex.Message}");
                }
            }

            KitLogger.Log($"[SaveKit] 已应用 {appliedCount} 个 Model 数据");
        }

        private static void CollectModelsFromArchitecture(IArchitecture architecture, List<IModel> models)
        {
            // 使用反射获取私有字段 mServices
            var archType = architecture.GetType();
            var servicesField = archType.GetField("mServices",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (servicesField == null)
            {
                KitLogger.Warning("[SaveKit] 无法访问 Architecture 的服务列表");
                return;
            }

            var services = servicesField.GetValue(architecture) as Dictionary<Type, IService>;
            if (services == null) return;

            foreach (var service in services.Values)
            {
                if (service is IModel model)
                {
                    models.Add(model);
                }
            }
        }

        /// <summary>
        /// 用于序列化 Model 数据的包装类
        /// </summary>
        [Serializable]
        private class SerializableModelData
        {
            public string TypeName;
            public string Data;
        }

        #endregion

        #region 异步保存

        /// <summary>
        /// 异步保存数据到指定槽位（在后台线程执行文件IO）
        /// </summary>
        /// <param name="slotId">槽位ID</param>
        /// <param name="data">要保存的数据</param>
        /// <param name="onComplete">完成回调，参数为是否成功</param>
        public static void SaveAsync(int slotId, SaveData data, Action<bool> onComplete = null)
        {
            ValidateSlotId(slotId);
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            try
            {
                // 准备元数据（主线程）
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

                // 序列化数据（主线程，因为可能涉及 Unity 对象）
                var dataBytes = SerializeSaveData(data);
                var metaBytes = sSerializer.Serialize(meta);

                // 可选加密
                if (sEncryptor != null)
                {
                    dataBytes = sEncryptor.Encrypt(dataBytes);
                }

                var dataPath = GetDataFilePath(slotId);
                var metaPath = GetMetaFilePath(slotId);

                // 在线程池执行文件写入
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        File.WriteAllBytes(dataPath, dataBytes);
                        File.WriteAllBytes(metaPath, metaBytes);
                        
                        // 回调到主线程（如果需要）
                        KitLogger.Log($"[SaveKit] 异步存档保存成功: 槽位 {slotId}");
                        onComplete?.Invoke(true);
                    }
                    catch (Exception ex)
                    {
                        KitLogger.Error($"[SaveKit] 异步存档保存失败: {ex.Message}");
                        onComplete?.Invoke(false);
                    }
                });
            }
            catch (Exception ex)
            {
                KitLogger.Error($"[SaveKit] 异步存档准备失败: {ex.Message}");
                onComplete?.Invoke(false);
            }
        }

        /// <summary>
        /// 异步加载数据
        /// </summary>
        /// <param name="slotId">槽位ID</param>
        /// <param name="onComplete">完成回调，参数为加载的数据（失败时为null）</param>
        public static void LoadAsync(int slotId, Action<SaveData> onComplete)
        {
            ValidateSlotId(slotId);
            if (onComplete == null)
                throw new ArgumentNullException(nameof(onComplete));

            if (!Exists(slotId))
            {
                onComplete(null);
                return;
            }

            var dataPath = GetDataFilePath(slotId);

            // 在线程池执行文件读取
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
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
                            onComplete(null);
                            return;
                        }
                    }

                    // 反序列化
                    var data = DeserializeSaveData(dataBytes);
                    if (data == null)
                    {
                        KitLogger.Error("[SaveKit] 反序列化失败");
                        onComplete(null);
                        return;
                    }

                    // 检查版本迁移
                    var meta = GetMeta(slotId);
                    if (meta.Version < sCurrentVersion)
                    {
                        data = MigrateData(data, meta.Version, sCurrentVersion);
                        if (data != null)
                        {
                            // 同步保存迁移后的数据
                            Save(slotId, data);
                        }
                    }

                    data.SetSerializer(sSerializer);
                    KitLogger.Log($"[SaveKit] 异步存档加载成功: 槽位 {slotId}");
                    onComplete(data);
                }
                catch (Exception ex)
                {
                    KitLogger.Error($"[SaveKit] 异步存档加载失败: {ex.Message}");
                    onComplete(null);
                }
            });
        }

        #endregion

        #region 自动保存

        private static CancellationTokenSource sAutoSaveCts;
        private static int sAutoSaveSlotId;
        private static SaveData sAutoSaveData;
        private static Action sOnBeforeAutoSave;
        private static Timer sAutoSaveTimer;

        /// <summary>
        /// 启用自动保存
        /// </summary>
        /// <param name="slotId">保存槽位</param>
        /// <param name="data">要保存的数据</param>
        /// <param name="intervalSeconds">保存间隔（秒）</param>
        /// <param name="onBeforeSave">保存前回调</param>
        public static void EnableAutoSave(int slotId, SaveData data, float intervalSeconds, Action onBeforeSave = null)
        {
            ValidateSlotId(slotId);
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (intervalSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Interval must be > 0");

            DisableAutoSave();

            sAutoSaveSlotId = slotId;
            sAutoSaveData = data;
            sOnBeforeAutoSave = onBeforeSave;
            sAutoSaveCts = new CancellationTokenSource();

            var intervalMs = (int)(intervalSeconds * 1000);
            
            // 使用 System.Threading.Timer 实现定时器，不依赖 Unity 生命周期
            sAutoSaveTimer = new Timer(AutoSaveCallback, null, intervalMs, intervalMs);
            
            KitLogger.Log($"[SaveKit] 自动保存已启用: 槽位 {slotId}, 间隔 {intervalSeconds}s");
        }

        /// <summary>
        /// 禁用自动保存
        /// </summary>
        public static void DisableAutoSave()
        {
            if (sAutoSaveTimer != null)
            {
                sAutoSaveTimer.Dispose();
                sAutoSaveTimer = null;
            }
            
            if (sAutoSaveCts != null)
            {
                sAutoSaveCts.Cancel();
                sAutoSaveCts.Dispose();
                sAutoSaveCts = null;
            }
            
            sAutoSaveData = null;
            sOnBeforeAutoSave = null;
            KitLogger.Log("[SaveKit] 自动保存已禁用");
        }

        /// <summary>
        /// 检查自动保存是否启用
        /// </summary>
        public static bool IsAutoSaveEnabled => sAutoSaveTimer != null;

        private static void AutoSaveCallback(object state)
        {
            if (sAutoSaveCts == null || sAutoSaveCts.IsCancellationRequested)
                return;

            try
            {
                // 注意：回调在线程池线程执行
                // 如果 onBeforeSave 需要访问 Unity API，用户需要自行处理线程同步
                sOnBeforeAutoSave?.Invoke();

                // 异步保存，不阻塞
                SaveAsync(sAutoSaveSlotId, sAutoSaveData);
            }
            catch (Exception ex)
            {
                KitLogger.Error($"[SaveKit] 自动保存失败: {ex.Message}");
            }
        }

        #endregion

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask 异步支持

        /// <summary>
        /// [UniTask] 异步保存数据到指定槽位
        /// </summary>
        public static async UniTask<bool> SaveUniTaskAsync(int slotId, SaveData data, CancellationToken cancellationToken = default)
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

                // 序列化数据（在主线程完成）
                var dataBytes = SerializeSaveData(data);
                var metaBytes = sSerializer.Serialize(meta);

                // 可选加密
                if (sEncryptor != null)
                {
                    dataBytes = sEncryptor.Encrypt(dataBytes);
                }

                var dataPath = GetDataFilePath(slotId);
                var metaPath = GetMetaFilePath(slotId);

                // 在线程池执行文件写入
                await UniTask.RunOnThreadPool(() =>
                {
                    File.WriteAllBytes(dataPath, dataBytes);
                    File.WriteAllBytes(metaPath, metaBytes);
                }, cancellationToken: cancellationToken);

                KitLogger.Log($"[SaveKit] UniTask 异步存档保存成功: 槽位 {slotId}");
                return true;
            }
            catch (OperationCanceledException)
            {
                KitLogger.Log($"[SaveKit] UniTask 异步存档保存已取消: 槽位 {slotId}");
                return false;
            }
            catch (Exception ex)
            {
                KitLogger.Error($"[SaveKit] UniTask 异步存档保存失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// [UniTask] 异步加载数据
        /// </summary>
        public static async UniTask<SaveData> LoadUniTaskAsync(int slotId, CancellationToken cancellationToken = default)
        {
            ValidateSlotId(slotId);

            if (!Exists(slotId))
            {
                return null;
            }

            try
            {
                var dataPath = GetDataFilePath(slotId);

                // 在线程池执行文件读取
                var dataBytes = await UniTask.RunOnThreadPool(() => File.ReadAllBytes(dataPath), cancellationToken: cancellationToken);

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
                        await SaveUniTaskAsync(slotId, data, cancellationToken);
                    }
                }

                data.SetSerializer(sSerializer);
                KitLogger.Log($"[SaveKit] UniTask 异步存档加载成功: 槽位 {slotId}");
                return data;
            }
            catch (OperationCanceledException)
            {
                KitLogger.Log($"[SaveKit] UniTask 异步存档加载已取消: 槽位 {slotId}");
                return null;
            }
            catch (Exception ex)
            {
                KitLogger.Error($"[SaveKit] UniTask 异步存档加载失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// [UniTask] 启用基于 UniTask 的自动保存（推荐）
        /// </summary>
        public static void EnableAutoSaveUniTask(int slotId, SaveData data, float intervalSeconds, Action onBeforeSave = null)
        {
            ValidateSlotId(slotId);
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (intervalSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Interval must be > 0");

            DisableAutoSave();

            sAutoSaveSlotId = slotId;
            sAutoSaveData = data;
            sOnBeforeAutoSave = onBeforeSave;
            sAutoSaveCts = new CancellationTokenSource();

            StartAutoSaveLoopUniTask(intervalSeconds, sAutoSaveCts.Token).Forget();
            KitLogger.Log($"[SaveKit] UniTask 自动保存已启用: 槽位 {slotId}, 间隔 {intervalSeconds}s");
        }

        private static async UniTaskVoid StartAutoSaveLoopUniTask(float intervalSeconds, CancellationToken token)
        {
            var intervalMs = (int)(intervalSeconds * 1000);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await UniTask.Delay(intervalMs, cancellationToken: token);

                    if (token.IsCancellationRequested)
                        break;

                    // 在主线程调用回调
                    sOnBeforeAutoSave?.Invoke();

                    // 异步保存
                    await SaveUniTaskAsync(sAutoSaveSlotId, sAutoSaveData, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    KitLogger.Error($"[SaveKit] UniTask 自动保存失败: {ex.Message}");
                }
            }
        }

        #endregion
#endif
    }
}
