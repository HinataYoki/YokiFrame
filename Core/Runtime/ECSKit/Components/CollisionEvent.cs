namespace YokiFrame.ECS
{
    /// <summary>
    /// 碰撞事件组件 - 记录碰撞信息
    /// </summary>
    public struct CollisionEvent : IComponentData
    {
        public long OtherEntityId;
        public float ContactX;
        public float ContactY;
        public float ContactZ;
        public float NormalX;
        public float NormalY;
        public float NormalZ;
        
        public CollisionEvent(long otherEntityId, float cx, float cy, float cz, float nx, float ny, float nz)
        {
            OtherEntityId = otherEntityId;
            ContactX = cx;
            ContactY = cy;
            ContactZ = cz;
            NormalX = nx;
            NormalY = ny;
            NormalZ = nz;
        }
    }
}
