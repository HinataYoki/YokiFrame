using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 访问修饰符
    /// </summary>
    public enum AccessModifier
    {
        /// <summary>
        /// 无修饰符（用于接口成员等）
        /// </summary>
        None,
        Public,
        Private,
        Protected,
        Internal,
        ProtectedInternal,
        PrivateProtected
    }

    /// <summary>
    /// 成员修饰符（可组合）
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
    /// 修饰符转换工具
    /// </summary>
    public static class ModifierHelper
    {
        /// <summary>
        /// 缓存的 StringBuilder，避免重复分配
        /// </summary>
        private static readonly StringBuilder sBuilder = new(32);

        /// <summary>
        /// 获取访问修饰符字符串
        /// </summary>
        public static string GetAccessString(AccessModifier access)
        {
            return access switch
            {
                AccessModifier.None => string.Empty,
                AccessModifier.Public => "public ",
                AccessModifier.Private => "private ",
                AccessModifier.Protected => "protected ",
                AccessModifier.Internal => "internal ",
                AccessModifier.ProtectedInternal => "protected internal ",
                AccessModifier.PrivateProtected => "private protected ",
                _ => string.Empty
            };
        }

        /// <summary>
        /// 获取成员修饰符字符串
        /// </summary>
        public static string GetMemberString(MemberModifier modifiers)
        {
            if (modifiers == MemberModifier.None)
                return string.Empty;

            sBuilder.Clear();

            if ((modifiers & MemberModifier.New) != 0) sBuilder.Append("new ");
            if ((modifiers & MemberModifier.Static) != 0) sBuilder.Append("static ");
            if ((modifiers & MemberModifier.Const) != 0) sBuilder.Append("const ");
            if ((modifiers & MemberModifier.Readonly) != 0) sBuilder.Append("readonly ");
            if ((modifiers & MemberModifier.Virtual) != 0) sBuilder.Append("virtual ");
            if ((modifiers & MemberModifier.Abstract) != 0) sBuilder.Append("abstract ");
            if ((modifiers & MemberModifier.Override) != 0) sBuilder.Append("override ");
            if ((modifiers & MemberModifier.Sealed) != 0) sBuilder.Append("sealed ");
            if ((modifiers & MemberModifier.Partial) != 0) sBuilder.Append("partial ");
            if ((modifiers & MemberModifier.Async) != 0) sBuilder.Append("async ");

            return sBuilder.ToString();
        }
    }
}
