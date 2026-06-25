using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public abstract class PoolKit<T> : IPool<T>, IPoolDebugReturn
    {
        private const int DEFAULT_CAPACITY = 16;

        /// <summary>对象池中的当前对象数量。</summary>
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

        /// <summary>对象池缓存栈。</summary>
        protected readonly Stack<T> mCacheStack;

        /// <summary>保护缓存栈和对象池配置的同步锁。</summary>
        protected readonly object mSyncRoot = new object();

        /// <summary>对象工厂。</summary>
        protected IObjectFactory<T> mFactory;

        protected PoolKit(int initialCapacity = DEFAULT_CAPACITY)
        {
            mCacheStack = new Stack<T>(initialCapacity);
        }

        /// <summary>设置对象工厂。</summary>
        public void SetObjectFactory(IObjectFactory<T> factory)
        {
            lock (mSyncRoot)
            {
                mFactory = factory;
            }
        }

        /// <summary>设置工厂方法。</summary>
        public void SetFactoryMethod(Func<T> factoryMethod)
        {
            lock (mSyncRoot)
            {
                mFactory = new CustomObjectFactory<T>(factoryMethod);
            }
        }

        public virtual T Allocate()
        {
            IObjectFactory<T> factory;
            lock (mSyncRoot)
            {
                if (mCacheStack.Count > 0)
                    return mCacheStack.Pop();

                factory = mFactory;
            }

            if (factory == null)
                throw new InvalidOperationException("Pool object factory is not configured.");

            return factory.Create();
        }

        protected void PushCachedObject(T obj)
        {
            lock (mSyncRoot)
            {
                mCacheStack.Push(obj);
            }
        }

        protected T CreateObject()
        {
            IObjectFactory<T> factory;
            lock (mSyncRoot)
            {
                factory = mFactory;
            }

            if (factory == null)
                throw new InvalidOperationException("Pool object factory is not configured.");

            return factory.Create();
        }

        protected void SyncDebuggerInactiveObjects()
        {
#if UNITY_EDITOR || GODOT
            if (!PoolDebugger.EnableTracking)
                return;

            object[] items;
            lock (mSyncRoot)
            {
                items = new object[mCacheStack.Count];
                var index = 0;
                foreach (var item in mCacheStack)
                    items[index++] = item;
            }

            PoolDebugger.UpdateInactiveObjects(this, items);
#endif
        }

        public abstract bool Recycle(T obj);

        bool IPoolDebugReturn.TryRecycleObject(object obj)
        {
            if (obj is T typedObj)
                return Recycle(typedObj);

            return false;
        }
    }
}
