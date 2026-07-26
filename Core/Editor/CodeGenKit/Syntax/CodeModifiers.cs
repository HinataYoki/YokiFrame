using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 表示 C# 声明的访问级别。
    /// </summary>
    public enum AccessModifier
    {
        None,
        Public,
        Private,
        Protected,
        Internal,
        ProtectedInternal,
        PrivateProtected
    }

    /// <summary>
    /// 表示可按声明类型受控组合的 C# 成员修饰符。
    /// </summary>
    [Flags]
    public enum MemberModifier
    {
        None = 0,
        Static = 1 << 0,
        Readonly = 1 << 1,
        Const = 1 << 2,
        Virtual = 1 << 3,
        Override = 1 << 4,
        Abstract = 1 << 5,
        Sealed = 1 << 6,
        Partial = 1 << 7,
        Async = 1 << 8,
        New = 1 << 9
    }

    /// <summary>
    /// 将已验证的修饰符转换为具有稳定顺序的 C# 文本。
    /// </summary>
    internal static class CodeModifierText
    {
        /// <summary>
        /// 返回访问修饰符文本并保留末尾空格，未知枚举值会被拒绝。
        /// </summary>
        /// <param name="access">访问修饰符。</param>
        /// <returns>可直接拼接到声明头部的文本。</returns>
        internal static string GetAccessText(AccessModifier access)
        {
            switch (access)
            {
                case AccessModifier.None: return string.Empty;
                case AccessModifier.Public: return "public ";
                case AccessModifier.Private: return "private ";
                case AccessModifier.Protected: return "protected ";
                case AccessModifier.Internal: return "internal ";
                case AccessModifier.ProtectedInternal: return "protected internal ";
                case AccessModifier.PrivateProtected: return "private protected ";
                default: throw new ArgumentOutOfRangeException(nameof(access), access, "未知访问修饰符。");
            }
        }

        /// <summary>
        /// 按固定 C# 声明顺序输出成员修饰符，调用方必须先完成上下文校验。
        /// </summary>
        /// <param name="modifiers">已验证的组合修饰符。</param>
        /// <returns>可直接拼接到声明头部的文本。</returns>
        internal static string GetMemberText(MemberModifier modifiers)
        {
            if (modifiers == MemberModifier.None)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(48);
            Append(builder, modifiers, MemberModifier.New, "new ");
            Append(builder, modifiers, MemberModifier.Static, "static ");
            Append(builder, modifiers, MemberModifier.Const, "const ");
            Append(builder, modifiers, MemberModifier.Readonly, "readonly ");
            Append(builder, modifiers, MemberModifier.Virtual, "virtual ");
            Append(builder, modifiers, MemberModifier.Abstract, "abstract ");
            Append(builder, modifiers, MemberModifier.Sealed, "sealed ");
            Append(builder, modifiers, MemberModifier.Override, "override ");
            Append(builder, modifiers, MemberModifier.Async, "async ");
            Append(builder, modifiers, MemberModifier.Partial, "partial ");
            return builder.ToString();
        }

        /// <summary>
        /// 在组合包含目标 flag 时追加对应文本，集中保持输出顺序。
        /// </summary>
        /// <param name="builder">目标文本构建器。</param>
        /// <param name="modifiers">完整修饰符组合。</param>
        /// <param name="flag">当前检查的 flag。</param>
        /// <param name="text">命中时追加的文本。</param>
        private static void Append(StringBuilder builder, MemberModifier modifiers, MemberModifier flag, string text)
        {
            if ((modifiers & flag) != 0)
            {
                builder.Append(text);
            }
        }

    }
}
