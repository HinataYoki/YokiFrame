using System;

namespace YokiFrame
{
    /// <summary>
    /// 面向 IPoolable 类型的类型安全对象池。
    /// 通过 SingletonKit 提供单例访问，并强制执行最大缓存容量。
    /// </summary>
    public class SafePoolKit<T> : PoolKit<T>, ISingleton where T : IPoolable, new()
    {
        public static SafePoolKit<T> Instance => SingletonKit<SafePoolKit<T>>.Instance;

        private int mMaxCount = 5;
        public int MaxCacheCount
        {
            get
            {
                lock (mSyncRoot)
                {
                    return mMaxCount;
                }
            }
            set
            {
                lock (mSyncRoot)
                {
                    mMaxCount = value;
                    if (mMaxCount > 0 && mMaxCount < mCacheStack.Count)
                    {
                        int removeCount = mCacheStack.Count - mMaxCount;
                        while (removeCount > 0)
                        {
                            var item = mCacheStack.Pop();
                            if (item is IDisposable disposable)
                                disposable.Dispose();
                            --removeCount;
                        }
                    }
                }
#if UNITY_EDITOR || GODOT
                PoolDebugger.UpdateMaxCacheCount(this, mMaxCount);
                SyncDebuggerInactiveObjects();
#endif
            }
        }

        protected SafePoolKit() : base(20)
        {
            mFactory = new DefaultObjectFactory<T>();
#if UNITY_EDITOR || GODOT
            PoolDebugger.RegisterPool(this, typeof(T).Name, mMaxCount);
            UpdateDebuggerTotalCount();
            SyncDebuggerInactiveObjects();
#endif
        }

        void ISingleton.OnSingletonInit() => Init();

#if UNITY_EDITOR || GODOT
        private void UpdateDebuggerTotalCount()
        {
            var activeCount = PoolDebugger.GetActiveCount(this);
            PoolDebugger.UpdateTotalCount(this, CurCount + activeCount);
            SyncDebuggerInactiveObjects();
        }
#endif

        public void Init(int initCount = 0, int maxCount = 20, Func<T> factoryMethod = null)
        {
            if (factoryMethod is not null)
                SetFactoryMethod(factoryMethod);

            MaxCacheCount = maxCount;
            if (CurCount < initCount)
            {
                for (var i = CurCount; i < initCount; ++i)
                    Recycle(CreateObject());
            }
        }

        public override T Allocate()
        {
            var result = base.Allocate();
            result.IsRecycled = false;
#if UNITY_EDITOR || GODOT
            if (PoolDebugger.EnableTracking)
            {
                PoolDebugger.TrackAllocate(this, result);
                UpdateDebuggerTotalCount();
            }
#endif
            return result;
        }

        public override bool Recycle(T obj)
        {
            if (obj == null || obj.IsRecycled)
                return false;

            var consumed = false;
            var cached = false;

            lock (mSyncRoot)
            {
                if (obj.IsRecycled)
                    return false;

                if (mCacheStack.Count >= mMaxCount)
                {
                    obj.IsRecycled = true;
                    obj.OnRecycled();
                }
                else
                {
                    obj.IsRecycled = true;
                    obj.OnRecycled();
                    mCacheStack.Push(obj);
                    cached = true;
                }

                consumed = true;
            }
#if UNITY_EDITOR || GODOT
            if (PoolDebugger.EnableTracking && consumed)
            {
                if (PoolDebugger.IsObjectTracked(obj))
                    PoolDebugger.TrackRecycle(this, obj);
                UpdateDebuggerTotalCount();
            }
#endif
            return cached;
        }
    }
}
