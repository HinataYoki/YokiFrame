# CodeGenKit 代码生成

CodeGenKit 是纯 C# 代码生成工具，用于生成格式化的 C# 源代码。它将 C# 语法元素（命名空间、类、字段、方法、属性、注解、注释、using 指令）建模为 `ICode` 节点树，通过 writer 渲染为带缩进的代码字符串或文件。

CodeGenKit 位于 Editor 层（`YokiFrame.CodeGenKit` 程序集），不依赖 Unity 引擎 API，可作为通用 C# 代码构建器使用。

## 核心类型

| 类型 | 说明 |
|------|------|
| `CodeGenKit` | 静态入口，提供 `Root()`、`GenerateToString()`、`GenerateToFile()` 等方法。 |
| `ICode` | 代码节点接口，所有代码元素的基础。 |
| `ICodeScope` | 代码作用域接口，可包含子节点列表。 |
| `ICodeWriteKit` | 代码写入器接口，负责缩进和输出。 |
| `RootCode` | 根作用域，顶层容器，无花括号。 |

## 快速使用

### 结构化 AST 方式

使用 fluent API 构建代码树：

```csharp
using YokiFrame;

string code = CodeGenKit.GenerateToString(root =>
{
    root.Using("UnityEngine");
    root.Using("YokiFrame");
    root.EmptyLine();
    root.Namespace("Game.Logic", ns =>
    {
        ns.Class("PlayerController", cls =>
        {
            cls.WithAccess(AccessModifier.Public);
            cls.AsSealed();
            cls.WithInterface("IPlayerController");
            cls.EmptyLine();
            cls.Summary("玩家控制器");
            cls.PrivateField("int", "mHealth", "100");
            cls.EmptyLine();
            cls.PublicField("string", "PlayerName", "玩家名称");
            cls.EmptyLine();
            cls.AutoProperty("int", "Health", hasSetter: false, "当前生命值");
            cls.EmptyLine();
            cls.VoidMethod("TakeDamage", method =>
            {
                method.WithAccess(AccessModifier.Public);
                method.WithParameter("int", "damage", comment: "伤害值");
                method.WithBody(body =>
                {
                    body.Custom("mHealth = Mathf.Max(0, mHealth - damage);");
                });
            });
        });
    });
});
```

### 模板方式

使用 `CodeGenLineBuilder` 逐行构建：

```csharp
CodeGenKit.GenerateToFile("Assets/Scripts/Generated.cs", root =>
{
    root.Using("System");
    root.EmptyLine();
    var sb = CodeGenKit.Lines(root);
    sb.AppendLine("namespace Generated");
    sb.AppendLine("{");
    sb.AppendLine("    public static class Constants");
    sb.AppendLine("    {");
    sb.AppendLine("        public const int MaxLevel = 100;");
    sb.AppendLine("    }");
    sb.AppendLine("}");
    sb.Flush();
});
```

## 静态入口 API

| 方法 | 说明 |
|------|------|
| `Root()` | 创建新的根作用域。 |
| `Lines(scope)` | 创建逐行构建器，用于模板式代码生成。 |
| `GenerateToString(build, capacity)` | 构建代码并返回字符串。 |
| `GenerateToFile(filePath, build)` | 构建代码并写入文件。 |
| `WriteToFile(filePath, root)` | 将已构建的 `RootCode` 写入文件。 |

## 代码节点

### 基础节点

| 节点 | 说明 | 输出示例 |
|------|------|----------|
| `UsingCode` | using 指令 | `using UnityEngine;` |
| `CommentCode` | 注释 | `// 注释` |
| `CustomCode` | 自定义行 | 任意文本 |
| `EmptyLineCode` | 空行 | |
| `OpenBraceCode` | 开花括号 | `{` |
| `CloseBraceCode` | 闭花括号 | `}` 或 `};` |
| `AttributeCode` | 特性 | `[SerializeField]` |

### 成员节点

| 节点 | 说明 | 输出示例 |
|------|------|----------|
| `FieldCode` | 字段 | `private int mHealth = 100;` |
| `MethodCode` | 方法 | `public void TakeDamage(int damage) { ... }` |
| `PropertyCode` | 属性 | `public int Health { get; }` |

### 作用域节点

| 节点 | 说明 | 输出示例 |
|------|------|----------|
| `RootCode` | 根作用域 | 无花括号，直接输出子节点 |
| `NamespaceCodeScope` | 命名空间 | `namespace X { ... }` |
| `ClassCodeScope` | 类 | `public sealed class X : Y { ... }` |
| `CustomCodeScope` | 自定义作用域 | 任意首行 + `{ ... }` |

## Fluent API 扩展方法

### 基础操作

```csharp
scope.Using("UnityEngine");           // using 指令
scope.EmptyLine();                     // 空行
scope.Custom("任意代码行");             // 自定义行
```

### 注释

```csharp
scope.Comment("单行注释");
scope.Summary("XML 文档摘要");
scope.Param("damage", "伤害值");
scope.Returns("是否存活");
scope.Region("战斗逻辑", region =>
{
    region.VoidMethod("Attack", ...);
});
```

### 特性

```csharp
scope.Attribute("SerializeField");
scope.Attribute("Header", "\"玩家属性\"");
```

### 字段

```csharp
// 通用字段
scope.Field("int", "mHealth", field =>
{
    field.WithAccess(AccessModifier.Private);
    field.WithModifiers(MemberModifier.Readonly);
    field.WithDefaultValue("100");
    field.WithComment("当前生命值");
    field.WithAttribute("SerializeField");
});

// 便捷方法
scope.PublicField("string", "PlayerName", "玩家名称");
scope.PrivateField("int", "mHealth", "100");
scope.SerializeField("int", "mAttack", "攻击力");
```

### 方法

