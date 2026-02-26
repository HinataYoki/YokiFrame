#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FluentApi Transform 扩展文档
    /// </summary>
    internal static class FluentApiDocTransform
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "Transform 扩展",
                Description = "Transform 和 RectTransform 的链式操作扩展。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "Transform 操作",
                        Code = @"// 链式设置位置
transform
    .Position(Vector3.zero)
    .LocalPosition(0, 1, 0)
    .LocalPositionX(5)
    .LocalScale(1.5f)
    .Rotation(Quaternion.identity);

// 层级操作
transform
    .Parent(newParent)
    .AsLastSibling()
    .SiblingIndex(2);

// 重置
transform.ResetTransform();

// 遍历子物体
transform.ForEachChild(child => child.gameObject.SetActive(false));

// 销毁所有子物体
transform.DestroyAllChildren();

// 查找组件
var button = transform.FindComponent<Button>(""BtnStart"");"
                    },
                    new()
                    {
                        Title = "RectTransform 操作",
                        Code = @"// 链式设置 UI 属性
rectTransform
    .AnchoredPosition(100, 200)
    .AnchoredPositionX(50)
    .SizeDelta(200, 100)
    .Anchors(Vector2.zero, Vector2.one)
    .Pivot(0.5f, 0.5f);

// 重置 RectTransform
rectTransform.ResetRectTransform();"
                    }
                }
            };
        }
    }
}
#endif
