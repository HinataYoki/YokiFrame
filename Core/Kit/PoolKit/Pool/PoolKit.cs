using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public abstract class PoolKit<T> : IPool<T>
    {
        /// <summary>
        /// 当前池内对象数量
        /// </summary>
        public int CurCount => mCacheStack.Count;
        /// <summary>
        /// 池缓存
        /// </summary>
        protected readonly Stack<T> mCacheStack = new();
        /// <summary>
        /// 池对象创建工厂
        /// </summary>
        protected IObjectFactory<T> mFactory;

        /// <summary>
        /// 设置工厂
        /// </summary>
        /// <param name="factory"></param>
        public void SetObjectFactory(IObjectFactory<T> factory) => mFactory = factory;
        /// <summary>
        /// 设置构造函数
        /// </summary>
        public void SetFactoryMethod(Func<T> factoryMethod) => mFactory = new CustomObjectFactory<T>(factoryMethod);

        public virtual T Allocate()
        {
            return mCacheStack.Count == 0
                ? mFactory.Create()
                : mCacheStack.Pop();
        }

        public abstract bool Recycle(T obj);
    }
}