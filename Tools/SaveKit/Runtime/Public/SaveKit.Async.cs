using System;
using System.IO;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 异步操作扩展
    /// 支持延迟序列化：序列化在线程池执行，主线程零阻塞
    /// </summary>
    public static partial class SaveKit
    {
        #region 异步保存（回调版本）

        /// <summary>
        /// 异步保存数据到指定槽位
        /// 序列化和文件IO都在后台线程执行，主线程零阻塞
        /// </summary>
        /// <param name="slotId">槽位ID</param>
        /// <param name="data">要保存的数据</param>
        /// <param name="onComplete">完成回调，参数为是否成功</param>
        /// <param name="displayName">可选的显示名称</param>
        public static void SaveAsync(int slotId, SaveData data, Action<bool> onComplete = null, string displayName = null)
        {
            ValidateSlotId(slotId);
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            try
            {
                // === 主线程：只做轻量准备 ===
                
                // 准备元数据
                var existingMeta = GetMeta(slotId);
                SaveMeta meta;
                
                if (existingMeta.SlotId == 0 && existingMeta.Version == 0)
                {
                    meta = SaveMeta.Create(slotId, GetCurrentVersion(), displayName);
                }
                else
                {
                    meta = existingMeta;
                    meta.UpdateSaveTime();
                    meta.Version = GetCurrentVersion();
                    if (displayName != null)
                        meta.DisplayName = displayName;
                }

                // 序列化头部（很快）
                var headerBytes = meta.SerializeHeader();
                
                // 捕获引用
                var serializer = GetSerializer();
                var encryptor = GetEncryptor();
                var filePath = GetSaveFilePath(slotId);

                // === 线程池：执行序列化、加密、IO ===
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        // 序列化所有已注册的模块
                        var dataBytes = SerializeSaveData(data, serializer);

                        // 可选加密
                        if (encryptor != null)
                        {
                            dataBytes = encryptor.Encrypt(dataBytes);
                        }

                        // 组合头部和数据
                        var fileBytes = new byte[headerBytes.Length + dataBytes.Length];
                        Buffer.BlockCopy(headerBytes, 0, fileBytes, 0, headerBytes.Length);
                        Buffer.BlockCopy(dataBytes, 0, fileBytes, headerBytes.Length, dataBytes.Length);

                        // 写入文件
                        File.WriteAllBytes(filePath, fileBytes);
                        
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

            var filePath = GetSaveFilePath(slotId);
            var encryptor = GetEncryptor();
            var currentVersion = GetCurrentVersion();
            var serializer = GetSerializer();

            // 在线程池执行文件读取和反序列化
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var fileBytes = File.ReadAllBytes(filePath);

                    // 解析头部
                    if (!SaveMeta.TryDeserializeHeader(fileBytes, out var meta, out var headerSize))
                    {
                        KitLogger.Error("[SaveKit] 存档文件头部无效");
                        onComplete(null);
                        return;
                    }

                    // 提取数据部分
                    var dataLength = fileBytes.Length - headerSize;
                    var dataBytes = new byte[dataLength];
                    Buffer.BlockCopy(fileBytes, headerSize, dataBytes, 0, dataLength);

                    // 可选解密
                    if (encryptor != null)
                    {
                        try
                        {
                            dataBytes = encryptor.Decrypt(dataBytes);
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
                    if (meta.Version < currentVersion)
                    {
                        data = MigrateData(data, meta.Version, currentVersion);
                        if (data != null)
                        {
                            Save(slotId, data);
                        }
                    }

                    data.SetSerializer(serializer);
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

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask 异步支持

        /// <summary>
        /// [UniTask] 异步保存数据到指定槽位
        /// 序列化和IO在线程池执行，主线程零阻塞
        /// </summary>
        public static async UniTask<bool> SaveUniTaskAsync(int slotId, SaveData data, CancellationToken cancellationToken = default, string displayName = null)
        {
            ValidateSlotId(slotId);
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            try
            {
                // === 主线程：只做轻量准备 ===
                
                // 准备元数据
                var existingMeta = GetMeta(slotId);
                SaveMeta meta;
                
                if (existingMeta.SlotId == 0 && existingMeta.Version == 0)
                {
                    meta = SaveMeta.Create(slotId, GetCurrentVersion(), displayName);
                }
                else
                {
                    meta = existingMeta;
                    meta.UpdateSaveTime();
                    meta.Version = GetCurrentVersion();
                    if (displayName != null)
                        meta.DisplayName = displayName;
                }

                // 序列化头部
                var headerBytes = meta.SerializeHeader();
                
                // 捕获引用
                var serializer = GetSerializer();
                var encryptor = GetEncryptor();
                var filePath = GetSaveFilePath(slotId);

                // === 线程池：执行序列化、加密、IO ===
                await UniTask.RunOnThreadPool(() =>
                {
                    // 序列化所有已注册的模块
                    var dataBytes = SerializeSaveData(data, serializer);

                    // 可选加密
                    if (encryptor != null)
                    {
                        dataBytes = encryptor.Encrypt(dataBytes);
                    }

                    // 组合头部和数据
                    var fileBytes = new byte[headerBytes.Length + dataBytes.Length];
                    Buffer.BlockCopy(headerBytes, 0, fileBytes, 0, headerBytes.Length);
                    Buffer.BlockCopy(dataBytes, 0, fileBytes, headerBytes.Length, dataBytes.Length);

                    // 写入文件
                    File.WriteAllBytes(filePath, fileBytes);
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
                var filePath = GetSaveFilePath(slotId);
                var encryptor = GetEncryptor();
                var serializer = GetSerializer();
                var currentVersion = GetCurrentVersion();

                // 在线程池执行文件读取和反序列化
                var data = await UniTask.RunOnThreadPool(() =>
                {
                    var fileBytes = File.ReadAllBytes(filePath);

                    // 解析头部
                    if (!SaveMeta.TryDeserializeHeader(fileBytes, out var meta, out var headerSize))
                    {
                        KitLogger.Error("[SaveKit] 存档文件头部无效");
                        return (null, 0);
                    }

                    // 提取数据部分
                    var dataLength = fileBytes.Length - headerSize;
                    var dataBytes = new byte[dataLength];
                    Buffer.BlockCopy(fileBytes, headerSize, dataBytes, 0, dataLength);

                    // 可选解密
                    if (encryptor != null)
                    {
                        dataBytes = encryptor.Decrypt(dataBytes);
                    }

                    // 反序列化
                    var saveData = DeserializeSaveData(dataBytes);
                    return (saveData, meta.Version);
                }, cancellationToken: cancellationToken);

                if (data.Item1 == null)
                {
                    return null;
                }

                var saveData = data.Item1;
                var fileVersion = data.Item2;

                // 检查版本迁移
                if (fileVersion < currentVersion)
                {
                    saveData = MigrateData(saveData, fileVersion, currentVersion);
                    if (saveData != null)
                    {
                        await SaveUniTaskAsync(slotId, saveData, cancellationToken);
                    }
                }

                saveData.SetSerializer(serializer);
                KitLogger.Log($"[SaveKit] UniTask 异步存档加载成功: 槽位 {slotId}");
                return saveData;
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

        #endregion
#endif
    }
}
