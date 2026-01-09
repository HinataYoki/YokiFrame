#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// CodeGenKit 核心概念文档
    /// </summary>
    internal static class CodeGenKitDocConcept
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "核心概念",
                Description = "CodeGenKit 是一个 C# 代码生成工具，使用树形结构组织代码，支持 Fluent API 风格的链式调用。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "设计理念",
                        Code = @"// CodeGenKit 的核心设计理念：

// 1. 树形结构
// 代码被组织成树形结构，RootCode 是根节点
// 每个节点实现 ICode 接口，可以生成对应的代码文本
// ICodeScope 是特殊的 ICode，可以包含子节点

// 2. Fluent API
// 所有配置方法返回 this，支持链式调用
// 例如: field.WithAccess(AccessModifier.Private).WithComment(""注释"")

// 3. 扩展方法
// 通过 ICodeScopeExtensions 提供大量扩展方法
// 简化常见代码模式的生成

// 4. 分离关注点
// 代码结构（ICode/ICodeScope）与输出（ICodeWriteKit）分离
// 可以输出到文件或字符串",
                        Explanation = "理解这些设计理念有助于更好地使用 CodeGenKit。"
                    },
                    new()
                    {
                        Title = "核心接口",
                        Code = @"// ICode - 代码片段接口
// 所有代码类型都实现此接口
public interface ICode
{
    void Gen(ICodeWriteKit writer);  // 生成代码到写入器
}

// ICodeScope - 代码作用域接口
// 可以包含子代码的代码类型（如类、命名空间）
public interface ICodeScope : ICode
{
    List<ICode> Codes { get; set; }  // 子代码列表
}

// ICodeWriteKit - 代码写入器接口
public interface ICodeWriteKit
{
    int IndentCount { get; set; }    // 当前缩进级别
    void WriteLine(string line);     // 写入一行代码
}",
                        Explanation = "这三个接口是 CodeGenKit 的核心，所有功能都基于它们构建。"
                    },
                    new()
                    {
                        Title = "代码类型层次",
                        Code = @"// CodeGenKit 提供的代码类型：

// 基础代码（ICode）
// ├── UsingCode          - using 声明
// ├── EmptyLineCode      - 空行
// ├── CustomCode         - 自定义代码行
// ├── CommentCode        - 注释
// ├── AttributeCode      - 特性
// ├── FieldCode          - 字段
// ├── PropertyCode       - 属性
// └── MethodCode         - 方法

// 作用域代码（ICodeScope）
// ├── RootCode           - 根节点
// ├── NamespaceCodeScope - 命名空间
// ├── ClassCodeScope     - 类
// └── CustomCodeScope    - 自定义作用域（if/for/while等）",
                        Explanation = "了解代码类型层次有助于选择合适的类型。"
                    },
                    new()
                    {
                        Title = "基本使用流程",
                        Code = @"// 1. 创建根节点
var root = new RootCode();

// 2. 添加 using 声明
root.Codes.Add(new UsingCode(""System""));
root.Codes.Add(new UsingCode(""UnityEngine""));
root.Codes.Add(new EmptyLineCode());

// 3. 添加命名空间
root.Namespace(""MyGame"", ns =>
{
    // 4. 在命名空间内添加类
    ns.Class(""MyClass"", ""MonoBehaviour"", true, false, cls =>
    {
        // 5. 在类内添加成员
        cls.SerializeField(""int"", ""mValue"", ""数值"");
        cls.VoidMethod(""Start"", m => m.WithBody(b => b.Custom(""// 初始化"")));
    });
});

// 6. 输出到文件
using var writer = new FileCodeWriteKit(""Assets/Scripts/MyClass.cs"");
root.Gen(writer);

// 或输出到字符串（用于测试）
var stringWriter = new StringCodeWriteKit();
root.Gen(stringWriter);
string code = stringWriter.ToString();",
                        Explanation = "这是使用 CodeGenKit 的标准流程。"
                    },
                    new()
                    {
                        Title = "目录结构",
                        Code = @"// CodeGenKit 源码目录结构：
// Assets/YokiFrame/Core/Kit/CodeGenKit/Editor/
// ├── Code/              - 基础代码类型
// │   ├── ICode.cs       - 代码接口
// │   ├── UsingCode.cs   - using 声明
// │   ├── EmptyLineCode.cs
// │   ├── CustomCode.cs
// │   └── CommentCode.cs
// ├── CodeAttribute/     - 特性代码
// │   └── AttributeCode.cs
// ├── CodeMember/        - 成员代码
// │   ├── FieldCode.cs   - 字段
// │   ├── PropertyCode.cs - 属性
// │   └── MethodCode.cs  - 方法
// ├── CodeScope/         - 作用域代码
// │   ├── ICodeScope.cs  - 作用域接口
// │   ├── RootCode.cs    - 根节点
// │   ├── NamespaceCodeScope.cs
// │   ├── ClassCodeScope.cs
// │   └── CustomCodeScope.cs
// ├── CodeWriter/        - 写入器
// │   ├── ICodeWriteKit.cs
// │   ├── FileCodeWriteKit.cs
// │   └── StringCodeWriteKit.cs
// └── Common/            - 通用类型
//     └── AccessModifier.cs - 修饰符枚举",
                        Explanation = "了解目录结构有助于查找和扩展功能。"
                    }
                }
            };
        }
    }
}
#endif
