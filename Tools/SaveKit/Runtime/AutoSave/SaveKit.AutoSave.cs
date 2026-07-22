using System;

namespace YokiFrame
{
    /// <summary>SaveKit 自动保存扩展。</summary>
    public static partial class SaveKit
    {
        /// <summary>启用指定目标的自动保存。</summary>
        /// <param name="target">自动保存目标，必须是槽位。</param>
        /// <param name="data">自动保存数据。</param>
        /// <param name="intervalSeconds">自动保存间隔。</param>
        /// <param name="onBeforeSave">写入前更新数据的回调。</param>
        public static void EnableAutoSave(SaveTarget target, SaveData data, float intervalSeconds, Action onBeforeSave = null)
        {
            if (!target.IsSlot)
            {
                throw new ArgumentException("Auto save target must be a slot.", nameof(target));
            }

            ValidateTarget(target);
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (float.IsNaN(intervalSeconds) || float.IsInfinity(intervalSeconds) || intervalSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            }

            sAutoSaveTarget = target;
            sAutoSaveData = data;
            sBeforeAutoSave = onBeforeSave;
            sAutoSaveIntervalSeconds = intervalSeconds;
            sAutoSaveElapsedSeconds = 0f;
            sAutoSaveEnabled = true;
#if UNITY_EDITOR || (GODOT && TOOLS)
            MarkInteractionStateChanged();
#endif
        }

        /// <summary>启用数字槽位自动保存的便捷入口。</summary>
        /// <param name="slotId">槽位编号。</param>
        /// <param name="data">自动保存数据。</param>
        /// <param name="intervalSeconds">自动保存间隔。</param>
        /// <param name="onBeforeSave">写入前回调。</param>
        public static void EnableAutoSave(int slotId, SaveData data, float intervalSeconds, Action onBeforeSave = null)
        {
            EnableAutoSave(SaveTarget.Slot(slotId), data, intervalSeconds, onBeforeSave);
        }

        /// <summary>停用自动保存并清空自动保存状态。</summary>
        public static void DisableAutoSave()
        {
#if UNITY_EDITOR || (GODOT && TOOLS)
            bool wasEnabled = sAutoSaveEnabled;
#endif
            sAutoSaveEnabled = false;
            sAutoSaveTarget = default(SaveTarget);
            sAutoSaveData = null;
            sBeforeAutoSave = null;
            sAutoSaveIntervalSeconds = 0f;
            sAutoSaveElapsedSeconds = 0f;
#if UNITY_EDITOR || (GODOT && TOOLS)
            if (wasEnabled)
            {
                MarkInteractionStateChanged();
            }
#endif
        }

        /// <summary>判断自动保存是否开启。</summary>
        public static bool IsAutoSaveEnabled
        {
            get { return sAutoSaveEnabled; }
        }

        /// <summary>获取自动保存目标。</summary>
        public static SaveTarget GetAutoSaveTarget()
        {
            return sAutoSaveTarget;
        }

        /// <summary>获取自动保存间隔。</summary>
        public static float GetAutoSaveIntervalSeconds()
        {
            return sAutoSaveIntervalSeconds;
        }

        /// <summary>获取当前自动保存计时。</summary>
        public static float GetAutoSaveElapsedSeconds()
        {
            return sAutoSaveElapsedSeconds;
        }

        /// <summary>推进自动保存计时；宿主负责传入 deltaSeconds。</summary>
        /// <param name="deltaSeconds">非负时间增量。</param>
        /// <returns>本次是否成功触发保存。</returns>
        public static bool TickAutoSave(float deltaSeconds)
        {
            if (!sAutoSaveEnabled)
            {
                return false;
            }

            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            if (float.IsNaN(sAutoSaveElapsedSeconds) || float.IsInfinity(sAutoSaveElapsedSeconds) ||
                sAutoSaveElapsedSeconds < 0f || sAutoSaveElapsedSeconds >= sAutoSaveIntervalSeconds)
            {
                sAutoSaveElapsedSeconds = 0f;
            }

            var remainingSeconds = sAutoSaveIntervalSeconds - sAutoSaveElapsedSeconds;
            if (deltaSeconds < remainingSeconds)
            {
                // 普通计时不写 FileBridge；状态只在宿主显式刷新或查询时读取。
                sAutoSaveElapsedSeconds += deltaSeconds;
                return false;
            }

            // 先减去当前周期剩余时间，避免有限 delta 与已累计值相加后溢出为 Infinity。
            sAutoSaveElapsedSeconds = (deltaSeconds - remainingSeconds) % sAutoSaveIntervalSeconds;
            sBeforeAutoSave?.Invoke();
            return Save(sAutoSaveTarget, sAutoSaveData);
        }
    }
}
