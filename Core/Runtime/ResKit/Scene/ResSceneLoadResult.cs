namespace YokiFrame
{
    /// <summary>
    /// 表示 ResKit 场景加载完成结果。
    /// </summary>
    public struct ResSceneLoadResult
    {
        public readonly ResSceneHandle Scene;

        public ResSceneLoadResult(ResSceneHandle scene)
        {
            Scene = scene;
        }
    }
}
