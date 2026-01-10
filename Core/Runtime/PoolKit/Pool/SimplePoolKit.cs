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
            PoolDebugger.RegisterPool(this, typeof(T).Name);
            PoolDebugger.UpdateTotalCount(this, mCacheStack.Count);
#endif
        }

        public override T Allocate()
        {
            var result = base.Allocate();
#if UNITY_EDITOR
            PoolDebugger.TrackAllocate(this, result);
            PoolDebugger.UpdateTotalCount(this, mCacheStack.Count);
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
            PoolDebugger.UpdateTotalCount(this, mCacheStack.Count);
#endif
            return true;
        }
    }
}