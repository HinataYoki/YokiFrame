namespace YokiFrame
{
    /// <summary>
    /// 引擎对象抽象接口，替代 Unity GameObject / Godot Node
    /// </summary>
    public interface IEngineObject
    {
        /// <summary>
        /// 获取或设置引擎对象名称。
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// 获取或设置引擎对象激活状态。
        /// </summary>
        bool IsActive { get; set; }

        /// <summary>
        /// 获取或设置引擎对象位置。
        /// </summary>
        YokiVector3 Position { get; set; }

        /// <summary>
        /// 获取指定类型的组件或附加对象。
        /// </summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <returns>找到的组件；不存在时返回 null。</returns>
        T GetComponent<T>() where T : class;

        /// <summary>
        /// 销毁当前引擎对象。
        /// </summary>
        void Destroy();

        /// <summary>
        /// 实例化指定预制对象。
        /// </summary>
        /// <param name="prefab">要实例化的预制对象。</param>
        /// <returns>实例化后的引擎对象；失败时返回 null。</returns>
        IEngineObject Instantiate(IEngineObject prefab);
    }
}
