namespace YokiFrame.ECS
{
    /// <summary>
    /// 碰撞体类型
    /// </summary>
    public enum ColliderShape : byte
    {
        Sphere,
        Box,
        Capsule
    }
    
    /// <summary>
    /// 碰撞体组件
    /// </summary>
    public struct ColliderData : IComponentData
    {
        public ColliderShape Shape;
        public float Radius;      // Sphere/Capsule
        public float HalfExtentX; // Box
        public float HalfExtentY; // Box
        public float HalfExtentZ; // Box/Capsule height
        public int Layer;         // 碰撞层
        public int Mask;          // 碰撞掩码
        
        public static ColliderData Sphere(float radius, int layer = 0, int mask = -1)
        {
            return new ColliderData
            {
                Shape = ColliderShape.Sphere,
                Radius = radius,
                Layer = layer,
                Mask = mask
            };
        }
        
        public static ColliderData Box(float halfX, float halfY, float halfZ, int layer = 0, int mask = -1)
        {
            return new ColliderData
            {
                Shape = ColliderShape.Box,
                HalfExtentX = halfX,
                HalfExtentY = halfY,
                HalfExtentZ = halfZ,
                Layer = layer,
                Mask = mask
            };
        }
        
        public static ColliderData Capsule(float radius, float height, int layer = 0, int mask = -1)
        {
            return new ColliderData
            {
                Shape = ColliderShape.Capsule,
                Radius = radius,
                HalfExtentZ = height * 0.5f,
                Layer = layer,
                Mask = mask
            };
        }
        
        public bool CanCollideWith(int otherLayer)
        {
            return (Mask & (1 << otherLayer)) != 0;
        }
    }
}
