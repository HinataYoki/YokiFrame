using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 快速替换修改或交换Unity Vector分量值的拓展
    /// </summary>
    /*
    用法示例：
    void Func()
    {
        var input = new Vector2(-1, 1);//假设这是用户按下wasd的输入
        var worldInput = input.XOY().normalized;//转化到player自身的局部坐标系的输入,并归一
        player.characterController.SimpleMove(worldInput * speed);
    }
    */
    public static class UnityVectorExtension
    {
        #region Vector分量
        //只修改Vector单个分量的拓展
        public static Vector3 X(this Vector3 vector3, float x)
        {
            vector3.x = x;
            return vector3;
        }
        public static Vector3 Y(this Vector3 vector3, float y)
        {
            vector3.y = y;
            return vector3;
        }
        public static Vector3 Z(this Vector3 vector3, float z)
        {
            vector3.z = z;
            return vector3;
        }
        #endregion
        #region Vector2
        public static Vector2 YZ(this Vector3 vector3)
        {
            (vector3.x, vector3.y) = (vector3.y, vector3.z);
            return vector3;
        }
        public static Vector2 XZ(this Vector3 vector3)
        {
            vector3.y = vector3.z;
            return vector3;
        }
        public static Vector2 YX(this Vector3 vector3)
        {
            (vector3.x, vector3.y) = (vector3.y, vector3.x);
            return vector3;
        }
        public static Vector2 ZY(this Vector3 vector3)
        {
            vector3.x = vector3.z;
            return vector3;
        }
        public static Vector2 ZX(this Vector3 vector3)
        {
            (vector3.x, vector3.y) = (vector3.z, vector3.x);
            return vector3;
        }

        public static Vector2 XX(this Vector3 vector3)
        {
            vector3.y = vector3.x;
            return vector3;
        }
        public static Vector2 YY(this Vector3 vector3)
        {
            vector3.x = vector3.y;
            return vector3;
        }
        public static Vector2 ZZ(this Vector3 vector3)
        {
            (vector3.x, vector3.y) = (vector3.z, vector3.z);
            return vector3;
        }
        //将Vector2添加一个值转为Vector3的拓展
        public static Vector3 XYO(this Vector2 vector2, float o = 0) => new Vector3(vector2.x, vector2.y, o);
        public static Vector3 YXO(this Vector2 vector2, float o = 0) => new Vector3(vector2.y, vector2.x, o);
        public static Vector3 XOY(this Vector2 vector2, float o = 0) => new Vector3(vector2.x, o, vector2.y);
        public static Vector3 YOX(this Vector2 vector2, float o = 0) => new Vector3(vector2.y, o, vector2.y);
        public static Vector3 OXY(this Vector2 vector2, float o = 0) => new Vector3(o, vector2.x, vector2.y);
        public static Vector3 OYX(this Vector2 vector2, float o = 0) => new Vector3(o, vector2.y, vector2.x);
        #endregion
        #region Vector3
        public static Vector3 XXX(this Vector3 vector3)
        {
            (vector3.y, vector3.z) = (vector3.x, vector3.x);
            return vector3;
        }
        public static Vector3 XXY(this Vector3 vector3)
        {
            (vector3.y, vector3.z) = (vector3.x, vector3.y);
            return vector3;
        }
        public static Vector3 XXZ(this Vector3 vector3)
        {
            vector3.y = vector3.x;
            return vector3;
        }
        public static Vector3 XXO(this Vector3 vector3, float o = 0)
        {
            (vector3.y, vector3.z) = (vector3.x, o);
            return vector3;
        }
        public static Vector3 XYX(this Vector3 vector3)
        {
            vector3.z = vector3.x;
            return vector3;
        }
        public static Vector3 XYY(this Vector3 vector3)
        {
            vector3.z = vector3.y;
            return vector3;
        }
        public static Vector3 XYO(this Vector3 vector3, float o = 0)
        {
            vector3.z = o;
            return vector3;
        }
        public static Vector3 XZX(this Vector3 vector3)
        {
            (vector3.y, vector3.z) = (vector3.z, vector3.x);
            return vector3;
        }
        public static Vector3 XZY(this Vector3 vector3)
        {
            (vector3.y, vector3.z) = (vector3.z, vector3.y);
            return vector3;
        }
        public static Vector3 XZZ(this Vector3 vector3)
        {
            vector3.y = vector3.z;
            return vector3;
        }
        public static Vector3 XZO(this Vector3 vector3, float o = 0)
        {
            (vector3.y, vector3.z) = (vector3.z, o);
            return vector3;
        }
        public static Vector3 XOX(this Vector3 vector3, float o = 0)
        {
            (vector3.y, vector3.z) = (o, vector3.x);
            return vector3;
        }
        public static Vector3 XOY(this Vector3 vector3, float o = 0)
        {
            (vector3.y, vector3.z) = (o, vector3.y);
            return vector3;
        }
        public static Vector3 XOZ(this Vector3 vector3, float o = 0)
        {
            vector3.y = o;
            return vector3;
        }
        public static Vector3 XOO(this Vector3 vector3, float oy = 0, float oz = 0)
        {
            (vector3.y, vector3.z) = (oy, oz);
            return vector3;
        }
        public static Vector3 YXX(this Vector3 vector3)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.y, vector3.x, vector3.x);
            return vector3;
        }
        public static Vector3 YXY(this Vector3 vector3)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.y, vector3.x, vector3.y);
            return vector3;
        }
        public static Vector3 YXZ(this Vector3 vector3)
        {
            (vector3.x, vector3.y) = (vector3.y, vector3.x);
            return vector3;
        }
        public static Vector3 YXO(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.y, vector3.x, o);
            return vector3;
        }
        public static Vector3 YYX(this Vector3 vector3)
        {
            (vector3.x, vector3.z) = (vector3.y, vector3.x);
            return vector3;
        }
        public static Vector3 YYY(this Vector3 vector3)
        {
            (vector3.x, vector3.z) = (vector3.y, vector3.y);
            return vector3;
        }
        public static Vector3 YYZ(this Vector3 vector3)
        {
            vector3.x = vector3.y;
            return vector3;
        }
        public static Vector3 YYO(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.z) = (vector3.y, o);
            return vector3;
        }
        public static Vector3 YZX(this Vector3 vector3)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.y, vector3.z, vector3.x);
            return vector3;
        }
        public static Vector3 YZY(this Vector3 vector3)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.y, vector3.z, vector3.y);
            return vector3;
        }
        public static Vector3 YZZ(this Vector3 vector3)
        {
            (vector3.x, vector3.y) = (vector3.y, vector3.z);
            return vector3;
        }
        public static Vector3 YZO(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.y, vector3.z, o);
            return vector3;
        }
        public static Vector3 YOX(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.y, o, vector3.x);
            return vector3;
        }
        public static Vector3 YOY(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.y, o, vector3.y);
            return vector3;
        }
        public static Vector3 YOZ(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y) = (vector3.y, o);
            return vector3;
        }
        public static Vector3 YOO(this Vector3 vector3, float oy = 0, float oz = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.y, oy, oz);
            return vector3;
        }
        public static Vector3 ZXX(this Vector3 vector3)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.z, vector3.x, vector3.x);
            return vector3;
        }
        public static Vector3 ZXY(this Vector3 vector3)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.z, vector3.x, vector3.y);
            return vector3;
        }
        public static Vector3 ZXZ(this Vector3 vector3)
        {
            (vector3.x, vector3.y) = (vector3.z, vector3.x);
            return vector3;
        }
        public static Vector3 ZXO(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.z, vector3.x, o);
            return vector3;
        }
        public static Vector3 ZYX(this Vector3 vector3)
        {
            (vector3.x, vector3.z) = (vector3.z, vector3.x);
            return vector3;
        }
        public static Vector3 ZYY(this Vector3 vector3)
        {
            (vector3.x, vector3.z) = (vector3.z, vector3.y);
            return vector3;
        }
        public static Vector3 ZYZ(this Vector3 vector3)
        {
            vector3.x = vector3.z;
            return vector3;
        }
        public static Vector3 ZYO(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.z) = (vector3.z, o);
            return vector3;
        }
        public static Vector3 ZZX(this Vector3 vector3)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.z, vector3.z, vector3.x);
            return vector3;
        }
        public static Vector3 ZZY(this Vector3 vector3)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.z, vector3.z, vector3.y);
            return vector3;
        }
        public static Vector3 ZZZ(this Vector3 vector3)
        {
            (vector3.x, vector3.y) = (vector3.z, vector3.z);
            return vector3;
        }
        public static Vector3 ZZO(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.z, vector3.z, o);
            return vector3;
        }
        public static Vector3 ZOX(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.z, o, vector3.x);
            return vector3;
        }
        public static Vector3 ZOY(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.z, o, vector3.y);
            return vector3;
        }
        public static Vector3 ZOZ(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y) = (vector3.z, o);
            return vector3;
        }
        public static Vector3 ZOO(this Vector3 vector3, float oy = 0, float oz = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (vector3.z, oy, oz);
            return vector3;
        }
        public static Vector3 OXX(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (o, vector3.x, vector3.x);
            return vector3;
        }
        public static Vector3 OXY(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (o, vector3.x, vector3.y);
            return vector3;
        }
        public static Vector3 OXZ(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y) = (o, vector3.x);
            return vector3;
        }
        public static Vector3 OXO(this Vector3 vector3, float ox = 0, float oz = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (ox, vector3.x, oz);
            return vector3;
        }
        public static Vector3 OYX(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.z) = (o, vector3.x);
            return vector3;
        }
        public static Vector3 OYY(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.z) = (o, vector3.y);
            return vector3;
        }
        public static Vector3 OYZ(this Vector3 vector3, float o = 0)
        {
            vector3.x = o;
            return vector3;
        }
        public static Vector3 OYO(this Vector3 vector3, float ox = 0, float oz = 0)
        {
            (vector3.x, vector3.z) = (ox, oz);
            return vector3;
        }
        public static Vector3 OZX(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (o, vector3.z, vector3.x);
            return vector3;
        }
        public static Vector3 OZY(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (o, vector3.z, vector3.y);
            return vector3;
        }
        public static Vector3 OZZ(this Vector3 vector3, float o = 0)
        {
            (vector3.x, vector3.y) = (o, vector3.z);
            return vector3;
        }
        public static Vector3 OZO(this Vector3 vector3, float ox = 0, float oz = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (ox, vector3.z, oz);
            return vector3;
        }
        public static Vector3 OOX(this Vector3 vector3, float ox = 0, float oy = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (ox, oy, vector3.x);
            return vector3;
        }
        public static Vector3 OOY(this Vector3 vector3, float ox = 0, float oy = 0)
        {
            (vector3.x, vector3.y, vector3.z) = (ox, oy, vector3.y);
            return vector3;
        }
        public static Vector3 OOZ(this Vector3 vector3, float ox = 0, float oy = 0)
        {
            (vector3.x, vector3.y) = (ox, oy);
            return vector3;
        }
        #endregion
    }
}