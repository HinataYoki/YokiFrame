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
    /// </summary>
    public static partial class SaveKit
    {
        #region 异步保存（回调版本）

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
                    meta = SaveMeta.Create(slotId, GetCurrentVersion());
                }
                else
                {
                    meta.UpdateSaveTime();
                    meta.Version = GetCurrentVersion();
                }

                // 序列化数据（主线程，因为可能涉及 Unity 对象）
                var dataBytes = SerializeSaveData(data);
                var metaBytes = GetSerializer().Serialize(meta);

                // 可选加密
                var encryptor = GetEncryptor();
                if (encryptor != null)
                {
                    dataBytes = encryptor.Encrypt(dataBytes);
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
            var encryptor = GetEncryptor();
            var currentVersion = GetCurrentVersion();
            var serializer = GetSerializer();

            // 在线程池执行文件读取
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var dataBytes = File.ReadAllBytes(dataPath);

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
                    var meta = GetMeta(slotId);
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
                    meta = SaveMeta.Create(slotId, GetCurrentVersion());
                }
                else
                {
                    meta.UpdateSaveTime();
                    meta.Version = GetCurrentVersion();
                }

                // 序列化数据（在主线程完成）
                var dataBytes = SerializeSaveData(data);
                var metaBytes = GetSerializer().Serialize(meta);

                // 可选加密
                var encryptor = GetEncryptor();
                if (encryptor != null)
                {
                    dataBytes = encryptor.Encrypt(dataBytes);
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
                var encryptor = GetEncryptor();
                if (encryptor != null)
                {
                    try
                    {
                        dataBytes = encryptor.Decrypt(dataBytes);
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
                var currentVersion = GetCurrentVersion();
                if (meta.Version < currentVersion)
                {
                    data = MigrateData(data, meta.Version, currentVersion);
                    if (data != null)
                    {
                        await SaveUniTaskAsync(slotId, data, cancellationToken);
                    }
                }

                data.SetSerializer(GetSerializer());
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

        #endregion
#endif
    }
}
