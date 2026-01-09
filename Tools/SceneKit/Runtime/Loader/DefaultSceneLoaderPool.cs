using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 默认场景加载器池，使用栈实现对象池
    /// </summary>
    public class DefaultSceneLoaderPool : ISceneLoaderPool
    {
        private readonly Stack<ISceneLoader> mPool = new(4);

        /// <summary>
        /// 从池中分配一个加载器
        /// </summary>
        public ISceneLoader Allocate()
        {
            if (mPool.Count > 0)
            {
                return mPool.Pop();
            }
            return new DefaultSceneLoader(this);
        }

        /// <summary>
        /// 回收加载器到池中
        /// </summary>
        public void Recycle(ISceneLoader loader)
        {
            if (loader != null)
            {
                mPool.Push(loader);
            }
        }
    }
}
