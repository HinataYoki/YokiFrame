#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// CodeGenKit 内置代码类型文档
    /// </summary>
    internal static class CodeGenKitDocTypes
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "内置代码类型",
                Description = "CodeGenKit 提供多种内置的代码类型，包括基础代码、作用域代码、成员代码、特性代码等。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "基础代码类型",
                        Code = @"// 基础代码元素
new UsingCode(""System"");           // 生成: using System;
new UsingCode(""UnityEngine"");      // 生成: using UnityEngine;
new EmptyLineCode();                 // 生成: 空行
new CustomCode(""// 自定义代码"");   // 生成: // 自定义代码

// 花括号（通常由作用域自动管理）
new OpenBraceCode();                 // 生成: {
new CloseBraceCode();                // 生成: }",
                        Explanation = "基础代码类型是构建代码结构的最小单元。"
                    },
                    new()
                    {
                        Title = "作用域代码类型",
                        Code = @"// NamespaceCodeScope - 命名空间作用域
root.Namespace(""MyGame"", ns =>
{
    // 命名空间内的代码...
});
// 生成:
// namespace MyGame
// {
//     ...
// }

// ClassCodeScope - 类作用域
ns.Class(""PlayerController"", ""MonoBehaviour"", true, false, cls =>
{
    // 类内的代码...
});
// 参数: 类名, 基类, 是否 partial, 是否 static

// 带配置的类
ns.Class(""GameManager"", cls =>
{
    cls.WithAccess(AccessModifier.Public)
       .WithInterface(""IDisposable"")
       .WithAttribute(""Serializable"");
}, cls =>
{
    // 类体内容...
});

// CustomCodeScope - 自定义作用域（用于 if/for/while 等）
cls.Codes.Add(new CustomCodeScope(""if (condition)""));",
                        Explanation = "作用域代码自动管理花括号和缩进，支持嵌套。"
                    },
                    new()
                    {
                        Title = "访问修饰符枚举",
                        Code = @"// AccessModifier - 访问修饰符
public enum AccessModifier
{
    None,              // 无修饰符（用于接口成员）
    Public,            // public
    Private,           // private
    Protected,         // protected
    Internal,          // internal
    ProtectedInternal, // protected internal
    PrivateProtected   // private protected
}

// 使用示例
cls.WithAccess(AccessModifier.Public);
field.WithAccess(AccessModifier.Private);
method.WithAccess(AccessModifier.Protected);",
                        Explanation = "AccessModifier 枚举提供类型安全的访问修饰符设置。"
                    },
                    new()
                    {
                        Title = "成员修饰符枚举",
                        Code = @"// MemberModifier - 成员修饰符（Flags 枚举，可组合）
[Flags]
public enum MemberModifier
{
    None     = 0,
    Static   = 1 << 0,   // static
    Readonly = 1 << 1,   // readonly
    Const    = 1 << 2,   // const
    Virtual  = 1 << 3,   // virtual
    Override = 1 << 4,   // override
    Abstract = 1 << 5,   // abstract
    Sealed   = 1 << 6,   // sealed
    Partial  = 1 << 7,   // partial
    Async    = 1 << 8,   // async
    New      = 1 << 9    // new
}

// 组合使用示例
field.WithModifiers(MemberModifier.Static | MemberModifier.Readonly);
// 生成: static readonly

method.WithModifiers(MemberModifier.Override | MemberModifier.Async);
// 生成: override async",
                        Explanation = "MemberModifier 是 Flags 枚举，支持位运算组合多个修饰符。"
                    },
                    new()
                    {
                        Title = "写入器类型",
                        Code = @"// ICodeWriteKit - 写入器接口
public interface ICodeWriteKit
{
    int IndentCount { get; set; }  // 缩进级别
    void WriteLine(string line);   // 写入一行
}

// FileCodeWriteKit - 文件写入器
// 将生成的代码写入文件
using var writer = new FileCodeWriteKit(filePath);
root.Gen(writer);

// StringCodeWriteKit - 字符串写入器
// 将生成的代码输出到字符串（用于测试或预览）
var stringWriter = new StringCodeWriteKit();
root.Gen(stringWriter);
string code = stringWriter.ToString();

// 写入器特性：
// - 自动管理缩进（预生成缩进字符串数组优化性能）
// - 自动处理换行
// - 支持 using 语句自动释放资源",
                        Explanation = "写入器负责将代码结构输出为文本，支持文件和字符串两种目标。"
                    }
                }
            };
        }
    }
}
#endif
