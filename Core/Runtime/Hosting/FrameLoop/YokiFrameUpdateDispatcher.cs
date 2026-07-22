using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 保存纯 C# Runtime 帧监听者，由具体引擎 Adapter 统一投递宿主时间。
    /// </summary>
    public static class YokiFrameUpdateDispatcher
    {
        private static readonly object sSyncRoot = new();
        private static readonly List<IYokiFrameUpdateListener> sListeners = new(4);
        private static IYokiFrameUpdateListener[] sSnapshot = Array.Empty<IYokiFrameUpdateListener>();

        /// <summary>
        /// 注册一个稳定监听者；重复注册同一引用保持幂等。
        /// </summary>
        /// <param name="listener">需要接收宿主帧的纯 C# Runtime 监听者。</param>
        public static void Register(IYokiFrameUpdateListener listener)
        {
            if (listener == null)
            {
                throw new ArgumentNullException(nameof(listener));
            }

            lock (sSyncRoot)
            {
                if (IndexOf(listener) >= 0)
                {
                    return;
                }

                sListeners.Add(listener);
                sSnapshot = sListeners.ToArray();
            }
        }

        /// <summary>
        /// 注销指定监听者；不存在时保持幂等。
        /// </summary>
        /// <param name="listener">不再接收宿主帧的监听者。</param>
        public static void Unregister(IYokiFrameUpdateListener listener)
        {
            if (listener == null)
            {
                return;
            }

            lock (sSyncRoot)
            {
                int index = IndexOf(listener);
                if (index < 0)
                {
                    return;
                }

                sListeners.RemoveAt(index);
                sSnapshot = sListeners.ToArray();
            }
        }

        /// <summary>
        /// 由宿主 Adapter 在每帧调用；稳定注册后该热路径不创建集合或委托快照。
        /// </summary>
        /// <param name="scaledDeltaTime">受宿主时间缩放影响的秒数。</param>
        /// <param name="unscaledDeltaTime">不受宿主时间缩放影响的秒数。</param>
        public static void Tick(float scaledDeltaTime, float unscaledDeltaTime)
        {
            ValidateDeltaTime(scaledDeltaTime, nameof(scaledDeltaTime));
            ValidateDeltaTime(unscaledDeltaTime, nameof(unscaledDeltaTime));

            IYokiFrameUpdateListener[] snapshot = sSnapshot;
            for (var index = 0; index < snapshot.Length; index++)
            {
                try
                {
                    snapshot[index].OnFrameUpdate(scaledDeltaTime, unscaledDeltaTime);
                }
                catch (Exception exception)
                {
                    LogKit.Error("[FrameLoop] Runtime listener update failed: " + exception);
                }
            }
        }

        /// <summary>
        /// 通知全部监听者关闭当前宿主代际，但不移除静态注册，支持无 Domain Reload 重进。
        /// </summary>
        public static void ResetListeners()
        {
            IYokiFrameUpdateListener[] snapshot = sSnapshot;
            for (var index = 0; index < snapshot.Length; index++)
            {
                try
                {
                    snapshot[index].OnHostReset();
                }
                catch (Exception exception)
                {
                    LogKit.Error("[FrameLoop] Runtime listener reset failed: " + exception);
                }
            }
        }

        /// <summary>
        /// 使用引用相等查找监听者，避免业务类型重写 Equals 后错误覆盖另一实例。
        /// </summary>
        /// <param name="listener">目标监听者。</param>
        /// <returns>匹配索引；不存在时返回 -1。</returns>
        private static int IndexOf(IYokiFrameUpdateListener listener)
        {
            for (var index = 0; index < sListeners.Count; index++)
            {
                if (ReferenceEquals(sListeners[index], listener))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// 拒绝负数、NaN 和无穷时间，防止无效宿主帧污染所有 Runtime 监听者。
        /// </summary>
        /// <param name="deltaTime">待校验秒数。</param>
        /// <param name="parameterName">异常参数名。</param>
        private static void ValidateDeltaTime(float deltaTime, string parameterName)
        {
            if (deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                throw new ArgumentOutOfRangeException(parameterName, deltaTime, "Frame delta time must be finite and non-negative.");
            }
        }
    }
}
