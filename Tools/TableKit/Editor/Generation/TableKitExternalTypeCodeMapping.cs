using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>描述一个由 Luban mapper 产生的 TableKit 外部类型转换方法。</summary>
    public sealed class TableKitExternalTypeCodeMapping
    {
        /// <summary>创建不可变的外部类型转换代码契约。</summary>
        /// <param name="sourceTypeName">Luban bean 完整类型表达式。</param>
        /// <param name="targetTypeName">目标类型表达式。</param>
        /// <param name="helperMethodName">生成的 helper 方法名。</param>
        /// <param name="memberNames">传给目标构造函数的 bean 成员名。</param>
        public TableKitExternalTypeCodeMapping(
            string sourceTypeName,
            string targetTypeName,
            string helperMethodName,
            IReadOnlyList<string> memberNames)
        {
            SourceTypeName = sourceTypeName ?? throw new ArgumentNullException(nameof(sourceTypeName));
            TargetTypeName = targetTypeName ?? throw new ArgumentNullException(nameof(targetTypeName));
            HelperMethodName = CodeGenKit.RequireIdentifier(helperMethodName, nameof(helperMethodName));
            MemberNames = memberNames ?? throw new ArgumentNullException(nameof(memberNames));
        }

        /// <summary>获取 Luban bean 完整类型表达式。</summary>
        public string SourceTypeName { get; }

        /// <summary>获取目标类型表达式。</summary>
        public string TargetTypeName { get; }

        /// <summary>获取生成的 helper 方法名。</summary>
        public string HelperMethodName { get; }

        /// <summary>获取传给目标构造函数的 bean 成员名。</summary>
        public IReadOnlyList<string> MemberNames { get; }
    }
}
