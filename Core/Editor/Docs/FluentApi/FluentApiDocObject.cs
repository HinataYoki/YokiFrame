#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FluentApi Object 扩展文档
    /// </summary>
    internal static class FluentApiDocObject
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "Object 扩展",
                Description = "通用对象扩展方法，支持链式调用和条件执行。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "Self 链式调用",
                        Code = @"// 将自己传入 Action 并返回自身
var player = new Player()
    .Self(p => p.Name = ""Hero"")
    .Self(p => p.Level = 1)
    .Self(p => p.Init());

// 条件执行
player
    .If(isVip, p => p.AddBonus())
    .If(hasItem, p => p.EquipItem(), p => p.ShowTip());"
                    },
                    new()
                    {
                        Title = "空值判断",
                        Code = @"// 判断是否为空（仅引用类型）
if (obj.IsNull()) return;
if (obj.IsNotNull()) obj.DoSomething();

// 空值替换
var result = obj.OrDefault(defaultValue);
var result2 = obj.OrDefault(() => CreateDefault());"
                    },
                    new()
                    {
                        Title = "集合扩展",
                        Code = @"// 安全获取字典值
var value = dict.GetOrDefault(key, defaultValue);
var value2 = dict.GetOrAdd(key, () => new Value());

// 遍历
list.ForEach(item => Process(item));
list.ForEach((item, index) => Process(item, index));

// 安全获取列表元素
var item = list.GetOrDefault(index, defaultValue);

// 链式添加
list.AddEx(item1).AddEx(item2).AddRangeEx(items);"
                    }
                }
            };
        }
    }
}
#endif
