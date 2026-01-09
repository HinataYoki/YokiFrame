#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 容器池文档
    /// </summary>
    internal static class PoolKitDocContainer
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "容器池",
                Description = "List、Dictionary、HashSet 等容器的复用池，避免频繁分配。使用 Unity 内置的 Pool API。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "使用容器池",
                        Code = @"using UnityEngine.Pool;

// 方式1：使用 Pool 静态类（自动归还）
Pool.List<int>(list =>
{
    list.Add(1);
    list.Add(2);
    list.Add(3);
    // 使用 list...
    // 作用域结束后自动归还
});

Pool.Dictionary<int, string>(dict =>
{
    dict[1] = ""one"";
    dict[2] = ""two"";
    // 使用 dict...
});

// 方式2：手动管理
var list = ListPool<int>.Get();
list.Add(1);
list.Add(2);
// 使用完毕归还（会自动 Clear）
ListPool<int>.Release(list);

var dict = DictionaryPool<int, string>.Get();
dict[1] = ""one"";
DictionaryPool<int, string>.Release(dict);",
                        Explanation = "容器池避免了频繁 new List/Dictionary 带来的 GC 压力。"
                    }
                }
            };
        }
    }
}
#endif
