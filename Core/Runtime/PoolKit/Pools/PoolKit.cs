using System;

namespace YokiFrame
{
    /// <summary>
    /// 对象池统一门面，负责创建局部池并提供全局共享池注册表。
    /// </summary>
    public static class PoolKit
    {
        /// <summary>
        /// 获取全局共享对象池注册表。
        /// </summary>
        public static SharedPoolRegistry Shared { get; } = new();

        /// <summary>
        /// 使用显式工厂和生命周期委托创建局部对象池。
        /// </summary>
        /// <typeparam name="T">引用类型的池化对象。</typeparam>
        /// <param name="factory">缓存为空时创建对象的委托。</param>
        /// <param name="onAllocated">对象借出后的可选生命周期委托。</param>
        /// <param name="onRecycled">对象归还前的可选生命周期委托。</param>
        /// <param name="options">预热和缓存容量配置；省略时使用默认配置。</param>
        /// <returns>调用方独占所有权的对象池。</returns>
        public static ObjectPool<T> Create<T>(
            Func<T> factory,
            Action<T> onAllocated = null,
            Action<T> onRecycled = null,
            PoolOptions options = default) where T : class
        {
            return new ObjectPool<T>(factory, onAllocated, onRecycled, options);
        }

        /// <summary>
        /// 按 IPoolable 约定创建局部对象池，并在建池时绑定标准生命周期。
        /// </summary>
        /// <typeparam name="T">具备公开无参构造函数的标准池化对象。</typeparam>
        /// <param name="options">预热和缓存容量配置；省略时使用默认配置。</param>
        /// <returns>调用方独占所有权的对象池。</returns>
        public static ObjectPool<T> Create<T>(PoolOptions options = default)
            where T : class, IPoolable, new()
        {
            return new ObjectPool<T>(
                static () => new T(),
                static item => item.OnAllocated(),
                static item => item.OnRecycled(),
                options);
        }
    }
}
