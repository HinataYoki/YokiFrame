namespace YokiFrame.ECS
{
    /// <summary>
    /// 预制体标记 - 标识实体对应的预制体类型
    /// </summary>
    public struct Prefab : IComponentData
    {
        public int Id;
        
        public Prefab(int id)
        {
            Id = id;
        }
    }
}
