#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FluentApi Vector 扩展文档
    /// </summary>
    internal static class FluentApiDocVector
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "Vector 扩展",
                Description = "向量操作扩展方法，支持分量修改、重组、2D/3D 转换等。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "分量修改",
                        Code = @"// 修改 Vector3 单个分量
var pos = transform.position
    .X(10f)   // 只修改 X
    .Y(5f)    // 只修改 Y
    .Z(0f);   // 只修改 Z

// 链式修改多个分量
transform.position = transform.position.X(0).Z(0);",
                        Explanation = "返回新向量，不修改原值，适合需要保持某些分量不变的场景。"
                    },
                    new()
                    {
                        Title = "分量重组",
                        Code = @"// Vector3 分量交换
var v3 = new Vector3(1, 2, 3);
var swapped = v3.YXZ();  // (2, 1, 3)
var rotated = v3.ZXY();  // (3, 1, 2)

// 提取 2D 分量
Vector2 xz = v3.XZ();  // (1, 3) - 俯视图
Vector2 xy = v3.XY();  // (1, 2) - 正视图
Vector2 yz = v3.YZ();  // (2, 3) - 侧视图

// 重复分量
Vector2 xx = v3.XX();  // (1, 1)
Vector3 xxy = v3.XXY(); // (1, 1, 2)",
                        Explanation = "分量重组方法名由分量字母组成，如 XZ 表示取 X 和 Z 分量。"
                    },
                    new()
                    {
                        Title = "2D/3D 转换",
                        Code = @"// Vector2 转 Vector3（指定第三个分量）
var v2 = new Vector2(1, 2);
var v3_xyo = v2.XYO(0);   // (1, 2, 0) - XY 平面
var v3_xoy = v2.XOY(0);   // (1, 0, 2) - XZ 平面（俯视）
var v3_oxy = v2.OXY(0);   // (0, 1, 2) - YZ 平面

// 实际应用：2D 坐标转 3D 世界坐标
Vector2 screenPos = Input.mousePosition;
Vector3 worldPos = screenPos.XOY(0); // Y 轴为高度",
                        Explanation = "O 表示插入的第三个分量位置，值由参数指定。"
                    }
                }
            };
        }
    }
}
#endif
