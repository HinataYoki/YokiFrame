namespace YokiFrame
{
    /// <summary>表示 ResKit Provider 完成一次场景加载后的结果。</summary>
    public readonly struct ResSceneLoadResult
    {
        /// <summary>创建场景加载结果。</summary>
        public ResSceneLoadResult(ResSceneHandle scene)
        {
            Scene = scene;
        }

        /// <summary>获取 Provider 返回的场景句柄；无效句柄表示加载失败。</summary>
        public ResSceneHandle Scene { get; }

        /// <summary>获取加载是否成功。</summary>
        public bool Succeeded => Scene.IsValid;
    }
}
