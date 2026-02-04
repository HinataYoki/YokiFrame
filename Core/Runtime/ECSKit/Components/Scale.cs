using UnityEngine;

namespace YokiFrame.ECS
{
    /// <summary>
    /// 缩放组件
    /// </summary>
    public struct Scale : IComponentData
    {
        public float X;
        public float Y;
        public float Z;
        
        public Scale(float uniform)
        {
            X = Y = Z = uniform;
        }
        
        public Scale(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        
        public Scale(Vector3 v)
        {
            X = v.x;
            Y = v.y;
            Z = v.z;
        }
        
        public Vector3 ToVector3() => new Vector3(X, Y, Z);
        
        public static Scale One => new Scale(1, 1, 1);
        
        public static implicit operator Vector3(Scale s) => new Vector3(s.X, s.Y, s.Z);
        public static implicit operator Scale(Vector3 v) => new Scale(v.x, v.y, v.z);
    }
}
