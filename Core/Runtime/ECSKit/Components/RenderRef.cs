namespace YokiFrame.ECS
{
    /// <summary>
    /// 渲染引用 - 关联 GameObject 的 InstanceId
    /// </summary>
    public struct RenderRef : IComponentData
    {
        public int GameObjectInstanceId;
        
        public RenderRef(int instanceId)
        {
            GameObjectInstanceId = instanceId;
        }
    }
}
