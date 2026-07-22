# CodeGenKit 代码生成

> 面向读者：编写 Unity Editor、Godot Tools 或 .NET 工具的开发者
>
> 主要入口：`CodeGenKit.GenerateToString`、`CodeGenKit.GenerateToFile`
>
> 运行边界：Editor/Tools 专属，统一编入 `YokiFrame.Editor`
>
> 状态来源：当前 `Core/Editor/CodeGenKit` 源码

## 适用场景

CodeGenKit 是面向 Editor 工具的纯 C# 代码生成 API，适合生成样板类、配置访问器、注册代码、协议 DTO 和其它可重复产生的 C# 源码。它提供结构化声明 builder 和逐行模板 builder 两种入口，并负责稳定渲染与事务式文件提交。

它不是完整 C# 编译器、Roslyn AST 或语义分析器。类型表达式、默认值、方法体、特性参数和 `Custom` 内容由调用方提供，CodeGenKit 只对结构化名称、物理行和一部分声明修饰符做边界校验。生成后仍应使用目标 Unity/Godot Tools 编译器验证产物。

## 入口与当前状态

| 项目 | 结论 |
|---|---|
| Unity Editor | `YokiFrame.Editor` |
| Godot Tools / 纯 .NET | `Core/Editor/YokiFrame.Editor.csproj` |
| Unity Player | 不包含 CodeGenKit |
| Godot 导出包 | 不包含 CodeGenKit |
| 外部依赖 | 无，不引入 Roslyn、CodeDOM 或模板引擎 |
| C# 版本 | 共享 API 保持 C# 9 兼容 |

CodeGenKit 没有单独的 Core Kit asmdef。调用方应把使用代码放在 Editor/Tools 边界内；不要在 Runtime 程序集中直接引用 `CodeGenKit`。

## 快速上手

### 结构化生成字符串

```csharp
using YokiFrame;

string source = CodeGenKit.GenerateToString(root =>
{
    root.Using("System")
        .EmptyLine()
        .Namespace("Game.Generated", namespaceScope =>
        {
            namespaceScope.Class("PlayerApi", classScope =>
            {
                classScope.PrivateField("int", "mHealth", "100");
                classScope.ReadonlyProperty("int", "Health", "mHealth", "当前生命值");
                classScope.VoidMethod("Reset", method => method
                    .WithBody(body => body.Custom("mHealth = 100;")));
            });
        });
});
```

默认输出为 LF 换行和 Tab 缩进，并使用 `InvariantCulture` 格式化通过 `Append(object)` 或 `AppendFormat` 写入的值。

### 生成并提交文件

```csharp
CodeGenerationFileResult result = CodeGenKit.GenerateToFile(
    "Assets/Generated/PlayerApi.cs",
    root => root.Custom("// generated").EmptyLine());
```

`GenerateToFile` 会先在内存完成构建，成功后再提交文件。输出 `Created` 表示创建、`Updated` 表示内容变化并更新、`Unchanged` 表示字节完全一致且没有改动正式文件。

### 大型模板

```csharp
CodeGenerationFileResult result = CodeGenKit.GenerateToFile(
    "Assets/Generated/Constants.cs",
    root =>
    {
        CodeGenLineBuilder lines = CodeGenKit.Lines(root);
        lines.AppendLine("namespace Game.Generated");
        lines.AppendLine("{");
        lines.AppendLine("\tpublic static class Constants");
        lines.AppendLine("\t{");
        lines.Append("\t\tpublic const double Scale = ").Append(1.5d).AppendLine(";");
        lines.AppendLine("\t}");
        lines.AppendLine("}");
    });
```

逐行构建器的内容会在首次 `Append` 时插入当前作用域，因此末尾不调用 `Flush()` 也不会丢失尾行。混用结构化节点和逐行模板时，先规划追加顺序；逐行 builder 会把当前行作为一个节点插入到创建时的位置。

## 核心 API

### 输出契约

- 换行固定为 `\n`，缩进固定为一个 Tab。
- 输出不会受当前系统区域设置影响。
- XML 文档节点会转义 `&`、`<`、`>`、`"` 和 `'`。
- 结构化名称和限定名称会校验 C# 标识符；保留关键字必须使用 `@` 转义形式。
- 原始 C# 片段必须是单个物理行；需要多行内容时逐行调用 `AppendLine` 或 `Custom`。
- CodeGenKit 不验证原始类型表达式、表达式 body、默认值和特性参数是否具有完整 C# 语义。

