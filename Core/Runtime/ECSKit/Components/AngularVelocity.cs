using UnityEngine;

namespace YokiFrame.ECS
{
    /// <summary>
    /// 角速度组件（欧拉角/秒）
    /// </summary>
    public struct AngularVelocity : IComponentData
    {
        public float X;
        public float Y;
        public float Z;
        
        public AngularVelocity(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        
        public AngularVelocity(Vector3 v)
        {
            X = v.x;
            Y = v.y;
            Z = v.z;
        }
        
        public Vector3 ToVector3() => new Vector3(X, Y, Z);
        
        public static implicit operator Vector3(AngularVelocity v) => new Vector3(v.X, v.Y, v.Z);
        public static implicit operator AngularVelocity(Vector3 v) => new AngularVelocity(v.x, v.y, v.z);
    }
}
