namespace YokiFrame
{
    /// <summary>表示 SceneKit 的场景加载模式。</summary>
    public enum SceneLoadMode
    {
        /// <summary>替换当前场景集合。</summary>
        Single = 0,

        /// <summary>保留当前场景并叠加新场景。</summary>
        Additive = 1
    }
}
