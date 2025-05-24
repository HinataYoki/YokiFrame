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
                        mCacheStack.Pop();
                        --removeCount;
                    }
                }
            }
        }

        protected SafePoolKit() => mFactory = new DefaultObjectFactory<T>();
        void ISingleton.OnSingletonInit() => Init();


        /// <summary>
        /// 初始化对象池
        /// </summary>
        /// <param name="initCount">初始对象数量</param>
        /// <param name="maxCount">最大对象数量</param>
        /// <param name="factoryMethod">对象创建方法</param>
        public void Init(int initCount = 0, int maxCount = 20, Func<T> factoryMethod = null)
        {
            if (factoryMethod != null)
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
            return result;
        }

        public override bool Recycle(T obj)
        {
            if (obj == null || obj.IsRecycled)
            {
                return false;
            }
            //最大空间足够才入栈
            if (mCacheStack.Count >= mMaxCount)
            {
                obj.OnRecycled();
                return false;
            }
            else
            {
                obj.IsRecycled = true;
                obj.OnRecycled();
                mCacheStack.Push(obj);
                return true;
            }
        }
    }
}