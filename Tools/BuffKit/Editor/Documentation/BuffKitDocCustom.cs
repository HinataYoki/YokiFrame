#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// BuffKit 自定义 Buff 行为文档
    /// </summary>
    internal static class BuffKitDocCustom
    {
        internal static DocSection CreateSection()
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

container.Add(new PoisonBuff(5f));",
                        Explanation = "继承 BaseBuff 实现自定义 Buff 逻辑。"
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
                        Explanation = "将效果逻辑放在 IBuffEffect 中，实现关注点分离。"
                    }
                }
            };
        }
    }
}
#endif
