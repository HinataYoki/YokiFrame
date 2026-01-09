using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 核心操作 - Save/Load/CreateSaveData
    /// </summary>
    public static partial class SaveKit
    {
        #region 核心操作

        /// <summary>
        /// 保存数据到指定槽位
        /// </summary>
        /// <param name="slotId">槽位ID</param>
        /// <param name="data">要保存的数据</param>
        /// <param name="displayName">可选的显示名称</param>
        public static bool Save(int slotId, SaveData data, string displayName = null)
        {
            ValidateSlotId(slotId);
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            try
            {
                // 准备元数据
                var existingMeta = GetMeta(slotId);
                SaveMeta meta;
                
                if (existingMeta.SlotId == 0 && existingMeta.Version == 0)
                {
                    meta = SaveMeta.Create(slotId, sCurrentVersion, displayName);
                }
                else
                {
                    meta = existingMeta;
                    meta.UpdateSaveTime();
                    meta.Version = sCurrentVersion;
                    if (displayName != null)
                        meta.DisplayName = displayName;
                }

                // 序列化数据
                var dataBytes = SerializeSaveData(data, sSerializer);

                // 可选加密（仅加密数据部分，头部不加密）
                if (sEncryptor != null)
                {
                    dataBytes = sEncryptor.Encrypt(dataBytes);
                }

                // 组合头部和数据
                var headerBytes = meta.SerializeHeader();
                var fileBytes = new byte[headerBytes.Length + dataBytes.Length];
                Buffer.BlockCopy(headerBytes, 0, fileBytes, 0, headerBytes.Length);
                Buffer.BlockCopy(dataBytes, 0, fileBytes, headerBytes.Length, dataBytes.Length);

                // 写入单个文件
                var filePath = GetSaveFilePath(slotId);
                File.WriteAllBytes(filePath, fileBytes);

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
        /// <param name="slotId">槽位ID</param>
        /// <returns>加载的数据，失败返回 null</returns>
        public static SaveData Load(int slotId)
        {
            ValidateSlotId(slotId);

            if (!Exists(slotId))
            {
                return null;
            }

            try
            {
                var filePath = GetSaveFilePath(slotId);
                var fileBytes = File.ReadAllBytes(filePath);

                // 解析头部
                if (!SaveMeta.TryDeserializeHeader(fileBytes, out var meta, out var headerSize))
                {
                    KitLogger.Error("[SaveKit] 存档文件头部无效");
                    return null;
                }

                // 提取数据部分
                var dataLength = fileBytes.Length - headerSize;
                var dataBytes = new byte[dataLength];
                Buffer.BlockCopy(fileBytes, headerSize, dataBytes, 0, dataLength);

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
    }
}
