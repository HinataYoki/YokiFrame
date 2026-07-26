#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 保存 Runtime 对象池向 Editor/Tools 诊断桥接公开的最小注册信息。
    /// </summary>
    internal readonly struct PoolEditorRegistration
    {
        /// <summary>
        /// 创建对象池的工具侧登记信息。
        /// </summary>
        /// <param name="pool">对象池实例。</param>
        /// <param name="name">池显示名称。</param>
        /// <param name="maxCacheCount">缓存上限。</param>
        /// <param name="inactiveCount">当前缓存对象数量。</param>
        internal PoolEditorRegistration(object pool, string name, int maxCacheCount, int inactiveCount)
        {
            Pool = pool;
            Name = name;
            MaxCacheCount = maxCacheCount;
            InactiveCount = inactiveCount;
        }

        /// <summary>获取对象池实例。</summary>
        internal object Pool { get; }
        /// <summary>获取池显示名称。</summary>
        internal string Name { get; }
        /// <summary>获取缓存上限。</summary>
        internal int MaxCacheCount { get; }
        /// <summary>获取当前缓存对象数量。</summary>
        internal int InactiveCount { get; }
    }

    /// <summary>
    /// Runtime 与 Editor/Tools 之间的单向 PoolKit 诊断桥接；Player 构建不会包含该类型。
    /// </summary>
    internal static class PoolEditorHook
    {
        private static readonly object sLock = new();
        private static readonly Dictionary<object, PoolEditorRegistration> sRegisteredPools = new(PoolReferenceEqualityComparer<object>.Instance);
        private static int sTrackingEnabled;

        /// <summary>对象池创建后触发，供 Editor/Tools 建立稳定诊断身份。</summary>
        internal static event Action<PoolEditorRegistration> PoolRegistered;
        /// <summary>对象池释放后触发，供 Editor/Tools 删除诊断状态。</summary>
        internal static event Action<object> PoolUnregistered;
        /// <summary>对象借出后触发，只有跟踪开启时才会分发。</summary>
        internal static event Action<object, object, int> PoolAllocated;
        /// <summary>对象回收后触发，只有跟踪开启时才会分发。</summary>
        internal static event Action<object, object, int> PoolRecycled;
        /// <summary>没有单对象事件的缓存数量变化后触发。</summary>
        internal static event Action<object, int> PoolCountsUpdated;

        /// <summary>获取 Editor/Tools 是否需要接收高频借还事件。</summary>
        internal static bool IsTrackingEnabled => Volatile.Read(ref sTrackingEnabled) != 0;

        /// <summary>登记对象池并在 Editor/Tools 监听器已经初始化时立即发送注册事件。</summary>
        /// <param name="pool">对象池实例。</param>
        /// <param name="name">池显示名称。</param>
        /// <param name="maxCacheCount">缓存上限。</param>
        /// <param name="inactiveCount">当前缓存对象数量。</param>
        internal static void RegisterPool(object pool, string name, int maxCacheCount, int inactiveCount)
        {
            if (pool == null)
            {
                return;
            }

            var registration = new PoolEditorRegistration(pool, name, maxCacheCount, inactiveCount);
            lock (sLock)
            {
                sRegisteredPools[pool] = registration;
            }

            Invoke(PoolRegistered, registration);
        }

        /// <summary>取消对象池登记并通知 Editor/Tools 删除关联诊断信息。</summary>
        /// <param name="pool">已经释放的对象池。</param>
        internal static void UnregisterPool(object pool)
        {
            if (pool == null)
            {
                return;
            }

            var removed = false;
            lock (sLock)
            {
                removed = sRegisteredPools.Remove(pool);
            }

            if (removed)
            {
                Invoke(PoolUnregistered, pool);
            }
        }

        /// <summary>更新高频借还事件是否应分发到 Editor/Tools 监听器。</summary>
        /// <param name="enabled">跟踪开启时为 true。</param>
        internal static void SetTrackingEnabled(bool enabled)
        {
            Interlocked.Exchange(ref sTrackingEnabled, enabled ? 1 : 0);
        }

        /// <summary>在跟踪开启时发送借出事件。</summary>
        /// <param name="pool">发生借出的对象池。</param>
        /// <param name="obj">被借出的对象。</param>
        /// <param name="inactiveCount">借出后的缓存对象数量。</param>
        internal static void TrackAllocate(object pool, object obj, int inactiveCount)
        {
            if (!IsTrackingEnabled || pool == null || obj == null)
            {
                return;
            }

            Invoke(PoolAllocated, pool, obj, inactiveCount);
        }

        /// <summary>在跟踪开启时发送归还事件。</summary>
        /// <param name="pool">发生归还的对象池。</param>
        /// <param name="obj">被归还的对象。</param>
        /// <param name="inactiveCount">归还后的缓存对象数量。</param>
        internal static void TrackRecycle(object pool, object obj, int inactiveCount)
        {
            if (!IsTrackingEnabled || pool == null || obj == null)
            {
                return;
            }

            Invoke(PoolRecycled, pool, obj, inactiveCount);
        }

        /// <summary>在跟踪开启时发送缓存数量变化事件。</summary>
        /// <param name="pool">发生缓存变化的对象池。</param>
        /// <param name="inactiveCount">当前缓存对象数量。</param>
        internal static void UpdatePoolCounts(object pool, int inactiveCount)
        {
            if (!IsTrackingEnabled || pool == null)
            {
                return;
            }

            Invoke(PoolCountsUpdated, pool, inactiveCount);
        }

        /// <summary>复制当前已登记对象池，供较晚初始化的 Editor/Tools 诊断器补齐状态。</summary>
        /// <param name="result">接收登记信息的列表；方法会先清空。</param>
        internal static void CopyRegisteredPools(List<PoolEditorRegistration> result)
        {
            if (result == null)
            {
                return;
            }

            result.Clear();
            lock (sLock)
            {
                foreach (KeyValuePair<object, PoolEditorRegistration> pair in sRegisteredPools)
                {
                    result.Add(pair.Value);
                }
            }
        }

        /// <summary>清空当前会话的 Runtime 池登记，供诊断器显式重置和测试隔离使用。</summary>
        internal static void ClearRegisteredPools()
        {
            lock (sLock)
            {
                sRegisteredPools.Clear();
            }
        }

        /// <summary>隔离单参数诊断监听异常，确保工具代码不能反向中断对象池业务路径。</summary>
        /// <param name="callback">需要调用的监听器。</param>
        /// <param name="value">监听器参数。</param>
        private static void Invoke<T>(Action<T> callback, T value)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback(value);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
        }

        /// <summary>隔离双参数诊断监听异常，确保工具代码不能反向中断对象池业务路径。</summary>
        /// <param name="callback">需要调用的监听器。</param>
        /// <param name="first">第一个监听器参数。</param>
        /// <param name="second">第二个监听器参数。</param>
        private static void Invoke<TFirst, TSecond>(Action<TFirst, TSecond> callback, TFirst first, TSecond second)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback(first, second);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
        }

        /// <summary>隔离三参数诊断监听异常，确保工具代码不能反向中断对象池业务路径。</summary>
        /// <param name="callback">需要调用的监听器。</param>
        /// <param name="first">第一个监听器参数。</param>
        /// <param name="second">第二个监听器参数。</param>
        /// <param name="third">第三个监听器参数。</param>
        private static void Invoke<TFirst, TSecond, TThird>(
            Action<TFirst, TSecond, TThird> callback,
            TFirst first,
            TSecond second,
            TThird third)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback(first, second, third);
            }
            catch (Exception exception)
            {
                Debug.WriteLine(exception);
            }
        }
    }
}
#endif
