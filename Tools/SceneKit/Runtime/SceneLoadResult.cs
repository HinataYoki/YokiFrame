namespace YokiFrame
{
    /// <summary>表示一次 SceneKit 场景加载结果。</summary>
    public readonly struct SceneLoadResult
    {
        /// <summary>创建场景加载结果。</summary>
        public SceneLoadResult(SceneHandle scene)
        {
            Scene = scene;
        }

        /// <summary>获取场景句柄。</summary>
        public SceneHandle Scene { get; }

        /// <summary>获取加载是否成功。</summary>
        public bool Succeeded => Scene.IsValid;
    }
}
