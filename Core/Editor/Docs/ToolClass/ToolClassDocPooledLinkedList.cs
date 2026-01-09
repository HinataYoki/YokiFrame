#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ToolClass PooledLinkedList 池化链表文档
    /// </summary>
    internal static class ToolClassDocPooledLinkedList
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "PooledLinkedList 池化链表",
                Description = "节点池化的双向链表，避免频繁添加删除节点时的 GC。适合需要频繁插入删除的场景。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基本操作",
                        Code = @"// 创建池化链表（指定初始池容量）
var list = new PooledLinkedList<int>(initialPoolCapacity: 64);

// 预热节点池（避免运行时分配）
list.Prewarm(100);

// 添加元素
list.AddLast(1);
list.AddFirst(0);
var node = list.AddLast(2);

// 插入
list.InsertAfter(node, 3);
list.InsertBefore(node, 1);

// 删除（节点自动回收到池中）
list.Remove(1);
list.RemoveFirst();
list.RemoveLast();

// 批量删除
list.RemoveAll(x => x < 0);

// 清空（所有节点回收到池中）
list.Clear();

// 池管理
list.TrimPool();   // 裁剪多余的池节点
list.ClearPool();  // 清空节点池"
                    },
                    new()
                    {
                        Title = "遍历和查找",
                        Code = @"// 正向遍历
foreach (var item in list)
{
    Debug.Log(item);
}

// 反向遍历
foreach (var item in list.Reverse())
{
    Debug.Log(item);
}

// 查找
var node = list.Find(5);
bool contains = list.Contains(5);

// 转数组
int[] array = list.ToArray();"
                    }
                }
            };
        }
    }
}
#endif
