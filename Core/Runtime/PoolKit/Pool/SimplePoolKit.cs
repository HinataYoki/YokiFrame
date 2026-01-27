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
            if (PoolDebugger.EnableTracking)
            {
                UnityEngine.Debug.Log($"[SimplePoolKit] Allocate: Type={typeof(T).Name}, HashCode={result?.GetHashCode()}, PoolCount={mCacheStack.Count}");
                PoolDebugger.TrackAllocate(this, result);
                var activeCount = PoolDebugger.GetActiveCount(this);
                PoolDebugger.UpdateTotalCount(this, mCacheStack.Count + activeCount);
                UnityEngine.Debug.Log($"[SimplePoolKit] Allocate 完成: ActiveCount={activeCount}, PoolCount={mCacheStack.Count}");
            }
#endif
            return result;
        }

        public override bool Recycle(T obj)
        {
#if UNITY_EDITOR
            if (PoolDebugger.EnableTracking)
            {
                UnityEngine.Debug.Log($"[SimplePoolKit] Recycle 调用: Type={typeof(T).Name}, Obj={obj?.GetType().Name}, HashCode={obj?.GetHashCode()}");
                PoolDebugger.TrackRecycle(this, obj);
            }
#endif
            mResetMethod?.Invoke(obj);
            mCacheStack.Push(obj);
#if UNITY_EDITOR
            if (PoolDebugger.EnableTracking)
            {
                var activeCount = PoolDebugger.GetActiveCount(this);
                PoolDebugger.UpdateTotalCount(this, mCacheStack.Count + activeCount);
                UnityEngine.Debug.Log($"[SimplePoolKit] Recycle 完成: ActiveCount={activeCount}, PoolCount={mCacheStack.Count}");
            }
#endif
            return true;
        }
    }
}