using System;

namespace YokiFrame
{
    /// <summary>
    /// 简易对象池
    /// </summary>
    public class SimplePoolKit<T> : PoolKit<T>
    {
        private readonly Action<T> mResetMethod;

        public SimplePoolKit(Func<T> factoryMethod, Action<T> resetMethod = null, int initCount = 0) 
            : base(initCount > 0 ? initCount : 16)
        {
            mFactory = new CustomObjectFactory<T>(factoryMethod);
            mResetMethod = resetMethod;

            for (var i = 0; i < initCount; i++)
            {
                mCacheStack.Push(mFactory.Create());
            }
            
#if UNITY_EDITOR
            PoolDebugger.RegisterPool(this, typeof(T).Name, -1); // SimplePoolKit 无容量限制
            // 初始化时 TotalCount = 池内对象数（活跃数为 0）
            PoolDebugger.UpdateTotalCount(this, mCacheStack.Count);
#endif
        }

        public override T Allocate()
        {
            var result = base.Allocate();
#if UNITY_EDITOR
            PoolDebugger.TrackAllocate(this, result);
            // TotalCount = 池内对象数 + 借出对象数
            var activeCount = PoolDebugger.GetActiveCount(this);
            PoolDebugger.UpdateTotalCount(this, mCacheStack.Count + activeCount);
#endif
            return result;
        }

        public override bool Recycle(T obj)
        {
#if UNITY_EDITOR
            PoolDebugger.TrackRecycle(this, obj);
#endif
            mResetMethod?.Invoke(obj);
            mCacheStack.Push(obj);
#if UNITY_EDITOR
            // TotalCount = 池内对象数 + 借出对象数
            var activeCount = PoolDebugger.GetActiveCount(this);
            PoolDebugger.UpdateTotalCount(this, mCacheStack.Count + activeCount);
#endif
            return true;
        }
    }
}