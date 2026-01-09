namespace YokiFrame
{
    /// <summary>
    /// 场景加载模式，对应 Unity 的 LoadSceneMode
    /// </summary>
    public enum SceneLoadMode
    {
        /// <summary>
        /// 单场景模式，加载新场景时卸载所有已加载场景
        /// </summary>
        Single = 0,
        
        /// <summary>
        /// 叠加模式，保留已加载场景
        /// </summary>
        Additive = 1
    }
}
