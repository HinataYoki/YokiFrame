#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// BuffKit 属性修改器文档
    /// </summary>
    internal static class BuffKitDocModifier
    {
        internal static DocSection CreateSection()
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
}",
                        Explanation = "使用 int 常量定义属性 ID，避免魔法值。"
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
    }
}
#endif
