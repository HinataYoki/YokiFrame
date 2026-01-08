namespace YokiFrame
{
    /// <summary>
    /// 场景加载器池接口，管理加载器的分配和回收
    /// </summary>
    public interface ISceneLoaderPool
    {
        /// <summary>
        /// 从池中分配一个加载器
        /// </summary>
        /// <returns>场景加载器实例</returns>
        ISceneLoader Allocate();

        /// <summary>
        /// 回收加载器到池中
        /// </summary>
        /// <param name="loader">要回收的加载器</param>
        void Recycle(ISceneLoader loader);
    }
}
