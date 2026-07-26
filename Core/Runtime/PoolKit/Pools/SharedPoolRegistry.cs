using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 全局共享对象池注册表，按对象类型管理唯一池实例和确定性生命周期。
    /// </summary>
    public sealed class SharedPoolRegistry
    {
        private readonly Dictionary<Type, IDisposable> mPools = new();
        private readonly object mSyncRoot = new();

        /// <summary>
        /// 获取当前已经注册的共享对象池数量。
        /// </summary>
        public int Count
        {
            get
            {
                lock (mSyncRoot)
                {
                    return mPools.Count;
                }
            }
        }

        /// <summary>
        /// 使用显式工厂和生命周期委托注册普通类型的全局共享池。
        /// </summary>
        /// <typeparam name="T">引用类型的池化对象。</typeparam>
        /// <param name="factory">缓存为空时创建对象的委托。</param>
        /// <param name="onAllocated">对象借出后的可选生命周期委托。</param>
        /// <param name="onRecycled">对象归还前的可选生命周期委托。</param>
        /// <param name="options">预热和缓存容量配置；省略时使用默认配置。</param>
        /// <returns>完成注册的全局对象池。</returns>
        public ObjectPool<T> Register<T>(
            Func<T> factory,
            Action<T> onAllocated = null,
            Action<T> onRecycled = null,
            PoolOptions options = default) where T : class
        {
            ObjectPool<T> pool = PoolKit.Create(factory, onAllocated, onRecycled, options);
            return AddPool(pool);
        }

        /// <summary>
        /// 按 IPoolable 约定注册全局共享池，并在建池时绑定标准生命周期。
        /// </summary>
        /// <typeparam name="T">具备公开无参构造函数的标准池化对象。</typeparam>
        /// <param name="options">预热和缓存容量配置；省略时使用默认配置。</param>
        /// <returns>完成注册的全局对象池。</returns>
        public ObjectPool<T> Register<T>(PoolOptions options = default)
            where T : class, IPoolable, new()
        {
            ObjectPool<T> pool = PoolKit.Create<T>(options);
            return AddPool(pool);
        }

        /// <summary>
        /// 获取指定对象类型已经注册的全局共享池。
        /// </summary>
        /// <typeparam name="T">引用类型的池化对象。</typeparam>
        /// <returns>指定类型的全局对象池。</returns>
        public ObjectPool<T> Get<T>() where T : class
        {
            lock (mSyncRoot)
            {
                if (mPools.TryGetValue(typeof(T), out IDisposable registered))
                {
                    return (ObjectPool<T>)registered;
                }
            }

            throw new InvalidOperationException(
                "Shared pool is not registered for " + typeof(T).FullName + ".");
        }

        /// <summary>
        /// 尝试获取指定对象类型已经注册的全局共享池。
        /// </summary>
        /// <typeparam name="T">引用类型的池化对象。</typeparam>
        /// <param name="pool">已注册的对象池；不存在时为 null。</param>
        /// <returns>已找到共享池时返回 true。</returns>
        public bool TryGet<T>(out ObjectPool<T> pool) where T : class
        {
            lock (mSyncRoot)
            {
                if (mPools.TryGetValue(typeof(T), out IDisposable registered))
                {
                    pool = (ObjectPool<T>)registered;
                    return true;
                }
            }

            pool = null;
            return false;
        }

        /// <summary>
        /// 移除并释放指定对象类型的全局共享池。
        /// </summary>
        /// <typeparam name="T">引用类型的池化对象。</typeparam>
        /// <returns>找到并移除共享池时返回 true。</returns>
        public bool Remove<T>() where T : class
        {
            IDisposable pool;
            lock (mSyncRoot)
            {
                if (!mPools.TryGetValue(typeof(T), out pool))
                {
                    return false;
                }

                mPools.Remove(typeof(T));
            }

            pool.Dispose();
            return true;
        }

        /// <summary>
        /// 移除并释放全部全局共享池，供宿主卸载和测试隔离使用；单个池释放失败不影响其余池，异常聚合后统一抛出。
        /// </summary>
        public void Clear()
        {
            IDisposable[] pools;
            lock (mSyncRoot)
            {
                pools = new IDisposable[mPools.Count];
                mPools.Values.CopyTo(pools, 0);
                mPools.Clear();
            }

            List<Exception> errors = null;
            for (var index = 0; index < pools.Length; index++)
            {
                try
                {
                    pools[index].Dispose();
                }
                catch (Exception exception)
                {
                    errors ??= new List<Exception>();
                    errors.Add(exception);
                }
            }

            if (errors != null)
            {
                if (errors.Count == 1)
                {
                    throw errors[0];
                }

                throw new AggregateException("One or more shared pools failed to dispose.", errors);
            }
        }

        /// <summary>
        /// 把新池加入注册表；并发或重复注册时释放新池并报告配置冲突。
        /// </summary>
        /// <typeparam name="T">引用类型的池化对象。</typeparam>
        /// <param name="pool">准备注册的新池。</param>
        /// <returns>注册成功的同一池实例。</returns>
        private ObjectPool<T> AddPool<T>(ObjectPool<T> pool) where T : class
        {
            lock (mSyncRoot)
            {
                if (!mPools.ContainsKey(typeof(T)))
                {
                    mPools.Add(typeof(T), pool);
                    return pool;
                }
            }

            pool.Dispose();
            throw new InvalidOperationException(
                "Shared pool is already registered for " + typeof(T).FullName + ".");
        }
    }
}
