using System;

namespace YokiFrame
{
    /// <summary>
    /// 类型安全对象池
    /// </summary>
    public class SafePoolKit<T> : PoolKit<T>, ISingleton where T : IPoolable, new()
    {
        public static SafePoolKit<T> Instance => SingletonKit<SafePoolKit<T>>.Instance;

        private int mMaxCount = 5;
        public int MaxCacheCount
        {
            get => mMaxCount;
            set
            {
                mMaxCount = value;
                if (mMaxCount > 0 && mMaxCount < mCacheStack.Count)
                {
                    int removeCount = mCacheStack.Count - mMaxCount;
                    while (removeCount > 0)
                    {
                        var item = mCacheStack.Pop();
                        if (item is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                        --removeCount;
                    }
                }
#if UNITY_EDITOR
                UpdateDebuggerTotalCount();
                PoolDebugger.UpdateMaxCacheCount(this, mMaxCount);
#endif
            }
        }

        protected SafePoolKit() : base(20)
        {
            mFactory = new DefaultObjectFactory<T>();
#if UNITY_EDITOR
            PoolDebugger.RegisterPool(this, typeof(T).Name, mMaxCount);
#endif
        }
        
        void ISingleton.OnSingletonInit() => Init();
        
#if UNITY_EDITOR
        /// <summary>
        /// 更新调试器总容量（池内 + 借出）
        /// </summary>
        private void UpdateDebuggerTotalCount()
        {
            // TotalCount = 池内对象数 + 借出对象数
            var activeCount = PoolDebugger.GetActiveCount(this);
            PoolDebugger.UpdateTotalCount(this, CurCount + activeCount);
        }
#endif

        /// <summary>
        /// 初始化对象池
        /// </summary>
        /// <param name="initCount">初始对象数量</param>
        /// <param name="maxCount">最大对象数量</param>
        /// <param name="factoryMethod">对象创建方法</param>
        public void Init(int initCount = 0, int maxCount = 20, Func<T> factoryMethod = null)
        {
            if (factoryMethod is not null)
            {
                mFactory = new CustomObjectFactory<T>(factoryMethod);
            }

            MaxCacheCount = maxCount;
            if (CurCount < initCount)
            {
                for (var i = CurCount; i < initCount; ++i)
                {
                    Recycle(mFactory.Create());
                }
            }
        }

        public override T Allocate()
        {
            var result = base.Allocate();
            result.IsRecycled = false;
#if UNITY_EDITOR
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
            {
                return false;
            }
            
#if UNITY_EDITOR
            if (PoolDebugger.EnableTracking)
            {
                PoolDebugger.TrackRecycle(this, obj);
            }
#endif
            
            // 最大空间足够才入栈
            if (mCacheStack.Count >= mMaxCount)
            {
                obj.OnRecycled();
#if UNITY_EDITOR
                if (PoolDebugger.EnableTracking)
                {
                    UpdateDebuggerTotalCount();
                }
#endif
                return false;
            }
            else
            {
                obj.IsRecycled = true;
                obj.OnRecycled();
                mCacheStack.Push(obj);
#if UNITY_EDITOR
                if (PoolDebugger.EnableTracking)
                {
                    UpdateDebuggerTotalCount();
                }
#endif
                return true;
            }
        }
    }
}