using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 访问修饰符。
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
    /// 成员修饰符，可组合。
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
    /// 修饰符字符串转换工具。
    /// </summary>
    public static class ModifierHelper
    {
        public static string GetAccessString(AccessModifier access)
        {
            switch (access)
            {
                case AccessModifier.Public:
                    return "public ";
                case AccessModifier.Private:
                    return "private ";
                case AccessModifier.Protected:
                    return "protected ";
                case AccessModifier.Internal:
                    return "internal ";
                case AccessModifier.ProtectedInternal:
                    return "protected internal ";
                case AccessModifier.PrivateProtected:
                    return "private protected ";
                default:
                    return string.Empty;
            }
        }

        public static string GetMemberString(MemberModifier modifiers)
        {
            if (modifiers == MemberModifier.None)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(32);
            if ((modifiers & MemberModifier.New) != 0) builder.Append("new ");
            if ((modifiers & MemberModifier.Static) != 0) builder.Append("static ");
            if ((modifiers & MemberModifier.Const) != 0) builder.Append("const ");
            if ((modifiers & MemberModifier.Readonly) != 0) builder.Append("readonly ");
            if ((modifiers & MemberModifier.Virtual) != 0) builder.Append("virtual ");
            if ((modifiers & MemberModifier.Abstract) != 0) builder.Append("abstract ");
            if ((modifiers & MemberModifier.Override) != 0) builder.Append("override ");
            if ((modifiers & MemberModifier.Sealed) != 0) builder.Append("sealed ");
            if ((modifiers & MemberModifier.Partial) != 0) builder.Append("partial ");
            if ((modifiers & MemberModifier.Async) != 0) builder.Append("async ");
            return builder.ToString();
        }
    }
}
