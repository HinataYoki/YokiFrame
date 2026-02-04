using UnityEngine;

namespace YokiFrame.ECS
{
    /// <summary>
    /// 位置组件
    /// </summary>
    public struct Position : IComponentData
    {
        public float X;
        public float Y;
        public float Z;
        
        public Position(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        
        public Position(Vector3 v)
        {
            X = v.x;
            Y = v.y;
            Z = v.z;
        }
        
        public Vector3 ToVector3() => new Vector3(X, Y, Z);
        
        public static implicit operator Vector3(Position p) => new Vector3(p.X, p.Y, p.Z);
        public static implicit operator Position(Vector3 v) => new Position(v.x, v.y, v.z);
    }
}