### API 参考

### `CodeGenKit`

| 签名 | 说明 |
|---|---|
| `static RootCode Root()` | 创建空的根作用域，可通过 fluent 扩展继续构建 |
| `static CodeGenLineBuilder Lines(ICodeScope scope)` | 为 CodeGenKit 创建的作用域创建逐行模板 builder |
| `static string GenerateToString(Action<RootCode> build, int initialCapacity = 1024)` | 构建并返回确定性源码；`initialCapacity` 不能为负数，只影响初始分配 |
| `static CodeGenerationFileResult GenerateToFile(string filePath, Action<RootCode> build)` | 创建根作用域、构建源码并提交到文件 |
| `static CodeGenerationFileResult WriteToFile(string filePath, RootCode root)` | 提交已经构建好的根作用域 |

`build` 为空时抛出 `ArgumentNullException`。构建回调抛出异常时不会进入文件写入阶段。`WriteToFile` 的 `root` 为空时抛出 `ArgumentNullException`。

### `ICodeScope` 与作用域扩展

`ICodeScope` 是公开的空作用域标记接口。实际作用域由 `CodeGenKit.Root()` 或下列扩展创建，扩展方法会返回原作用域，便于链式调用。

#### 基础节点

| 签名 | 说明 |
|---|---|
| `Using(this ICodeScope scope, string namespaceName)` | 追加 `using Namespace;`；名称必须是点分隔的合法限定名 |
| `EmptyLine(this ICodeScope scope)` | 追加一个空行 |
| `Custom(this ICodeScope scope, string line)` | 追加调用方负责语义的单行原始 C# |

`Custom` 不会替调用方补分号、检查语法或检查路径。空字符串可用于显式空行；`null` 和包含 `CR/LF` 的非逐行片段会被拒绝。

#### 命名空间、类与自定义作用域

```csharp
ICodeScope Namespace(
    this ICodeScope scope,
    string namespaceName,
    Action<NamespaceCodeScope> build)

ICodeScope Class(
    this ICodeScope scope,
    string className,
    Action<ClassCodeScope> build)

ICodeScope Class(
    this ICodeScope scope,
    string className,
    string parentClassName,
    bool isPartial,
    bool isStatic,
    Action<ClassCodeScope> build)

ICodeScope CustomScope(
    this ICodeScope scope,
    string firstLine,
    bool semicolon,
    Action<CustomCodeScope> build)
```

- `Namespace` 生成传统块级 namespace；命名空间每一段都必须是合法标识符。
- 简化版 `Class` 默认生成 `public class`，不是 `partial`，也不是 `static`。
- 完整版 `Class` 支持可选父类型、`partial` 和 `static`；父类型仍是调用方负责的单行类型表达式。
- `CustomScope` 生成 `firstLine`、花括号、内部节点和可选的结尾分号，适用于未被结构化 API 覆盖的块。
- `NamespaceCodeScope`、`ClassCodeScope` 和 `CustomCodeScope` 的构造函数是内部的，应通过回调参数使用，不要直接 `new`。
- `build` 为空时跳过内部构建，但作用域节点仍会被追加。

`ClassCodeScope` 的公开配置方法：

| 签名 | 说明 |
|---|---|
| `ClassCodeScope WithAccess(AccessModifier access)` | 设置访问级别，默认 `Public` |
| `ClassCodeScope AsSealed()` | 标记为 `sealed`；静态类不能再调用 |
| `ClassCodeScope WithInterface(string interfaceName)` | 按调用顺序追加接口类型表达式 |
| `ClassCodeScope WithAttribute(string attributeName)` | 追加无参数特性 |
| `ClassCodeScope WithAttribute(string attributeName, string argument)` | 追加带一个原始参数的特性 |

静态类不能声明父类型、接口或 `sealed`；非法组合在生成阶段抛出 `InvalidOperationException`。

#### 字段扩展

