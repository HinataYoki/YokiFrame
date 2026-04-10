#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// TableKit ExternalTypeUtil 文档
    /// </summary>
    internal static class TableKitDocExternalType
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "ExternalTypeUtil",
                Description = "可选的类型转换工具，将 Luban 的 vector 类型转换为 Unity 的 Vector 类型。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "类型转换",
                        Code = @"// 在 TableKit 工具中勾选「生成 ExternalTypeUtil」后可用

// Luban vector2 -> Unity Vector2
Vector2 pos = ExternalTypeUtil.NewVector2(item.Position);

// Luban vector3 -> Unity Vector3
Vector3 scale = ExternalTypeUtil.NewVector3(item.Scale);

// Luban vector4 -> Unity Vector4
Vector4 color = ExternalTypeUtil.NewVector4(item.Color);

// 也支持 Int 版本
Vector2Int gridPos = ExternalTypeUtil.NewVector2Int(item.GridPosition);
Vector3Int cellPos = ExternalTypeUtil.NewVector3Int(item.CellPosition);",
                        Explanation = "如果配置表中没有使用 vector 类型，可以不生成此文件。"
                    }
                }
            };
        }
    }
}
#endif
