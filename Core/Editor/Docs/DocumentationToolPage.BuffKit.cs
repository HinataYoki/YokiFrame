#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    public partial class DocumentationToolPage
    {
        private DocModule CreateBuffKitDoc()
        {
            return new DocModule
            {
                Name = "BuffKit",
                Icon = KitIcons.BUFFKIT,
                Category = "TOOLS",
                Description = "通用 Buff 系统，提供完整的 Buff 生命周期管理、堆叠模式、属性修改、免疫系统和序列化支持。",
                Keywords = new List<string> { "Buff", "增益减益", "属性修改", "堆叠" },
                Sections = new List<DocSection>
                {
                    CreateBuffKitQuickStartSection(),
                    CreateBuffKitStackModeSection(),
                    CreateBuffKitQuerySection(),
                    CreateBuffKitImmunitySection(),
                    CreateBuffKitModifierSection(),
                    CreateBuffKitCustomBuffSection(),
                    CreateBuffKitEventSection(),
                    CreateBuffKitSerializationSection(),
                    CreateBuffKitCharacterSection(),
                    CreateBuffKitEditorSection()
                }
            };
        }

        private DocSection CreateBuffKitQuickStartSection()
        {
            return new DocSection
            {
                Title = "快速开始",
                Description = "BuffKit 提供简洁的 Buff 管理 API。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "注册 Buff 配置",
                        Code = @"// 使用 BuffData 静态工厂
BuffKit.RegisterBuffData(BuffData.Create(
    buffId: 1001,
    duration: 10f,
    maxStack: 5,
    stackMode: StackMode.Stack
));

// 链式配置
BuffKit.RegisterBuffData(
    BuffData.Create(1002, 5f)
        .WithTags(100, 101)
        .WithExclusionTags(200)
        .WithTickInterval(1f)
);",
                        Explanation = "使用 int ID 作为 Buff 标识，避免魔法字符串。"
                    },
                    new()
                    {
                        Title = "创建容器并添加 Buff",
                        Code = @"// 创建容器
var container = BuffKit.CreateContainer();

// 添加 Buff
container.Add(1001);
container.Add(1001, 3);  // 添加指定层数

// 更新时间
container.Update(Time.deltaTime);

// 释放
container.Dispose();",
                        Explanation = "每个实体创建一个容器，销毁时调用 Dispose()。"
                    }
                }
            };
        }

        private DocSection CreateBuffKitStackModeSection()
        {
            return new DocSection
            {
                Title = "堆叠模式",
                Description = "三种堆叠模式满足不同需求。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "Independent - 独立模式",
                        Code = @"// 每次添加创建新实例
BuffData.Create(1001, 10f, 99, StackMode.Independent);

var results = new List<BuffInstance>();
container.GetAll(1001, results);"
                    },
                    new()
                    {
                        Title = "Refresh - 刷新模式",
                        Code = @"// 重复添加只刷新持续时间
BuffData.Create(1002, 10f, 1, StackMode.Refresh);

container.Add(1002);  // 创建实例
container.Add(1002);  // 刷新时间"
                    },
                    new()
                    {
                        Title = "Stack - 堆叠模式",
                        Code = @"// 一个实例，多层堆叠
BuffData.Create(1003, 10f, 5, StackMode.Stack);

container.Add(1003);  // 1 层
container.Add(1003);  // 2 层
int stacks = container.GetStackCount(1003);",
                        Explanation = "达到最大堆叠数后只刷新时间。"
                    }
                }
            };
        }

        private DocSection CreateBuffKitQuerySection()
        {
            return new DocSection
            {
                Title = "查询与移除",
                Description = "丰富的查询和移除 API。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "查询 Buff",
                        Code = @"bool hasBuff = container.Has(1001);
BuffInstance instance = container.Get(1001);

var results = new List<BuffInstance>();
container.GetAll(1001, results);
container.GetByTag(100, results);

int stacks = container.GetStackCount(1001);
int count = container.Count;"
                    },
                    new()
                    {
                        Title = "移除 Buff",
                        Code = @"container.Remove(1001);
container.RemoveInstance(instance);
int removed = container.RemoveByTag(100);
container.Clear();"
                    }
                }
            };
        }

        private DocSection CreateBuffKitImmunitySection()
        {
            return new DocSection
            {
                Title = "免疫与排斥",
                Description = "控制 Buff 的添加规则。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "免疫系统",
                        Code = @"// 添加免疫标签
container.AddImmunity(100);

// 带标签 100 的 Buff 无法添加
var buff = BuffData.Create(1001, 10f).WithTags(100);
bool added = container.Add(buff);  // false

// 检查和移除免疫
bool isImmune = container.IsImmune(100);
container.RemoveImmunity(100);",
                        Explanation = "免疫系统用于实现无敌状态、控制免疫等。"
                    },
                    new()
                    {
                        Title = "排斥标签",
                        Code = @"// 添加此 Buff 时移除带指定标签的现有 Buff
var fireBuff = BuffData.Create(1001, 10f)
    .WithTags(100)
    .WithExclusionTags(200);

var iceBuff = BuffData.Create(1002, 10f)
    .WithTags(200);

container.Add(iceBuff);
container.Add(fireBuff);  // 自动移除冰系",
                        Explanation = "排斥标签用于实现互斥效果。"
                    }
                }
            };
        }

        private DocSection CreateBuffKitModifierSection()
        {
            return new DocSection
            {
                Title = "属性修改器",
                Description = "Buff 对属性值的影响计算。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "定义属性 ID",
                        Code = @"public static class AttributeId
{
    public const int Attack = 1;
    public const int Defense = 2;
    public const int Speed = 3;
}"
                    },
                    new()
                    {
                        Title = "添加修改器",
                        Code = @"public class AttackBuff : BaseBuff
{
    public AttackBuff() : base(
        BuffData.Create(1001, 10f, 5, StackMode.Stack))
    {
        AddModifier(new BuffModifier(
            AttributeId.Attack, 
            ModifierType.Additive, 
            10f));
    }
}

// 计算修改后的值
float finalAttack = container.GetModifiedValue(
    AttributeId.Attack, baseAttack);",
                        Explanation = "修改器支持堆叠层数乘数。"
                    },
                    new()
                    {
                        Title = "修改器类型",
                        Code = @"// Additive: 基础值 + 修改值
new BuffModifier(attr, ModifierType.Additive, 10f);

// Multiplicative: 结果 * (1 + 修改值)
new BuffModifier(attr, ModifierType.Multiplicative, 0.5f);

// Override: 直接覆盖
new BuffModifier(attr, ModifierType.Override, 999f);",
                        Explanation = "计算顺序: Additive -> Multiplicative -> Override"
                    }
                }
            };
        }

        private DocSection CreateBuffKitCustomBuffSection()
        {
            return new DocSection
            {
                Title = "自定义 Buff 行为",
                Description = "实现复杂的 Buff 逻辑。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "继承 BaseBuff",
                        Code = @"public class PoisonBuff : BaseBuff
{
    private readonly float mDamage;

    public PoisonBuff(float damage) : base(
        BuffData.Create(2001, 10f, 3, StackMode.Stack)
            .WithTickInterval(1f)
            .WithTags(100))
    {
        mDamage = damage;
    }

    public override void OnTick(BuffContainer container, 
        BuffInstance instance)
    {
        float totalDamage = mDamage * instance.StackCount;
        // ApplyDamage(totalDamage);
    }
}

container.Add(new PoisonBuff(5f));"
                    },
                    new()
                    {
                        Title = "实现 IBuffEffect",
                        Code = @"public class StunEffect : IBuffEffect
{
    public void OnApply(BuffContainer container, 
        BuffInstance instance)
    {
        // 禁用输入
    }

    public void OnRemove(BuffContainer container, 
        BuffInstance instance)
    {
        // 恢复输入
    }

    public void OnStackChanged(BuffContainer container, 
        BuffInstance instance, int oldStack, int newStack) { }
}

public class StunBuff : BaseBuff
{
    public StunBuff() : base(BuffData.Create(4001, 3f))
    {
        AddEffect(new StunEffect());
    }
}",
                        Explanation = "将效果逻辑放在 IBuffEffect 中。"
                    }
                }
            };
        }

        private DocSection CreateBuffKitEventSection()
        {
            return new DocSection
            {
                Title = "事件监听",
                Description = "监听 Buff 生命周期事件。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "注册事件",
                        Code = @"// 监听 Buff 添加
EventKit.Type.Register<BuffAddedEvent>(e =>
{
    Debug.Log(e.Instance.BuffId);
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 监听 Buff 移除
EventKit.Type.Register<BuffRemovedEvent>(e =>
{
    Debug.Log(e.Reason);
});

// 监听堆叠变化
EventKit.Type.Register<BuffStackChangedEvent>(e =>
{
    Debug.Log(e.OldStack + "" -> "" + e.NewStack);
});",
                        Explanation = "事件通过 EventKit 发送。"
                    }
                }
            };
        }

        private DocSection CreateBuffKitSerializationSection()
        {
            return new DocSection
            {
                Title = "序列化存档",
                Description = "保存和恢复 Buff 状态。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "导出和恢复",
                        Code = @"// 导出存档数据
BuffContainerSaveData saveData = container.ToSaveData();

// 存入存档系统
var gameSave = SaveKit.CreateSaveData();
gameSave.SetModule(ModuleId.Buff, saveData);
SaveKit.Save(0, gameSave);

// 从存档恢复
var loadedSave = SaveKit.Load(0);
var buffSaveData = loadedSave
    .GetModule<BuffContainerSaveData>(ModuleId.Buff);
container.FromSaveData(buffSaveData);",
                        Explanation = "保存 Buff ID、剩余时间、堆叠数和免疫标签。"
                    }
                }
            };
        }

        private DocSection CreateBuffKitCharacterSection()
        {
            return new DocSection
            {
                Title = "实战：角色 Buff 系统",
                Description = "将 BuffKit 集成到角色系统中。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "角色类持有 BuffContainer",
                        Code = @"public class Character : IDisposable
{
    private readonly CharacterData mData;
    private readonly BuffContainer mBuffContainer;
    
    public Character(CharacterData data)
    {
        mData = data;
        mBuffContainer = BuffKit.CreateContainer();
    }
    
    // 计算最终属性
    public float Attack => mBuffContainer.GetModifiedValue(
        AttributeId.Attack, mData.BaseAttack);
    
    // 添加 Buff
    public bool AddBuff(int buffId)
    {
        return mBuffContainer.Add(buffId);
    }
    
    public bool AddBuff(IBuff buff)
    {
        return mBuffContainer.Add(buff);
    }
    
    // 每帧更新
    public void Update(float deltaTime)
    {
        mBuffContainer.Update(deltaTime);
    }
    
    // 释放资源
    public void Dispose()
    {
        mBuffContainer.Dispose();
    }
}",
                        Explanation = "角色持有容器，在 Update 中更新 Buff 时间。"
                    },
                    new()
                    {
                        Title = "使用示例",
                        Code = @"// 创建角色
var player = new Character(playerData);

// 给敌人上毒
enemy.AddBuff(new PoisonBuff(10f));

// 使用增益技能
player.AddBuff(new AttackUpBuff(0.3f));

// 添加控制免疫
player.AddImmunity(BuffTag.Control);

// 游戏循环
void Update()
{
    player.Update(Time.deltaTime);
    enemy.Update(Time.deltaTime);
}

// 销毁时释放
player.Dispose();",
                        Explanation = "BuffKit 通过 BuffContainer 与角色关联。"
                    }
                }
            };
        }

        private DocSection CreateBuffKitEditorSection()
        {
            return new DocSection
            {
                Title = "编辑器工具",
                Description = "运行时调试工具。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "工具入口",
                        Code = @"// YokiFrame 工具面板: Ctrl+E
// 独立窗口: YokiFrame > BuffKit Viewer

// 功能:
// - 查看所有活跃的 BuffContainer
// - 实时监控 Buff 状态
// - 显示 Buff ID、堆叠数、剩余时间",
                        Explanation = "仅在 Play 模式下显示运行时数据。"
                    }
                }
            };
        }
    }
}
#endif