```csharp
ICodeScope Field(
    this ICodeScope scope,
    string typeName,
    string fieldName,
    Action<FieldCode> configure = null)

ICodeScope PublicField(
    this ICodeScope scope,
    string typeName,
    string fieldName,
    string comment = null)

ICodeScope PrivateField(
    this ICodeScope scope,
    string typeName,
    string fieldName,
    string defaultValue = null)

ICodeScope SerializeField(
    this ICodeScope scope,
    string typeName,
    string fieldName,
    string comment = null)
```

- `Field` 默认访问级别为 `private`，可在配置回调中完整设置。
- `PublicField` 生成 public 字段，并可附加 XML summary。
- `PrivateField` 生成 private 字段，并可附加默认值表达式。
- `SerializeField` 生成 `[SerializeField] private` 字段，但 CodeGenKit 不引用 `UnityEngine`。

`FieldCode` 的公开 API：

| 签名 | 说明 |
|---|---|
| `new FieldCode(string typeName, string fieldName)` | 创建字段 builder；字段名严格校验 |
| `FieldCode WithAccess(AccessModifier access)` | 设置访问级别 |
| `FieldCode WithModifiers(MemberModifier modifiers)` | 设置字段修饰符 |
| `FieldCode WithDefaultValue(string defaultValue)` | 设置等号右侧的单行默认值表达式 |
| `FieldCode WithComment(string comment)` | 设置 XML summary，输出时转义 |
| `FieldCode WithAttribute(string attributeName)` | 追加无参数特性 |
| `FieldCode WithAttribute(string attributeName, string argument)` | 追加带一个原始参数的特性 |

字段允许的修饰符为 `New`、`Static`、`Readonly`、`Const`。`Const` 不能与 `Static` 或 `Readonly` 组合。

#### 属性扩展

```csharp
ICodeScope Property(
    this ICodeScope scope,
    string typeName,
    string propertyName,
    Action<PropertyCode> configure = null)

ICodeScope ReadonlyProperty(
    this ICodeScope scope,
    string typeName,
    string propertyName,
    string expression,
    string comment = null)

ICodeScope AutoProperty(
    this ICodeScope scope,
    string typeName,
    string propertyName,
    bool hasSetter = true,
    string comment = null)
```

`Property` 默认生成带 getter 的 public 自动属性。`ReadonlyProperty` 生成只有 getter 的表达式属性；`AutoProperty` 根据 `hasSetter` 生成自动属性或只读自动属性。

`PropertyCode` 的公开 API：

| 签名 | 说明 |
|---|---|
| `new PropertyCode(string typeName, string propertyName)` | 创建属性 builder；属性名严格校验 |
| `PropertyCode WithAccess(AccessModifier access)` | 设置属性访问级别 |
| `PropertyCode WithModifiers(MemberModifier modifiers)` | 设置属性修饰符 |
| `PropertyCode WithComment(string comment)` | 设置 XML summary |
| `PropertyCode WithAttribute(string attributeName)` | 追加无参数特性 |
| `PropertyCode AsReadonly()` | 重置为只有 getter 的自动属性 |
| `PropertyCode AsAutoProperty(AccessModifier setterAccess = AccessModifier.None)` | 重置为自动属性，可设置 setter 访问级别 |
| `PropertyCode WithExpressionBody(string expression)` | 重置为只有 getter 的表达式属性 |
| `PropertyCode WithGetter(Action<ICodeScope> getterBody)` | 切换到显式访问器并配置 getter body |
| `PropertyCode WithSetter(Action<ICodeScope> setterBody, AccessModifier access = AccessModifier.None)` | 配置显式 setter，并可设置 setter 访问级别 |

表达式属性不能直接调用 `WithSetter`；必须先调用 `WithGetter` 切换到显式访问器。`Abstract` 属性只能使用无 body 的自动访问器。

#### 方法扩展

```csharp
ICodeScope Method(
    this ICodeScope scope,
    string returnType,
    string methodName,
    Action<MethodCode> configure)

ICodeScope VoidMethod(
    this ICodeScope scope,
    string methodName,
    Action<MethodCode> configure)

ICodeScope OverrideMethod(
    this ICodeScope scope,
    string returnType,
    string methodName,
    Action<MethodCode> configure)

ICodeScope ProtectedOverrideVoid(
    this ICodeScope scope,
    string methodName,
    Action<MethodCode> configure)
```

