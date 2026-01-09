using System;
using System.Collections.Generic;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 槽位管理 - Exists/Delete/GetMeta/GetAllSlots/ScanAllSaves
    /// </summary>
    public static partial class SaveKit
    {
        #region 槽位管理

        /// <summary>
        /// 检查槽位是否存在（验证文件后缀和魔数）
        /// </summary>
        public static bool Exists(int slotId)
        {
            ValidateSlotId(slotId);
            var filePath = GetSaveFilePath(slotId);
            
            if (!File.Exists(filePath))
                return false;

            // 读取固定头部验证魔数
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length < SaveMeta.FIXED_HEADER_SIZE)
                    return false;

                var headerBytes = new byte[SaveMeta.FIXED_HEADER_SIZE];
                fs.Read(headerBytes, 0, SaveMeta.FIXED_HEADER_SIZE);
                
                return SaveMeta.TryReadFixedHeader(headerBytes, out _, out _);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 删除指定槽位
        /// </summary>
        public static bool Delete(int slotId)
        {
            ValidateSlotId(slotId);

            try
            {
                var filePath = GetSaveFilePath(slotId);

                if (File.Exists(filePath))
                    File.Delete(filePath);

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
        /// 获取指定槽位的元数据（仅读取头部，不加载整个文件）
        /// </summary>
        public static SaveMeta GetMeta(int slotId)
        {
            ValidateSlotId(slotId);

            var filePath = GetSaveFilePath(slotId);
            if (!File.Exists(filePath))
            {
                return default;
            }

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length < SaveMeta.FIXED_HEADER_SIZE)
                    return default;

                // 先读取固定头部获取名称长度
                var fixedHeader = new byte[SaveMeta.FIXED_HEADER_SIZE];
                fs.Read(fixedHeader, 0, SaveMeta.FIXED_HEADER_SIZE);
                
                if (!SaveMeta.TryReadFixedHeader(fixedHeader, out var meta, out var nameLength))
                    return default;

                // 如果有名称，继续读取
                if (nameLength > 0 && fs.Length >= SaveMeta.FIXED_HEADER_SIZE + nameLength)
                {
                    var nameBytes = new byte[nameLength];
                    fs.Read(nameBytes, 0, nameLength);
                    meta.DisplayName = System.Text.Encoding.UTF8.GetString(nameBytes);
                }

                return meta;
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

        /// <summary>
        /// 扫描存档目录，获取所有有效存档（不限于槽位范围）
        /// </summary>
        public static List<SaveMeta> ScanAllSaves()
        {
            var result = new List<SaveMeta>();
            var savePath = GetSavePath();
            
            if (!Directory.Exists(savePath))
                return result;

            var files = Directory.GetFiles(savePath, $"*{sFileExtension}");
            foreach (var file in files)
            {
                try
                {
                    using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (fs.Length < SaveMeta.FIXED_HEADER_SIZE)
                        continue;

                    var fixedHeader = new byte[SaveMeta.FIXED_HEADER_SIZE];
                    fs.Read(fixedHeader, 0, SaveMeta.FIXED_HEADER_SIZE);
                    
                    if (!SaveMeta.TryReadFixedHeader(fixedHeader, out var meta, out var nameLength))
                        continue;

                    if (nameLength > 0 && fs.Length >= SaveMeta.FIXED_HEADER_SIZE + nameLength)
                    {
                        var nameBytes = new byte[nameLength];
                        fs.Read(nameBytes, 0, nameLength);
                        meta.DisplayName = System.Text.Encoding.UTF8.GetString(nameBytes);
                    }

                    result.Add(meta);
                }
                catch
                {
                    // 忽略无效文件
                }
            }

            return result;
        }

        #endregion
    }
}