```csharp
// 通用方法
scope.Method("bool", "IsAlive", method =>
{
    method.WithAccess(AccessModifier.Public);
    method.WithModifiers(MemberModifier.Override);
    method.WithComment("检查是否存活");
    method.WithParameter("int", "threshold", defaultValue: "0", comment: "阈值");
    method.WithGenericParameter("T", "where T : struct");
    method.WithAttribute("MethodImpl", "AggressiveInlining");
    method.WithExpressionBody("mHealth > threshold");
});

// 便捷方法
scope.VoidMethod("Attack", method => { ... });
scope.OverrideMethod("string", "ToString", method => { ... });
scope.ProtectedOverrideVoid("OnDestroy", method => { ... });
```

### 属性

```csharp
// 通用属性
scope.Property("int", "Health", prop =>
{
    prop.WithAccess(AccessModifier.Public);
    prop.WithComment("当前生命值");
    prop.AsReadonly();                        // 只读表达式体
    prop.AsAutoProperty(AccessModifier.Private); // 自动属性带私有 set
    prop.WithExpressionBody("mHealth");       // 表达式体
    prop.WithGetter(getter => { ... });       // 自定义 getter
    prop.WithSetter(setter => { ... }, AccessModifier.Private); // 自定义 setter
});

// 便捷方法
scope.AutoProperty("int", "Health", hasSetter: false, "生命值");
scope.ReadonlyProperty("int", "MaxHealth", "100", "最大生命值");
```

### 作用域

```csharp
// 命名空间
scope.Namespace("Game.Logic", ns => { ... });

// 类
scope.Class("PlayerController", cls =>
{
    cls.WithAccess(AccessModifier.Public);
    cls.AsSealed();
    cls.WithInterface("IPlayerController");
    cls.WithInterface("IDisposable");
    cls.WithAttribute("Serializable");
});

// 自定义作用域
scope.CustomScope("if (health > 0)", semicolon: false, scope =>
{
    scope.Custom("Alive();");
});
```

## 访问修饰符

`AccessModifier` 枚举：

| 值 | 说明 |
|----|------|
| `None` | 无修饰符 |
| `Public` | `public` |
| `Private` | `private` |
| `Protected` | `protected` |
| `Internal` | `internal` |
| `ProtectedInternal` | `protected internal` |
| `PrivateProtected` | `private protected` |

## 成员修饰符

`MemberModifier` 标志枚举（可组合）：

| 值 | 说明 |
|----|------|
| `None` | 无修饰符 |
| `Static` | `static` |
| `Readonly` | `readonly` |
| `Const` | `const` |
| `Virtual` | `virtual` |
| `Override` | `override` |
| `Abstract` | `abstract` |
| `Sealed` | `sealed` |
| `Partial` | `partial` |
| `Async` | `async` |
| `New` | `new` |

```csharp
method.WithModifiers(MemberModifier.Public | MemberModifier.Override);
```

## Writer 实现

### StringCodeWriteKit

写入 `StringBuilder`，适合生成字符串：

```csharp
var writer = new StringCodeWriteKit(initialCapacity: 2048);
root.Gen(writer);
string code = writer.ToString();
```

### FileCodeWriteKit

写入文件，自动创建目录，UTF-8 无 BOM：

```csharp
using var writer = new FileCodeWriteKit("Assets/Scripts/Generated.cs");
root.Gen(writer);
```

### CodeGenLineBuilder

逐行构建器，适合模板式代码：

```csharp
var sb = CodeGenKit.Lines(root);
sb.AppendLine("public class Foo");
sb.AppendLine("{");
sb.AppendLine("    public int X;");
sb.AppendLine("}");
sb.Flush(); // 必须调用 Flush 提交剩余内容
```

## 使用场景

| 场景 | 推荐方式 |
|------|----------|
| 生成面板绑定代码 | 结构化 AST + fluent API |
| 生成配置表加载器 | CodeGenLineBuilder 模板式 |
| 生成序列化代码 | 结构化 AST + fluent API |
| 生成协议/消息类 | 结构化 AST + fluent API |
| 大量线性代码 | CodeGenLineBuilder 模板式 |

## 项目中的使用

### UIKit 面板生成

`UIKitPanelPrefabCreator` 使用结构化 AST 方式生成面板绑定代码：

```csharp
CodeGenKit.GenerateToFile(assetPath, root =>
{
    root.Using("UnityEngine");
    root.Using("YokiFrame");
    root.EmptyLine();
    root.Namespace(scriptNamespace, ns =>
    {
        ns.Class(className, cls =>
        {
            cls.WithAccess(AccessModifier.Public);
            cls.WithInterface("IPanel");
            // ... 字段、方法绑定
        });
    });
});
```

### TableKit 配置表生成

`TableKitCodeGenerator` 使用模板方式生成 `TableKit.cs` 加载器：

```csharp
CodeGenKit.GenerateToFile(outputPath, root =>
{
    var sb = CodeGenKit.Lines(root);
    sb.AppendLine("public static class TableKit");
    sb.AppendLine("{");
    // ... 大量线性代码
    sb.AppendLine("}");
    sb.Flush();
});
```

## 常见问题

| 问题 | 处理方式 |
|------|----------|
| 生成的代码缩进不对 | 确认使用 `ICodeScope` 的嵌套方法，不要手动拼接缩进。 |
| 文件写入失败 | `FileCodeWriteKit` 会自动创建目录，检查路径是否合法。 |
| 模板方式没有输出 | 确认最后调用了 `sb.Flush()`。 |
| 需要生成非 C# 代码 | `CodeGenKit` 专为 C# 设计，其他语言需自定义 writer。 |
| 需要在运行时使用 | CodeGenKit 位于 Editor 程序集，运行时代码不应引用。 |