`VoidMethod` 是 `returnType = "void"` 的快捷入口；`OverrideMethod` 默认 `protected override`；`ProtectedOverrideVoid` 是两者的组合快捷入口。四个扩展都允许 `configure` 为空，但不建议省略方法体或访问级别配置。

`MethodCode` 的公开 API：

| 签名 | 说明 |
|---|---|
| `new MethodCode(string returnType, string methodName)` | 创建方法 builder；方法名严格校验 |
| `MethodCode WithAccess(AccessModifier access)` | 设置访问级别，默认 `Public` |
| `MethodCode WithModifiers(MemberModifier modifiers)` | 设置方法修饰符 |
| `MethodCode WithComment(string comment)` | 设置 XML summary |
| `MethodCode WithParameter(string type, string name, string defaultValue = null, string comment = null)` | 按顺序追加参数、默认值和 XML param 说明 |
| `MethodCode WithAttribute(string attributeName)` | 追加无参数特性 |
| `MethodCode WithAttribute(string attributeName, string argument)` | 追加带一个原始参数的特性 |
| `MethodCode WithGenericParameter(string parameterName, string constraint = null)` | 追加泛型参数及可选约束；约束不包含 `where` 前缀 |
| `MethodCode WithBody(Action<ICodeScope> bodyBuilder)` | 使用结构化作用域配置块级方法体 |
| `MethodCode WithExpressionBody(string expression)` | 使用箭头右侧单行表达式；会清除块级 body |

方法允许的修饰符为 `New`、`Static`、`Virtual`、`Override`、`Abstract`、`Sealed`、`Partial`、`Async`。`Virtual`、`Override`、`Abstract` 互斥；`sealed` 必须与 `override` 同时出现；静态方法不能声明多态修饰符；`abstract async` 不允许。`abstract` 方法不能配置 body。

#### 特性与注释扩展

```csharp
ICodeScope Attribute(this ICodeScope scope, string attributeName)
ICodeScope Attribute(this ICodeScope scope, string attributeName, string argument)
ICodeScope Comment(this ICodeScope scope, string content)
ICodeScope Summary(this ICodeScope scope, string content)
ICodeScope Param(this ICodeScope scope, string parameterName, string description)
ICodeScope Returns(this ICodeScope scope, string description)
ICodeScope Region(this ICodeScope scope, string regionName, Action<ICodeScope> build)
```

- `Attribute` 追加无参数或带一个原始参数的特性节点；特性名称必须是合法限定名。
- `Comment` 生成普通 `//` 注释；多行内容会拆成多行注释。
- `Summary`、`Param` 和 `Returns` 生成 XML 文档注释，并自动转义文本。
- `Param` 的参数名必须是合法 C# 标识符。
- `Region` 按 `#region`、空行、内容、空行、`#endregion` 的顺序追加到当前父作用域；`regionName` 必须是单行文本。

#### `CodeGenLineBuilder`

| 签名 | 说明 |
|---|---|
| `new CodeGenLineBuilder(ICodeScope scope)` | 为 CodeGenKit 创建的作用域建立逐行 builder |
| `CodeGenLineBuilder Append(string value)` | 向当前行追加文本；`null` 按空文本处理 |
| `CodeGenLineBuilder Append(char value)` | 追加单个字符；`CR/LF` 必须使用 `AppendLine` |
| `CodeGenLineBuilder Append(object value)` | 使用 `InvariantCulture` 格式化对象后追加 |
| `CodeGenLineBuilder AppendFormat(string format, params object[] arguments)` | 使用 `InvariantCulture` 格式化并追加单行文本 |
| `CodeGenLineBuilder AppendLine()` | 结束当前行；没有当前行时追加空行 |
| `CodeGenLineBuilder AppendLine(string value)` | 追加文本并结束当前行 |
| `void Flush()` | 结束当前行；通常不是必须调用 |

`CodeGenLineBuilder` 只接受由 CodeGenKit 创建的作用域。外部实现 `ICodeScope` 后传入会抛出 `ArgumentException`，避免绕过内部节点顺序和渲染边界。

### 枚举

#### `AccessModifier`

`None`、`Public`、`Private`、`Protected`、`Internal`、`ProtectedInternal`、`PrivateProtected`。

