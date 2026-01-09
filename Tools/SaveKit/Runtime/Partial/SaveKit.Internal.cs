using System;
using System.Collections.Generic;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 内部方法 - 序列化/反序列化/迁移/工具方法
    /// </summary>
    public static partial class SaveKit
    {
        #region 内部工具方法

        internal static void ValidateSlotId(int slotId)
        {
            if (slotId < 0 || slotId >= sMaxSlots)
                throw new ArgumentOutOfRangeException(nameof(slotId),
                    $"SlotId must be between 0 and {sMaxSlots - 1}");
        }

        internal static string GetSaveFilePath(int slotId) 
            => Path.Combine(GetSavePath(), $"{sFilePrefix}{slotId}{sFileExtension}");

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        internal static int GetMigratorKey(int fromVersion, int toVersion) 
            => fromVersion * 10000 + toVersion;

        #endregion

        #region 序列化

        /// <summary>
        /// 序列化 SaveData 为字节数组
        /// 序列化所有已注册的模块，在线程池中调用
        /// </summary>
        /// <param name="data">存档数据</param>
        /// <param name="serializer">序列化器</param>
        /// <returns>序列化后的字节数组</returns>
        internal static byte[] SerializeSaveData(SaveData data, ISaveSerializer serializer)
        {
            // 序列化所有已注册的模块
            var modules = data.SerializeRegisteredModules(serializer);
            return SerializeModulesToBytes(modules);
        }

        /// <summary>
        /// 将模块数组序列化为字节数组（unsafe 优化）
        /// </summary>
        private static unsafe byte[] SerializeModulesToBytes((int key, byte[] bytes)[] modules)
        {
            var count = modules.Length;
            var totalSize = 4;
            
            foreach (var (_, bytes) in modules)
            {
                totalSize += 8 + bytes.Length;
            }

            var result = new byte[totalSize];
            
            fixed (byte* pResult = result)
            {
                var ptr = pResult;
                
                // 写入模块数量
                *(int*)ptr = count;
                ptr += 4;

                // 写入每个模块
                for (var i = 0; i < count; i++)
                {
                    var (key, bytes) = modules[i];
                    
                    *(int*)ptr = key;
                    ptr += 4;
                    *(int*)ptr = bytes.Length;
                    ptr += 4;
                    
                    if (bytes.Length > 0)
                    {
                        fixed (byte* pSrc = bytes)
                        {
                            Buffer.MemoryCopy(pSrc, ptr, bytes.Length, bytes.Length);
                        }
                        ptr += bytes.Length;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 反序列化字节数组为 SaveData（unsafe 优化版本）
        /// </summary>
        internal static unsafe SaveData DeserializeSaveData(byte[] bytes)
        {
            var data = new SaveData();
            
            fixed (byte* pBytes = bytes)
            {
                var ptr = pBytes;
                
                // 读取模块数量
                var count = *(int*)ptr;
                ptr += 4;

                // 读取每个模块
                for (var i = 0; i < count; i++)
                {
                    var key = *(int*)ptr;
                    ptr += 4;
                    var length = *(int*)ptr;
                    ptr += 4;
                    
                    var value = new byte[length];
                    if (length > 0)
                    {
                        fixed (byte* pDst = value)
                        {
                            Buffer.MemoryCopy(ptr, pDst, length, length);
                        }
                        ptr += length;
                    }
                    
                    data.SetRawModule(key, value);
                }
            }

            return data;
        }

        #endregion

        #region 版本迁移

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
            // 收集所有 key（避免迭代时修改集合）
            var keys = new List<int>(data.ModuleCount);
            foreach (var key in data.GetModuleKeys())
            {
                keys.Add(key);
            }

            // 记录需要移除和添加的数据
            var keysToRemove = new List<int>();
            var dataToAdd = new List<(int key, byte[] bytes)>();

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

            // 执行移除
            foreach (var key in keysToRemove)
            {
                data.RemoveRawModule(key);
            }

            // 执行添加
            foreach (var (key, bytes) in dataToAdd)
            {
                data.SetRawModule(key, bytes);
            }

            return migrator.Migrate(data);
        }

        #endregion
    }
}
