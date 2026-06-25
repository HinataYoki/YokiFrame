namespace YokiFrame
{
    /// <summary>
    /// 表示场景加载模式。
    /// </summary>
    public enum SceneLoadMode
    {
        /// <summary>
        /// 单场景模式，加载新场景前清理已登记场景。
        /// </summary>
        Single = 0,

        /// <summary>
        /// 叠加模式，保留已登记场景。
        /// </summary>
        Additive = 1
    }
}
