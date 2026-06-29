# CodeGenKit 代码生成

## 用途

CodeGenKit 用于 Editor 侧生成 C# 文件。优先用结构化 AST 生成业务代码；需要快速拼模板时再用逐行构建器。

| 生成目标 | 推荐 |
|---|---|
| 生成面板绑定代码 | 结构化 AST + fluent API。 |
| 生成 TableKit 加载器 | `CodeGenLineBuilder` 模板式。 |
| 生成协议、配置、工具代码 | 优先结构化 AST。 |
| 生成非 C# 文件 | 不适合，另写专用 writer。 |

## 结构化生成

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
            cls.PrivateField("int", "mHealth", "100");
            cls.AutoProperty("int", "Health", hasSetter: false, "当前生命值");
            cls.VoidMethod("TakeDamage", method =>
            {
                method.WithAccess(AccessModifier.Public);
                method.WithParameter("int", "damage");
                method.WithBody(body =>
                {
                    body.Custom("mHealth -= damage;");
                });
            });
        });
    });
});
```

## 模板式生成

```csharp
CodeGenKit.GenerateToFile("Assets/Scripts/Generated.cs", root =>
{
    var lines = CodeGenKit.Lines(root);
    lines.AppendLine("namespace Generated");
    lines.AppendLine("{");
    lines.AppendLine("    public static class Constants");
    lines.AppendLine("    {");
    lines.AppendLine("        public const int MaxLevel = 100;");
    lines.AppendLine("    }");
    lines.AppendLine("}");
    lines.Flush();
});
```

模板式最后必须调用 `Flush()`。

## 常用入口

| 方法 | 说明 |
|---|---|
| `Root()` | 创建根作用域。 |
| `Lines(scope)` | 创建逐行构建器。 |
| `GenerateToString(build, capacity)` | 构建并返回字符串。 |
| `GenerateToFile(filePath, build)` | 构建并写入文件。 |
| `WriteToFile(filePath, root)` | 写入已构建的根节点。 |

## 常用节点

| 节点 | 作用 |
|---|---|
| `UsingCode` | using 指令。 |
| `NamespaceCodeScope` | 命名空间。 |
| `ClassCodeScope` | 类。 |
| `FieldCode` | 字段。 |
| `MethodCode` | 方法。 |
| `PropertyCode` | 属性。 |
| `AttributeCode` | 特性。 |
| `CustomCode` | 任意代码行。 |

## 项目内落点

| 使用者 | 用途 |
|---|---|
| UIKit 面板生成 | 生成面板绑定脚本。 |
| TableKit 生成器 | 生成 `TableKit.cs`、可选 `ExternalTypeUtil.cs`。 |

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 缩进不对 | 优先用作用域节点，不手写缩进。 |
| 模板没输出 | 检查 `CodeGenLineBuilder.Flush()`。 |
| 运行时程序集引用失败 | CodeGenKit 是 Editor 层工具，不给 Runtime 引用。 |
| 写文件失败 | 检查路径是否合法，以及目标目录是否允许写入。 |
