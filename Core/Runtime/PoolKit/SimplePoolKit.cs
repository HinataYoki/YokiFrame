using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 简单对象池，支持配置工厂方法和重置动作。
    /// </summary>
    public class SimplePoolKit<T> : PoolKit<T>
    {
        private readonly Action<T> mResetMethod;
        private readonly HashSet<T> mCachedObjects = new HashSet<T>(PoolReferenceEqualityComparer<T>.Instance);

        public SimplePoolKit(Func<T> factoryMethod, Action<T> resetMethod = null, int initCount = 0)
            : base(initCount > 0 ? initCount : 16)
        {
            mFactory = new CustomObjectFactory<T>(factoryMethod);
            mResetMethod = resetMethod;

            for (var i = 0; i < initCount; i++)
            {
                var obj = mFactory.Create();
                PushCachedObject(obj);
                mCachedObjects.Add(obj);
            }

#if UNITY_EDITOR || GODOT
            PoolDebugger.RegisterPool(this, typeof(T).Name, -1);
            PoolDebugger.UpdateTotalCount(this, CurCount);
            SyncDebuggerInactiveObjects();
#endif
        }

        public override T Allocate()
        {
            var result = base.Allocate();
            lock (mSyncRoot)
                mCachedObjects.Remove(result);
#if UNITY_EDITOR || GODOT
            if (PoolDebugger.EnableTracking)
            {
                PoolDebugger.TrackAllocate(this, result);
                var activeCount = PoolDebugger.GetActiveCount(this);
                PoolDebugger.UpdateTotalCount(this, CurCount + activeCount);
                SyncDebuggerInactiveObjects();
            }
#endif
            return result;
        }

        public override bool Recycle(T obj)
        {
            if (ReferenceEquals(obj, null))
                return false;

            lock (mSyncRoot)
            {
                if (!mCachedObjects.Add(obj))
                    return false;
            }

            try
            {
                mResetMethod?.Invoke(obj);

                lock (mSyncRoot)
                    mCacheStack.Push(obj);
            }
            catch
            {
                lock (mSyncRoot)
                    mCachedObjects.Remove(obj);
                throw;
            }
#if UNITY_EDITOR || GODOT
            if (PoolDebugger.EnableTracking)
            {
                PoolDebugger.TrackRecycle(this, obj);
                var activeCount = PoolDebugger.GetActiveCount(this);
                PoolDebugger.UpdateTotalCount(this, CurCount + activeCount);
                SyncDebuggerInactiveObjects();
            }
#endif
            return true;
        }

        private sealed class PoolReferenceEqualityComparer<TValue> : IEqualityComparer<TValue>
        {
            public static readonly PoolReferenceEqualityComparer<TValue> Instance = new PoolReferenceEqualityComparer<TValue>();

            public bool Equals(TValue x, TValue y)
            {
                if (!typeof(TValue).IsValueType)
                    return ReferenceEquals(x, y);

                return EqualityComparer<TValue>.Default.Equals(x, y);
            }

            public int GetHashCode(TValue obj)
            {
                if (!typeof(TValue).IsValueType)
                    return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);

                return EqualityComparer<TValue>.Default.GetHashCode(obj);
            }
        }
    }
}
