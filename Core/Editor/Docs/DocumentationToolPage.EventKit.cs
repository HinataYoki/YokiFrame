#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    // EventKit 文档
    public partial class DocumentationToolPage
    {
        private DocModule CreateEventKitDoc()
        {
            return new DocModule
            {
                Name = "EventKit",
                Icon = "📡",
                Category = "CORE KIT",
                Description = "轻量级事件系统，支持枚举、类型和字符串三种事件键。零 GC 设计，适合高频事件场景。推荐使用枚举事件获得最佳性能和类型安全。",
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "枚举事件（推荐）",
                        Description = "使用枚举作为事件键，获得最佳性能和类型安全。避免魔法字符串，编译期检查。内部使用 UnsafeUtility 避免枚举装箱。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "定义事件枚举",
                                Code = @"public enum GameEvent
{
    PlayerDied,
    ScoreChanged,
    LevelCompleted,
    ItemCollected,
    EnemySpawned
}"
                            },
                            new()
                            {
                                Title = "注册和触发无参事件",
                                Code = @"// 注册无参事件
EventKit.Enum.Register(GameEvent.PlayerDied, OnPlayerDied);

// 触发事件
EventKit.Enum.Send(GameEvent.PlayerDied);

// 注销事件（重要！防止内存泄漏）
EventKit.Enum.UnRegister(GameEvent.PlayerDied, OnPlayerDied);

private void OnPlayerDied()
{
    Debug.Log(""玩家死亡"");
}",
                                Explanation = "务必在对象销毁时注销事件，避免内存泄漏。"
                            },
                            new()
                            {
                                Title = "带参数的事件",
                                Code = @"// 注册带参数事件
EventKit.Enum.Register<GameEvent, int>(GameEvent.ScoreChanged, OnScoreChanged);

// 触发带参数事件
EventKit.Enum.Send(GameEvent.ScoreChanged, 100);

// 多参数事件（使用 params object[]）
EventKit.Enum.Register(GameEvent.ItemCollected, OnItemCollected);
EventKit.Enum.Send(GameEvent.ItemCollected, itemId, count, ""Gold"");

private void OnScoreChanged(int newScore)
{
    scoreText.text = newScore.ToString();
}

private void OnItemCollected(object[] args)
{
    int itemId = (int)args[0];
    int count = (int)args[1];
}"
                            },
                            new()
                            {
                                Title = "使用 LinkUnRegister 自动注销",
                                Code = @"// Register 返回 LinkUnRegister，可用于链式注销
private LinkUnRegister mUnRegister;

void OnEnable()
{
    mUnRegister = EventKit.Enum.Register(GameEvent.PlayerDied, OnPlayerDied);
}

void OnDisable()
{
    // 方式1：直接调用 UnRegister
    mUnRegister.UnRegister();
    
    // 方式2：使用 UnRegisterWhenGameObjectDestroyed 扩展
    // EventKit.Enum.Register(...).UnRegisterWhenGameObjectDestroyed(gameObject);
}",
                                Explanation = "LinkUnRegister 封装了注销逻辑，避免手动管理回调引用。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "类型事件",
                        Description = "使用类型作为事件键，适合需要传递复杂数据结构的场景。类型本身就是事件标识。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "定义事件数据结构",
                                Code = @"// 推荐使用结构体（小于16字节）减少 GC
public struct PlayerDiedEvent
{
    public int PlayerId;
    public Vector3 Position;
}

public struct DamageEvent
{
    public int SourceId;
    public int TargetId;
    public float Damage;
    public DamageType Type;
}

public enum DamageType { Physical, Magic, True }",
                                Explanation = "结构体避免堆分配，减少 GC 压力。"
                            },
                            new()
                            {
                                Title = "使用类型事件",
                                Code = @"// 注册
EventKit.Type.Register<PlayerDiedEvent>(OnPlayerDied);
EventKit.Type.Register<DamageEvent>(OnDamage);

// 触发
EventKit.Type.Send(new PlayerDiedEvent 
{ 
    PlayerId = 1, 
    Position = transform.position 
});

EventKit.Type.Send(new DamageEvent
{
    SourceId = attackerId,
    TargetId = targetId,
    Damage = 50f,
    Type = DamageType.Physical
});

// 注销
EventKit.Type.UnRegister<PlayerDiedEvent>(OnPlayerDied);

private void OnPlayerDied(PlayerDiedEvent evt)
{
    Debug.Log($""玩家 {evt.PlayerId} 在 {evt.Position} 死亡"");
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "字符串事件（已过时）",
                        Description = "使用字符串作为事件键。已标记为 [Obsolete]，存在类型安全隐患且重构困难，建议迁移到枚举或类型事件。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "字符串事件用法（不推荐）",
                                Code = @"// ⚠️ 已过时，仅用于旧代码兼容
#pragma warning disable CS0618

// 注册
EventKit.String.Register(""PlayerDied"", OnPlayerDied);
EventKit.String.Register<int>(""ScoreChanged"", OnScoreChanged);

// 触发
EventKit.String.Send(""PlayerDied"");
EventKit.String.Send(""ScoreChanged"", 100);

// 注销
EventKit.String.UnRegister(""PlayerDied"", OnPlayerDied);

#pragma warning restore CS0618",
                                Explanation = "字符串事件容易拼写错误，重构时无法自动更新引用，建议尽快迁移。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "EasyEvent 底层 API",
                        Description = "EventKit 内部使用 EasyEvent 实现，也可以直接使用 EasyEvent 创建独立的事件实例。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "直接使用 EasyEvent",
                                Code = @"// 创建独立的事件实例
private readonly EasyEvent mOnDeath = new();
private readonly EasyEvent<int> mOnHealthChanged = new();
private readonly EasyEvent<int, string> mOnItemAdded = new();

// 注册
mOnDeath.Register(OnDeath);
mOnHealthChanged.Register(OnHealthChanged);

// 触发
mOnDeath.Trigger();
mOnHealthChanged.Trigger(currentHealth);
mOnItemAdded.Trigger(itemId, itemName);

// 注销
mOnDeath.UnRegister(OnDeath);
mOnDeath.UnRegisterAll(); // 注销所有监听者",
                                Explanation = "EasyEvent 适合在类内部使用，不需要全局事件总线的场景。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "参数类型通道隔离",
                        Description = "不同参数类型的事件通道是完全隔离的，互不影响。这是编译期类型安全的核心设计。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "通道隔离示例",
                                Code = @"// 以下是 5 个完全独立的事件通道，互不干扰：

// 通道1：无参数
EventKit.Enum.Register(GameEvent.Test, OnTestNoParam);
EventKit.Enum.Send(GameEvent.Test);

// 通道2：int 参数
EventKit.Enum.Register<GameEvent, int>(GameEvent.Test, OnTestInt);
EventKit.Enum.Send(GameEvent.Test, 100);

// 通道3：string 参数
EventKit.Enum.Register<GameEvent, string>(GameEvent.Test, OnTestString);
EventKit.Enum.Send(GameEvent.Test, ""hello"");

// 通道4：元组参数
EventKit.Enum.Register<GameEvent, (int, string)>(GameEvent.Test, OnTestTuple);
EventKit.Enum.Send(GameEvent.Test, (1, ""world""));

// 通道5：object[] 参数
EventKit.Enum.Register(GameEvent.Test, OnTestParams);
EventKit.Enum.Send(GameEvent.Test, 1, ""a"", 3.14f);

// 触发 Send<int> 只会通知 Register<int> 的监听者
// 触发 Send<string> 只会通知 Register<string> 的监听者
// 以此类推，参数类型不匹配的监听者不会收到通知",
                                Explanation = "这种设计确保了编译期类型检查，避免运行时类型转换错误。同一个枚举值可以承载多种参数类型的事件。"
                            },
                            new()
                            {
                                Title = "常见误区",
                                Code = @"// ❌ 错误：以为 Send<int> 会触发无参监听者
EventKit.Enum.Register(GameEvent.Test, OnTest);  // 无参监听
EventKit.Enum.Send(GameEvent.Test, 100);         // 发送 int 参数
// OnTest 不会被调用！因为它们在不同通道

// ✅ 正确：参数类型必须匹配
EventKit.Enum.Register<GameEvent, int>(GameEvent.Test, OnTestInt);
EventKit.Enum.Send(GameEvent.Test, 100);  // OnTestInt 会被调用

// ✅ 正确：无参事件使用无参 Send
EventKit.Enum.Register(GameEvent.Test, OnTest);
EventKit.Enum.Send(GameEvent.Test);  // OnTest 会被调用",
                                Explanation = "Register 和 Send 的参数类型必须完全一致才能正确通信。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "编辑器工具",
                        Description = "EventKit 提供运行时事件查看器，可在 YokiFrame Tools 面板中查看所有事件的注册、触发和监听情况。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "打开事件查看器",
                                Code = @"// 快捷键：Ctrl+E 打开 YokiFrame Tools 面板
// 选择 EventKit 标签页

// 功能：
// - 实时查看所有事件的监听者数量
// - 查看事件历史记录（注册/注销/触发）
// - 代码扫描：查找项目中所有事件的使用位置
// - 支持枚举、类型、字符串三种事件类型",
                                Explanation = "事件查看器帮助调试事件系统，追踪事件流向和监听者。"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
