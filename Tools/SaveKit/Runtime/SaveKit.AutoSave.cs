using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 自动保存、槽位扫描和重置 API。
    /// </summary>
    public static partial class SaveKit
    {
        /// <summary>
        /// 启用自动保存。
        /// </summary>
        /// <param name="slotId">自动保存目标槽位。</param>
        /// <param name="data">需要自动保存的数据。</param>
        /// <param name="intervalSeconds">自动保存间隔秒数。</param>
        /// <param name="onBeforeSave">保存前回调。</param>
        public static void EnableAutoSave(int slotId, SaveData data, float intervalSeconds, Action onBeforeSave = null)
        {
            ValidateSlotId(slotId);
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (intervalSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Interval must be > 0.");
            }

            sAutoSaveSlotId = slotId;
            sAutoSaveData = data;
            sBeforeAutoSave = onBeforeSave;
            sAutoSaveIntervalSeconds = intervalSeconds;
            sAutoSaveElapsedSeconds = 0f;
            sAutoSaveEnabled = true;
        }

        /// <summary>
        /// 停用自动保存并清空自动保存状态。
        /// </summary>
        public static void DisableAutoSave()
        {
            sAutoSaveEnabled = false;
            sAutoSaveSlotId = 0;
            sAutoSaveData = null;
            sBeforeAutoSave = null;
            sAutoSaveIntervalSeconds = 0f;
            sAutoSaveElapsedSeconds = 0f;
        }

        /// <summary>
        /// 当前是否启用了自动保存。
        /// </summary>
        public static bool IsAutoSaveEnabled
        {
            get { return sAutoSaveEnabled; }
        }

        internal static int GetAutoSaveSlotId()
        {
            return sAutoSaveSlotId;
        }

        internal static float GetAutoSaveIntervalSeconds()
        {
            return sAutoSaveIntervalSeconds;
        }

        internal static float GetAutoSaveElapsedSeconds()
        {
            return sAutoSaveElapsedSeconds;
        }

        /// <summary>
        /// 推进自动保存计时。
        /// </summary>
        /// <param name="deltaSeconds">宿主传入的时间增量。</param>
        /// <returns>本次触发保存并成功写入时返回 true。</returns>
        public static bool TickAutoSave(float deltaSeconds)
        {
            if (!sAutoSaveEnabled)
            {
                return false;
            }

            // SaveKit 不启动线程、协程或引擎计时器；Unity/Godot Adapter 负责把宿主 deltaTime 转进来。
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Delta seconds must be >= 0.");
            }

            sAutoSaveElapsedSeconds += deltaSeconds;
            if (sAutoSaveElapsedSeconds < sAutoSaveIntervalSeconds)
            {
                return false;
            }

            sAutoSaveElapsedSeconds %= sAutoSaveIntervalSeconds;
            sBeforeAutoSave?.Invoke();
            return Save(sAutoSaveSlotId, sAutoSaveData);
        }

        /// <summary>
        /// 检查指定槽位是否存在有效保存。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <returns>槽位存在有效保存时返回 true。</returns>
        public static bool Exists(int slotId)
        {
            ValidateSlotId(slotId);
            var bytes = sStorage.Read(slotId);
            if (bytes == null)
            {
                return false;
            }

            SaveMeta meta;
            int nameLength;
            return SaveMeta.TryReadFixedHeader(bytes, out meta, out nameLength);
        }

        /// <summary>
        /// 删除指定槽位保存。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <returns>删除成功时返回 true。</returns>
        public static bool Delete(int slotId)
        {
            ValidateSlotId(slotId);
            return sStorage.Delete(slotId);
        }

        /// <summary>
        /// 获取指定槽位的保存元数据。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <returns>保存元数据；槽位无效时返回默认值。</returns>
        public static SaveMeta GetMeta(int slotId)
        {
            ValidateSlotId(slotId);
            var bytes = sStorage.Read(slotId);
            if (bytes == null)
            {
                return default(SaveMeta);
            }

            SaveMeta meta;
            int headerSize;
            return SaveMeta.TryDeserializeHeader(bytes, out meta, out headerSize) ? meta : default(SaveMeta);
        }

        /// <summary>
        /// 获取所有有效保存槽位的元数据。
        /// </summary>
        /// <returns>按槽位编号排序的保存元数据列表。</returns>
        public static List<SaveMeta> GetAllSlots()
        {
            List<SaveMeta> metas = new();
            var ids = sStorage.GetSlotIds();
            for (var i = 0; i < ids.Count; i++)
            {
                var slotId = ids[i];
                if (slotId < 0 || slotId >= sMaxSlots)
                {
                    continue;
                }

                if (Exists(slotId))
                {
                    metas.Add(GetMeta(slotId));
                }
            }

            metas.Sort((left, right) => left.SlotId.CompareTo(right.SlotId));
            return metas;
        }

        /// <summary>
        /// 重置 SaveKit 到默认内存存储状态。
        /// </summary>
        public static void Reset()
        {
            DisableAutoSave();
            sSerializer = new RawSaveSerializer();
            sEncryptor = null;
            sStorage = new MemorySaveStorage();
            sCurrentVersion = DEFAULT_VERSION;
            sMaxSlots = DEFAULT_MAX_SLOTS;
            sMigrators.Clear();
        }
    }
}
