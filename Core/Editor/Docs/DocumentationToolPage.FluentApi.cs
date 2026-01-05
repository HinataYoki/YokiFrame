#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    // FluentApi 和 ToolClass 文档
    public partial class DocumentationToolPage
    {
        private DocModule CreateFluentApiDoc()
        {
            return new DocModule
            {
                Name = "FluentApi",
                Icon = "🔗",
                Category = "CORE KIT",
                Description = "流畅 API 扩展方法集合，提供链式调用支持。包含 Object、String、Transform、Vector、Color 等类型的扩展。",
                Sections = new List<DocSection>
                {
                    new()
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
                    },
                    new()
                    {
                        Title = "String 扩展",
                        Description = "字符串处理扩展方法。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "字符串操作",
                                Code = @"// 空值判断
if (str.IsNullOrEmpty()) return;
if (str.IsNotNullOrWhiteSpace()) Process(str);

// StringBuilder 链式
var result = ""Hello""
    .Builder()
    .AddSuffix("" World"")
    .AddPrefix(""Say: "")
    .ToString();

// 格式化
var msg = ""{0} has {1} HP"".Format(name, hp);

// 首字母大小写
var upper = ""hello"".UpperFirst(); // ""Hello""
var lower = ""Hello"".LowerFirst(); // ""hello""

// 安全截取
var sub = str.SafeSubstring(0, 10);

// 移除前后缀
var name = ""PlayerController"".RemoveSuffix(""Controller""); // ""Player""
var path = ""/root/file"".RemovePrefix(""/root/""); // ""file"""
                            }
                        }
                    },
                    new()
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
transform.ForEachChild((child, index) => child.name = $""Child_{index}"");

// 销毁所有子物体
transform.DestroyAllChildren();

// 查找组件
var button = transform.FindComponent<Button>(""BtnStart"");
var text = transform.FindByPath<Text>(""Panel/Title"");"
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
                    }
                }
            };
        }
        
        private DocModule CreateToolClassDoc()
        {
            return new DocModule
            {
                Name = "ToolClass",
                Icon = "🧰",
                Category = "CORE KIT",
                Description = "工具类集合，包含 BindValue（数据绑定）、PooledLinkedList（池化链表）、SpanSplitter（零分配字符串分割）等高性能工具。",
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "BindValue 数据绑定",
                        Description = "响应式数据绑定，当值变化时自动通知所有监听者。适合 MVVM 模式的数据层。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "基本使用",
                                Code = @"public class PlayerData
{
    // 创建可绑定的属性
    public BindValue<int> Health = new(100);
    public BindValue<int> Gold = new(0);
    public BindValue<string> Name = new(""Player"");
}

// 绑定 UI 更新
var data = new PlayerData();

// 绑定回调（值变化时触发）
data.Health.Bind(hp => healthText.text = hp.ToString());
data.Gold.Bind(gold => goldText.text = gold.ToString());

// 绑定并立即触发一次
data.Name.BindWithCallback(name => nameText.text = name);

// 修改值会自动触发回调
data.Health.Value = 80;  // healthText 自动更新
data.Gold.Value += 100;  // goldText 自动更新

// 静默修改（不触发回调）
data.Health.SetValueWithoutEvent(50);

// 解绑
data.Health.UnBind(callback);
data.Health.UnBindAll();"
                            },
                            new()
                            {
                                Title = "隐式转换",
                                Code = @"BindValue<int> health = new(100);

// 隐式转换为值类型
int currentHealth = health;  // 等同于 health.Value

// 比较
if (health > 50) { }  // 自动转换"
                            }
                        }
                    },
                    new()
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

// 索引访问（性能较低，慎用）
var first = list[0];

// 转数组
int[] array = list.ToArray();"
                            }
                        }
                    },
                    new()
                    {
                        Title = "SpanSplitter 零分配分割",
                        Description = "使用 Span<char> 实现的字符串分割器，完全避免字符串分配。适合高频字符串处理场景。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "使用示例",
                                Code = @"// 传统方式（产生 GC）
// string[] parts = text.Split(',');

// 零分配方式
var text = ""item1,item2,item3,item4"";
var splitter = new SpanSplitter(text.AsSpan(), ',');

while (splitter.MoveNext(out var part))
{
    // part 是 ReadOnlySpan<char>，不会分配新字符串
    Debug.Log(part.ToString()); // 仅在需要时转换
    
    // 直接比较
    if (part.SequenceEqual(""item2""))
    {
        // 找到了
    }
}",
                                Explanation = "SpanSplitter 是 ref struct，只能在栈上使用，不能作为类成员。"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
