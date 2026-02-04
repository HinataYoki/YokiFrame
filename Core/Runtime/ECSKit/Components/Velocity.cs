using UnityEngine;

namespace YokiFrame.ECS
{
    /// <summary>
    /// 速度组件
    /// </summary>
    public struct Velocity : IComponentData
    {
        public float X;
        public float Y;
        public float Z;
        
        public Velocity(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        
        public Velocity(Vector3 v)
        {
            X = v.x;
            Y = v.y;
            Z = v.z;
        }
        
        public Vector3 ToVector3() => new Vector3(X, Y, Z);
        
        public float SqrMagnitude => X * X + Y * Y + Z * Z;
        public float Magnitude => UnityEngine.Mathf.Sqrt(SqrMagnitude);
        
        public static implicit operator Vector3(Velocity v) => new Vector3(v.X, v.Y, v.Z);
        public static implicit operator Velocity(Vector3 v) => new Velocity(v.x, v.y, v.z);
    }
}