`None` 表示不输出访问修饰符；builder 默认值由声明类型决定：字段为 `Private`，属性、方法和类为 `Public`。

#### `MemberModifier`

这是带 `[Flags]` 的组合枚举：

| 值 | 输出 |
|---|---|
| `None` | 无修饰符 |
| `New` | `new` |
| `Static` | `static` |
| `Readonly` | `readonly` |
| `Const` | `const` |
| `Virtual` | `virtual` |
| `Override` | `override` |
| `Abstract` | `abstract` |
| `Sealed` | `sealed` |
| `Partial` | `partial` |
| `Async` | `async` |

输出顺序由 CodeGenKit 固定，不由 flag 传入顺序决定。声明类型不支持的 flag 或语义冲突组合会在渲染时抛出 `InvalidOperationException`。

#### `CodeGenerationFileResult`

| 值 | 含义 |
|---|---|
| `Created` | 正式文件原本不存在，本次已创建 |
| `Updated` | 内容发生变化，本次已原子更新 |
| `Unchanged` | UTF-8 字节完全一致，本次未触碰正式文件 |

#### `CommentType`

公开枚举值为 `SingleLine`、`XmlSummary`、`XmlParam`、`XmlReturns`。注释节点本身由扩展方法创建，日常调用应优先使用 `Comment`、`Summary`、`Param` 和 `Returns`，不直接构造内部节点。

## 生命周期与错误边界

`GenerateToFile` 和 `WriteToFile` 的文件提交过程如下：

1. 将目标路径规范化为绝对路径，并拒绝空路径或不包含文件名的路径。
2. 以 UTF-8 无 BOM 编码生成 payload。
3. 如果目标文件存在，先按长度和字节比较；完全一致时返回 `Unchanged`，不创建目录、不更新时间戳。
4. 在目标目录创建随机临时文件，写入完整 payload 并执行持久化 flush。
5. 新文件使用同目录 move；已有文件使用 `File.Replace` 原子更新。
6. 失败时删除临时文件；构建失败、临时写入失败或提交失败都不会主动截断已有正式文件。

CodeGenKit 不负责路径 containment、生成目录白名单、用户修改检测或生成文件清单。调用方的 Editor 工具、Application 或 CLI 必须在调用前校验这些业务约束，避免把生成 API 变成任意路径写入入口。

## 限制与相关资料

| 问题 | 原因与处理 |
|---|---|
| 名称包含 `-`、空格或未转义关键字 | namespace、类名、成员名、参数名和泛型参数使用合法 C# 标识符；关键字使用 `@` |
| `Custom` 或类型表达式包含换行 | 原始片段只允许单行；多行模板拆成多次 `AppendLine` 或 `Custom` |
| 结构化输出编译失败 | CodeGenKit 不解析原始类型、表达式、默认值和特性参数；检查生成源码并运行目标宿主编译 |
| 表达式属性追加 setter 失败 | 先调用 `WithGetter` 切换到显式访问器，再调用 `WithSetter` |
| 修饰符组合失败 | 按字段、属性、方法的允许集合配置，避免 `const readonly`、`static abstract` 和不带 `override` 的 `sealed` |
| 文件无变化但调用方期待更新时间 | `Unchanged` 是按 UTF-8 字节比较的结果；内容未变化时不会更新时间戳 |
| Runtime 程序集无法解析类型 | CodeGenKit 只在 `YokiFrame.Editor`，将调用代码移入 Editor/Tools 程序集 |

### 验证

当前 Editor 测试位于 `Core/Tests/Editor/CodeGenKit/YokiFrameCodeGenKitTests.cs`，覆盖：

- 结构化输出的确定性换行、缩进和区域文化稳定性。
- XML 文档转义、合法性校验和修饰符组合校验。
- `SerializeField` 文本快捷入口与 `YokiFrame.Editor` 程序集归属。
- `Created`、`Updated`、`Unchanged` 三种文件结果。
- 构建失败时已有文件保持不变以及临时文件清理。

新增生成模板后，至少执行对应 Editor 测试，并在目标 Unity/Godot Tools 编译生成产物；不要仅凭 CodeGenKit 成功返回就认定生成 C# 语义正确。
