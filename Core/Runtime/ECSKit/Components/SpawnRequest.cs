namespace YokiFrame.ECS
{
    /// <summary>
    /// 生成请求 - 在 Logic 阶段添加，Creation 阶段统一处理
    /// </summary>
    public struct SpawnRequest : IComponentData
    {
        public int PrefabId;
        public float X;
        public float Y;
        public float Z;
        public int ExtraData;
        
        public SpawnRequest(int prefabId, float x, float y, float z, int extraData = 0)
        {
            PrefabId = prefabId;
            X = x;
            Y = y;
            Z = z;
            ExtraData = extraData;
        }
    }
}
