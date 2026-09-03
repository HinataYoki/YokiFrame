using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 通用对象池实现，统一负责缓存、生命周期、安全检查、容量和诊断跟踪。
    /// </summary>
    /// <typeparam name="T">引用类型的池化对象。</typeparam>
    public sealed class ObjectPool<T> : IPool<T>
#if UNITY_EDITOR || (GODOT && TOOLS)
        , IPoolDebugReturn
        , IPoolDebugSnapshot
#endif
        , IDisposable where T : class
    {
        private const int DEFAULT_CAPACITY = 16;

        private readonly Stack<T> mCacheStack;
        private readonly HashSet<T> mCachedObjects = new(PoolReferenceEqualityComparer<T>.Instance);
        private readonly object mSyncRoot = new();
        private readonly Func<T> mFactory;
        private readonly Action<T> mOnAllocated;
        private readonly Action<T> mOnRecycled;
        private readonly int mMaxRetained;
        private bool mDisposed;

        /// <summary>
        /// 创建统一对象池，并按配置预热缓存；仅允许由 PoolKit 门面和共享注册表调用。
        /// </summary>
        /// <param name="factory">缓存为空时创建对象的委托。</param>
        /// <param name="onAllocated">对象借出后的可选生命周期委托。</param>
        /// <param name="onRecycled">对象归还前的可选生命周期委托。</param>
        /// <param name="options">预热和缓存容量配置。</param>
        internal ObjectPool(
            Func<T> factory,
            Action<T> onAllocated,
            Action<T> onRecycled,
            PoolOptions options)
        {
            mFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            mOnAllocated = onAllocated;
            mOnRecycled = onRecycled;

            mMaxRetained = options.MaxRetained;
            int capacity = GetInitialCacheCapacity(options);
            mCacheStack = new(capacity);

            WarmUp(options.InitialCount);
#if UNITY_EDITOR || (GODOT && TOOLS)
            PoolEditorHook.RegisterPool(this, typeof(T).Name, mMaxRetained, CurCount);
#endif
        }

        /// <summary>
        /// 获取当前池内可直接复用的对象数量。
        /// </summary>
        public int CurCount
        {
            get
            {
                lock (mSyncRoot)
                {
                    return mCacheStack.Count;
                }
            }
        }

        /// <summary>
        /// 从缓存分配对象或通过工厂创建对象，然后执行借出生命周期。
        /// </summary>
        /// <returns>完成借出生命周期的可用对象。</returns>
        public T Allocate()
        {
            T item = TakeOrCreate();
            try
            {
                mOnAllocated?.Invoke(item);
            }
            catch
            {
                DisposeItemAfterFailedAllocation(item);
                throw;
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            if (PoolEditorHook.IsTrackingEnabled)
            {
                PoolEditorHook.TrackAllocate(this, item, CurCount);
            }
#endif

            return item;
        }

        /// <summary>
        /// 回收对象；拒绝 null 和重复回收，容量满时完成清理和释放但不缓存。
        /// </summary>
        /// <param name="obj">需要归还的对象。</param>
        /// <returns>对象进入缓存时返回 true；无效、重复或容量满时返回 false。</returns>
        public bool Recycle(T obj)
        {
            if (obj == null || !TryReserveRecycle(obj))
            {
                return false;
            }

            try
            {
                mOnRecycled?.Invoke(obj);
            }
            catch
            {
                ReleaseRecycleReservation(obj);
                throw;
            }

            bool cached = TryCacheReservedObject(obj);
            if (!cached)
            {
                DisposeItem(obj);
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            TrackRecycleIfNeeded(obj);
#endif
            return cached;
        }

        /// <summary>
        /// 清空并释放当前缓存对象，池实例保持可继续使用。
        /// </summary>
        public void Clear()
        {
            T[] cachedItems;
            lock (mSyncRoot)
            {
                ThrowIfDisposed();
                cachedItems = mCacheStack.ToArray();
                mCacheStack.Clear();
                // 仅移除真正出栈的对象，保留在途回收的重复回收防护标记。
                for (var index = 0; index < cachedItems.Length; index++)
                {
                    mCachedObjects.Remove(cachedItems[index]);
                }
            }

            try
            {
                DisposeItems(cachedItems);
            }
            finally
            {
#if UNITY_EDITOR || (GODOT && TOOLS)
                if (PoolEditorHook.IsTrackingEnabled)
                {
                    PoolEditorHook.UpdatePoolCounts(this, CurCount);
                }
#endif
            }
        }

        /// <summary>
        /// 释放缓存对象并注销诊断信息；释放后的池不能再次使用。
        /// </summary>
        public void Dispose()
        {
            T[] cachedItems;
            lock (mSyncRoot)
            {
                if (mDisposed)
                {
                    return;
                }

                mDisposed = true;
                cachedItems = mCacheStack.ToArray();
                mCacheStack.Clear();
                mCachedObjects.Clear();
            }

            try
            {
                DisposeItems(cachedItems);
            }
            finally
            {
#if UNITY_EDITOR || (GODOT && TOOLS)
                PoolEditorHook.UnregisterPool(this);
#endif
                GC.SuppressFinalize(this);
            }
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 为 PoolDebugger 提供 object 形式的强制归还入口。
        /// </summary>
        /// <param name="obj">需要归还的对象。</param>
        /// <returns>对象类型匹配且归还成功时返回 true。</returns>
        bool IPoolDebugReturn.TryRecycleObject(object obj)
        {
            return obj is T typedObj && Recycle(typedObj);
        }

        /// <summary>
        /// 在 Workbench 或显式诊断读取时有界复制缓存对象，不在借还路径创建快照数组。
        /// </summary>
        /// <param name="result">接收缓存对象的诊断列表。</param>
        /// <param name="maxCount">最多复制的对象数量；负数视为零。</param>
        /// <returns>当前缓存对象总数。</returns>
        int IPoolDebugSnapshot.CopyInactiveObjects(List<object> result, int maxCount)
        {
            if (result == null)
            {
                return 0;
            }

            result.Clear();
            int limit = maxCount > 0 ? maxCount : 0;
            lock (mSyncRoot)
            {
                int totalCount = mCacheStack.Count;
                if (limit == 0)
                {
                    return totalCount;
                }

                foreach (T item in mCacheStack)
                {
                    if (result.Count >= limit)
                    {
                        break;
                    }

                    result.Add(item);
                }

                return totalCount;
            }
        }
#endif

        /// <summary>
        /// 预创建指定数量的对象并直接放入缓存，不触发业务生命周期。
        /// </summary>
        /// <param name="initialCount">预创建数量。</param>
        private void WarmUp(int initialCount)
        {
            try
            {
                for (var index = 0; index < initialCount; index++)
                {
                    T item = CreateItem();
                    mCacheStack.Push(item);
                    mCachedObjects.Add(item);
                }
            }
            catch
            {
                T[] createdItems = mCacheStack.ToArray();
                mCacheStack.Clear();
                mCachedObjects.Clear();
                DisposeItemsSafely(createdItems);
                throw;
            }
        }

        /// <summary>
        /// 从缓存取出对象；缓存为空时在锁外调用用户工厂创建对象。
        /// </summary>
        /// <returns>尚未执行借出生命周期的对象。</returns>
        private T TakeOrCreate()
        {
            lock (mSyncRoot)
            {
                ThrowIfDisposed();
                if (mCacheStack.Count > 0)
                {
                    T cached = mCacheStack.Pop();
                    mCachedObjects.Remove(cached);
                    return cached;
                }
            }

            return CreateItem();
        }

        /// <summary>
        /// 调用对象工厂，并拒绝返回 null 的无效工厂实现。
        /// </summary>
        /// <returns>新创建的非空对象。</returns>
        private T CreateItem()
        {
            T item = mFactory();
            if (item == null)
            {
                throw new InvalidOperationException("Pool factory returned null for " + typeof(T).FullName + ".");
            }

            return item;
        }

        /// <summary>
        /// 标记对象正在回收，防止回收回调期间发生重复归还。
        /// </summary>
        /// <param name="obj">需要标记的对象。</param>
        /// <returns>对象此前不在缓存或回收流程中时返回 true。</returns>
        private bool TryReserveRecycle(T obj)
        {
            lock (mSyncRoot)
            {
                ThrowIfDisposed();
                return mCachedObjects.Add(obj);
            }
        }

        /// <summary>
        /// 回收回调失败时撤销对象的回收保留标记。
        /// </summary>
        /// <param name="obj">需要撤销标记的对象。</param>
        private void ReleaseRecycleReservation(T obj)
        {
            lock (mSyncRoot)
            {
                mCachedObjects.Remove(obj);
            }
        }

        /// <summary>
        /// 在容量允许时缓存已完成回收生命周期的对象。
        /// </summary>
        /// <param name="obj">已保留并完成清理的对象。</param>
        /// <returns>对象进入缓存时返回 true。</returns>
        private bool TryCacheReservedObject(T obj)
        {
            lock (mSyncRoot)
            {
                if (mDisposed || IsCacheFull())
                {
                    mCachedObjects.Remove(obj);
                    return false;
                }

                mCacheStack.Push(obj);
                return true;
            }
        }

        /// <summary>
        /// 判断当前缓存是否达到配置上限；负数上限表示无限制。
        /// </summary>
        /// <returns>缓存不再接收对象时返回 true。</returns>
        private bool IsCacheFull()
        {
            return mMaxRetained >= 0 && mCacheStack.Count >= mMaxRetained;
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 在诊断开启时记录对象归还，并让诊断器依据真实缓存数量更新统计。
        /// </summary>
        /// <param name="obj">已完成回收的对象。</param>
        private void TrackRecycleIfNeeded(T obj)
        {
            if (!PoolEditorHook.IsTrackingEnabled)
            {
                return;
            }

            PoolEditorHook.TrackRecycle(this, obj, CurCount);
        }
#endif

        /// <summary>
        /// 根据预热数量和缓存上限选择初始 Stack 容量，零保留池不预分配无用槽位。
        /// </summary>
        /// <param name="options">已经完成构造期校验的容量配置。</param>
        /// <returns>用于 Stack 的初始容量。</returns>
        private static int GetInitialCacheCapacity(PoolOptions options)
        {
            if (options.InitialCount > 0)
            {
                return options.InitialCount;
            }

            if (options.MaxRetained == 0)
            {
                return 0;
            }

            return options.MaxRetained > 0
                ? Math.Min(DEFAULT_CAPACITY, options.MaxRetained)
                : DEFAULT_CAPACITY;
        }

        /// <summary>
        /// 释放一组已经离开缓存的对象。
        /// </summary>
        /// <param name="items">需要释放的对象数组。</param>
        private static void DisposeItems(T[] items)
        {
            List<Exception> errors = null;
            for (var index = 0; index < items.Length; index++)
            {
                try
                {
                    DisposeItem(items[index]);
                }
                catch (Exception exception)
                {
                    errors ??= new List<Exception>();
                    errors.Add(exception);
                }
            }

            if (errors == null)
            {
                return;
            }

            if (errors.Count == 1)
            {
                throw errors[0];
            }

            throw new AggregateException("One or more pooled objects failed to dispose.", errors);
        }

        /// <summary>
        /// 预热失败时尽力释放已经创建的对象，避免清理异常覆盖工厂原始异常。
        /// </summary>
        /// <param name="items">预热阶段已经创建的对象。</param>
        private static void DisposeItemsSafely(T[] items)
        {
            for (var index = 0; index < items.Length; index++)
            {
                try
                {
                    DisposeItem(items[index]);
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[PoolKit] Warm-up cleanup failed: " + exception.Message);
                }
            }
        }

        /// <summary>
        /// 对实现 IDisposable 的对象执行资源释放。
        /// </summary>
        /// <param name="item">需要按约定释放的对象。</param>
        private static void DisposeItem(T item)
        {
            if (item is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// 借出回调失败时释放尚未交给调用方的对象，且不允许清理异常覆盖业务异常。
        /// </summary>
        /// <param name="item">借出流程中已经取得但尚未返回的对象。</param>
        private static void DisposeItemAfterFailedAllocation(T item)
        {
            try
            {
                DisposeItem(item);
            }
            catch (Exception cleanupException)
            {
                System.Diagnostics.Debug.WriteLine(cleanupException);
            }
        }

        /// <summary>
        /// 池已释放时拒绝继续使用，避免静默操作失去所有权的缓存。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (mDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}
