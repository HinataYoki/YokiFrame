namespace YokiFrame.ECS
{
    /// <summary>
    /// 目标组件 - 用于追踪/攻击目标
    /// </summary>
    public struct Target : IComponentData
    {
        public long EntityId;
        public float LastKnownX;
        public float LastKnownY;
        public float LastKnownZ;
        
        public Target(long entityId)
        {
            EntityId = entityId;
            LastKnownX = 0;
            LastKnownY = 0;
            LastKnownZ = 0;
        }
        
        public Target(long entityId, float x, float y, float z)
        {
            EntityId = entityId;
            LastKnownX = x;
            LastKnownY = y;
            LastKnownZ = z;
        }
        
        public bool HasTarget => EntityId >= 0;
        public static Target None => new Target(-1);
    }
}
