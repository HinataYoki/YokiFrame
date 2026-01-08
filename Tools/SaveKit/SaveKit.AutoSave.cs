using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 自动保存功能扩展
    /// </summary>
    public static partial class SaveKit
    {
        #region 自动保存字段

        private static CancellationTokenSource sAutoSaveCts;
        private static int sAutoSaveSlotId;
        private static SaveData sAutoSaveData;
        private static Action sOnBeforeAutoSave;
        private static Timer sAutoSaveTimer;

        #endregion

        #region 自动保存 API

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
        #region UniTask 自动保存

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
