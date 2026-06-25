namespace YokiFrame
{
    /// <summary>
    /// 表示场景加载完成结果。
    /// </summary>
    public struct SceneLoadResult
    {
        /// <summary>
        /// 已加载场景句柄。
        /// </summary>
        public readonly SceneHandle Scene;

        /// <summary>
        /// 创建场景加载结果。
        /// </summary>
        /// <param name="scene">已加载场景句柄。</param>
        public SceneLoadResult(SceneHandle scene)
        {
            Scene = scene;
        }
    }
}
