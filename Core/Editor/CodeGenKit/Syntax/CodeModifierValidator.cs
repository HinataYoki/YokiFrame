using System;

namespace YokiFrame
{
    /// <summary>
    /// 按声明类型拒绝明显无效或语义冲突的成员修饰符组合。
    /// </summary>
    internal static class CodeModifierValidator
    {
        private const MemberModifier FIELD_ALLOWED = MemberModifier.New | MemberModifier.Static
            | MemberModifier.Readonly | MemberModifier.Const;
        private const MemberModifier PROPERTY_ALLOWED = MemberModifier.New | MemberModifier.Static
            | MemberModifier.Virtual | MemberModifier.Override | MemberModifier.Abstract | MemberModifier.Sealed;
        private const MemberModifier METHOD_ALLOWED = MemberModifier.New | MemberModifier.Static
            | MemberModifier.Virtual | MemberModifier.Override | MemberModifier.Abstract | MemberModifier.Sealed
            | MemberModifier.Partial | MemberModifier.Async;

        /// <summary>
        /// 验证字段只使用字段允许的 flag，并拒绝 const 与 static/readonly 重复组合。
        /// </summary>
        /// <param name="modifiers">字段修饰符。</param>
        internal static void ValidateField(MemberModifier modifiers)
        {
            RequireAllowed(modifiers, FIELD_ALLOWED, "字段");
            if (Has(modifiers, MemberModifier.Const)
                && (Has(modifiers, MemberModifier.Static) || Has(modifiers, MemberModifier.Readonly)))
            {
                throw new InvalidOperationException("const 字段不能再声明 static 或 readonly。");
            }
        }

        /// <summary>
        /// 验证属性修饰符，并约束 virtual/override/abstract/sealed 的组合语义。
        /// </summary>
        /// <param name="modifiers">属性修饰符。</param>
        internal static void ValidateProperty(MemberModifier modifiers)
        {
            RequireAllowed(modifiers, PROPERTY_ALLOWED, "属性");
            ValidatePolymorphicModifiers(modifiers, "属性");
        }

        /// <summary>
        /// 验证方法修饰符，并拒绝静态多态、抽象异步等无效组合。
        /// </summary>
        /// <param name="modifiers">方法修饰符。</param>
        internal static void ValidateMethod(MemberModifier modifiers)
        {
            RequireAllowed(modifiers, METHOD_ALLOWED, "方法");
            ValidatePolymorphicModifiers(modifiers, "方法");
            if (Has(modifiers, MemberModifier.Static) && HasAnyPolymorphic(modifiers))
            {
                throw new InvalidOperationException("static 方法不能声明 virtual、override 或 abstract。");
            }

            if (Has(modifiers, MemberModifier.Abstract) && Has(modifiers, MemberModifier.Async))
            {
                throw new InvalidOperationException("abstract 方法不能声明 async。");
            }
        }

        /// <summary>
        /// 验证给定组合没有声明当前上下文之外的 flag。
        /// </summary>
        /// <param name="modifiers">实际修饰符。</param>
        /// <param name="allowed">当前声明允许的全部 flag。</param>
        /// <param name="declarationName">用于错误消息的声明类别。</param>
        private static void RequireAllowed(MemberModifier modifiers, MemberModifier allowed, string declarationName)
        {
            MemberModifier unsupported = modifiers & ~allowed;
            if (unsupported != MemberModifier.None)
            {
                throw new InvalidOperationException(declarationName + "不支持修饰符: " + unsupported);
            }
        }

        /// <summary>
        /// 约束多态 flag 互斥，并要求 sealed 只与 override 一起出现。
        /// </summary>
        /// <param name="modifiers">实际修饰符。</param>
        /// <param name="declarationName">用于错误消息的声明类别。</param>
        private static void ValidatePolymorphicModifiers(MemberModifier modifiers, string declarationName)
        {
            int count = Count(modifiers, MemberModifier.Virtual)
                + Count(modifiers, MemberModifier.Override)
                + Count(modifiers, MemberModifier.Abstract);
            if (count > 1)
            {
                throw new InvalidOperationException(declarationName + "不能同时声明 virtual、override 和 abstract。");
            }

            if (Has(modifiers, MemberModifier.Sealed) && !Has(modifiers, MemberModifier.Override))
            {
                throw new InvalidOperationException("sealed " + declarationName + "必须同时声明 override。");
            }
        }

        /// <summary>
        /// 判断组合是否包含任一多态方法 flag。
        /// </summary>
        /// <param name="modifiers">实际修饰符。</param>
        /// <returns>包含 virtual、override 或 abstract 时返回 true。</returns>
        private static bool HasAnyPolymorphic(MemberModifier modifiers)
        {
            return Has(modifiers, MemberModifier.Virtual)
                || Has(modifiers, MemberModifier.Override)
                || Has(modifiers, MemberModifier.Abstract);
        }

        /// <summary>
        /// 将指定 flag 是否存在转换为计数值。
        /// </summary>
        /// <param name="modifiers">实际修饰符。</param>
        /// <param name="flag">目标 flag。</param>
        /// <returns>存在时为 1，否则为 0。</returns>
        private static int Count(MemberModifier modifiers, MemberModifier flag)
        {
            return Has(modifiers, flag) ? 1 : 0;
        }

        /// <summary>
        /// 判断组合是否包含指定 flag。
        /// </summary>
        /// <param name="modifiers">实际修饰符。</param>
        /// <param name="flag">目标 flag。</param>
        /// <returns>包含时返回 true。</returns>
        private static bool Has(MemberModifier modifiers, MemberModifier flag)
        {
            return (modifiers & flag) != 0;
        }
    }
}
