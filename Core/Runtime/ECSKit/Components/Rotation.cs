using UnityEngine;

namespace YokiFrame.ECS
{
    /// <summary>
    /// 旋转组件（四元数）
    /// </summary>
    public struct Rotation : IComponentData
    {
        public float X;
        public float Y;
        public float Z;
        public float W;
        
        public Rotation(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
        
        public Rotation(Quaternion q)
        {
            X = q.x;
            Y = q.y;
            Z = q.z;
            W = q.w;
        }
        
        public Quaternion ToQuaternion() => new Quaternion(X, Y, Z, W);
        
        public static Rotation Identity => new Rotation(0, 0, 0, 1);
        
        public static implicit operator Quaternion(Rotation r) => new Quaternion(r.X, r.Y, r.Z, r.W);
        public static implicit operator Rotation(Quaternion q) => new Rotation(q.x, q.y, q.z, q.w);
    }
}
