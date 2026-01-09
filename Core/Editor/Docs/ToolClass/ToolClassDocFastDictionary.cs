#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ToolClass FastDictionary 快速字典文档
    /// </summary>
    internal static class ToolClassDocFastDictionary
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "FastDictionary 快速字典",
                Description = "基于开放寻址法实现的高性能字典，减少 GC 压力。适合高频查找、热路径场景。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基本操作",
                        Code = @"// 创建字典（预估容量避免扩容）
var dict = new FastDictionary<int, string>(128);

// 添加元素
dict.Add(1, ""one"");
dict[2] = ""two"";

// 安全添加（键存在时返回 false）
bool added = dict.TryAdd(1, ""duplicate""); // false

// 获取值
string value = dict[1];
bool exists = dict.ContainsKey(1);

// 删除
dict.Remove(1);

// 清空
dict.Clear();",
                        Explanation = "开放寻址法内存连续，缓存友好，适合频繁查找场景。"
                    },
                    new()
                    {
                        Title = "热路径推荐用法",
                        Code = @"// TryGetValue - 无异常查询（推荐）
if (dict.TryGetValue(key, out var value))
{
    // 使用 value
}

// GetValueOrDefault - 直接获取，不存在返回默认值
var result = dict.GetValueOrDefault(999, ""not found"");

// GetOrAdd - 懒加载模式
var cached = dict.GetOrAdd(key, ""default"");

// GetOrAdd 工厂模式 - 仅在需要时创建
var expensive = dict.GetOrAdd(key, k => ExpensiveCreate(k));",
                        Explanation = "热路径中避免使用索引器直接访问，因为键不存在时会抛出异常。"
                    },
                    new()
                    {
                        Title = "遍历",
                        Code = @"// 标准遍历
foreach (var kvp in dict)
{
    Debug.Log($""{kvp.Key}: {kvp.Value}"");
}

// ForEach - 无 GC 遍历（推荐）
dict.ForEach((key, value) => 
{
    Debug.Log($""{key}: {value}"");
});",
                        Explanation = "ForEach 方法避免了迭代器的 GC 分配，适合高频调用场景。"
                    }
                }
            };
        }
    }
}
#endif
