# YokiScript v1：语法与执行契约

> **历史方案，已撤销，未实施。用户现已选定 Roslyn，以下内容不再是开发或验收要求。**
> 当前唯一执行契约为 [Roslyn 内存 C# 自动化](Engine-Roslyn-Automation-Contract.md)，父设计为 [Engine Kit v5.2](Engine-Operation-Glue-Design.md)。
> 第一版必须支持父设计 §12 中用户给出的整段脚本。它是自研命令语言，不是 TypeScript 子集兼容性承诺；不依赖外部 SDK、Node、JS 引擎或 C# eval。

## 1. 执行模型

```text
外部输入 / .yoki 文件
  -> 词法分析（token + 行列）
  -> 语法分析（AST）
  -> 整段静态绑定与内建调用校验
  -> 顺序解释（变量、数据表达式、断言）
  -> 已有命令客户端 -> 宿主主线程 -> 已编译的 C# 方法
```

- 上述解析、校验与解释发生在 CLI 中，不写入引擎脚本目录，不生成 JS/C# 或动态程序集。
- 不能用正则抽取调用后跳过未识别文本。必须消费完整输入；末尾非法语句也应令整段在任何宿主请求前失败。
- AST 校验只保证语法、名称绑定、已知内建函数与调用形状。取回业务数据后才能发现的类型/成员错误，在对应步骤报错；不保证回滚此前业务调用。
- 引擎操作使用现有 Policy/Gate/目标/会话/运行契约，解析器不能绕过权限、线程或确认边界。
- 第一版不暴露任意 `command(action)`、文件访问、网络、系统 shell、CLR 反射、JS eval 或 C# eval 内建函数。

## 2. 支持的语言范围

| 特性 | v1 契约 |
|---|---|
| 固定任务包裹 | `export default async function (engine, test) { ... }`；仅这一种模块外观，参数名固定 |
| 变量 | `const name = expression;`，允许前面变量被后面引用；同作用域不可重名、不可重新赋值 |
| 保留绑定 | `engine`、`test` 不允许被变量遮蔽；标识符大小写敏感 |
| 语句 | const 声明、内建调用语句；分号必需，不实现自动分号插入 |
| 等待 | `await` 仅用于 §4 的异步内建调用；没有任意 Promise 或后台并发任务 |
| 字面量 | JSON 形式的双引号字符串及转义、有限数值、`true`、`false`、`null` |
| 数组 | `[value, ...]`，元素可为数据表达式 |
| 对象 | `{ key: value }`、`{ "key": value }`、变量简写 `{ before, after }`；重复键拒绝 |
| 数据访问 | 已返回数据的 `.Field`、`["key"]`、`[index]`；只访问自身数据，缺失值报错，不自动得到 undefined |
| 简单运算 | 数值 `+ - * /`、一元正负号、括号；乘除优先于加减，不隐式转字符串或布尔值 |
| 注释与排版 | `//`、`/* ... */`、空白、多行调用；对象/数组/参数末尾允许一个尾逗号 |

暂不支持：类型注解、interface、泛型语法、import、任意 export、类、用户定义函数、箭头函数、let/var/赋值、解构、展开语法、模板字符串、可选链、if、循环、try/catch/finally、new、动态执行。

遇到未支持语法，返回带行列的 `YokiScriptUnsupportedSyntax` 或 `YokiScriptSyntaxError`，不忽略、不转换为其他语言、不偷偷交给外部解析器。

### 2.1 语法骨架

下面定义语句与表达式优先级；异步调用是否合法还需 §4 的语义校验。

```text
program        = "export" "default" "async" "function"
                 "(" "engine" "," "test" ")" "{" statement* "}" EOF
statement      = "const" identifier "=" expression ";"
               | expression ";"
expression     = additive
additive       = multiplicative (("+" | "-") multiplicative)*
multiplicative = unary (("*" | "/") unary)*
unary          = ("+" | "-") unary | "await" postfix | postfix
postfix        = primary ("." identifier | "[" expression "]"
                         | "(" arguments? ")")*
primary        = literal | identifier | array | object | "(" expression ")"
```

非声明表达式语句必须是允许的内建调用；普通 `123;`、任意数据上的函数调用等在执行前拒绝。异步调用的 await 必须紧邻该调用，不能通过保存未等待值规避顺序执行。

## 3. 值与句柄

解释器的值只有：空值、布尔值、字符串、数值、数据数组、数据对象、受控对象句柄和受控成员句柄。没有任意 JS 对象或原始 CLR 对象。

- 数值计算使用受检查的有限 `decimal` 范围；除零/溢出报 `YokiScriptNumericError`。不承诺 JavaScript 的 IEEE 754、NaN、Infinity、BigInt 或字符串拼接语义。C# 参数绑定仍按目录声明的实际类型检查范围。
- 数组下标必须为非负整数且在范围内。数据字段名大小写敏感。空值不能继续访问成员。
- 数据访问只读已序列化值，不调用 C# getter、索引器、枚举器或 ToString。`before.Health` 不是再次向宿主读取 Model。
- 每次 `invoke` 完成返回新的值快照；`before` 不得因后续业务修改而变化，否则前后数值断言没有意义。
- 对象句柄只暴露内建 `.methods` 目录；成员句柄暴露只读 `.id`。不能通过 `.GetType()`、`.constructor` 等任意成员逃逸到宿主。
- 句柄绑定 engine/target/session/generation/实例身份；memberId 必须属于该对象的已发现成员，不接受伪造或跨对象复用。
- 句柄、能力对象与内建函数不是普通 JSON 数据；放入 invoke 参数、日志或对象简写时应报类型错误，不能隐式反射序列化。

## 4. 第一版内建函数

`engine` 和 `test` 是解释器注入的能力对象，用户不安装或 import SDK。

| 写法 | 同步性 | 后端与返回 |
|---|---|---|
| `engine.objects.require({ root, type, architecture? })` | 必须 await | 通过对象发现取唯一活动对象，返回受控句柄；没有对象或有歧义时失败 |
| `object.methods.require(name, parameterTypes)` | 必须 await | 发现成员，按完整参数类型数组绑定，返回成员句柄；不执行方法 |
| `engine.invoke(object, memberId, args)` | 必须 await | 提交普通 C# 方法调用并观察终态；成功后返回值快照，void 返回 null |
| `engine.waitFrames(count, { clock })` | 必须 await | 等待真实宿主帧；完成后返回 null，不使用固定毫秒 sleep 冒充 |
| `test.equal(actual, expected, message?)` | 同步，不允许 await | 严格结构化相等断言，记录 actual/expected/message/位置 |
| `engine.consoleLog(value)` | 必须 await | 将结构化数据发送到宿主日志后端，完成后返回 null |
| `engine.capture({ mode, path })` | 必须 await | 观察真实截图完成，返回产物路径、尺寸、hash、模式与宿主身份 |

### 4.1 发现与调用

- `parameterTypes` 例如 `["System.String", "System.Int32", "System.Int32"]`；以目录返回的完整签名为准，不猜重载。
- `.require` 只绑定已有实例和方法，不调用懒初始化属性来创建 Architecture；未初始化时报告明确原因。
- `engine.invoke` 对普通公开 C# 方法工作，不要求静态包装、`YokiFrameEntry` 属性、测试上下文或专用返回类型。
- `Read` 仍是业务方法，默认 Dangerous。不得因名称像查询，就改走只读 inspect 或跳过执行确认。
- 元数据发现得到的实际支持状态、调用范围和当前会话要在派发时再校验。预检成功不能冻结后续权限或保证对象一直存活。

### 4.2 await 与执行完成

- 所有宿主交互串行执行。`await` 表示“当前操作明确成功完成后才能继续”，不是只等待 accepted。
- 已完成的调用才绑定变量；未完成、失败、取消或未知结局不产生可用返回值，也不执行下一条语句。
- 全程保留 scriptRunId/requestId/runId 和宿主身份。查询结果本身纯读，不能推进用户代码。
- 异步内建调用缺少 await，或对 `test.equal` / 普通数值使用 await，均在整段预检时拒绝。
- 默认运行超时 30 秒；允许调用方在 CLI 上显式调整，但不得超过 10 分钟。取消和超时沿用宿主协作取消/Detached 语义，不承诺抢占同步 C#。

### 4.3 断言

- 数字按数值比较，`85` 与 `85.0` 相等；数字与字符串不相等。
- 数据数组有序比较，对象比较键集合和对应值，不依赖字段输出顺序；句柄不支持值相等断言。
- `test.equal` 失败记录实际值和期望值，任务进入 `Failed` 并立即停止。调用异常或动态类型错误进入 `Errored`；重载导致的未确定操作结局进入 `Unknown`。
- 父设计示例中的日志和截图位于断言之后，因此失败时不会执行。第一版不提供自动 finally，也不能为了“收集证据”擅自执行后续步骤。

### 4.4 帧、日志与截图

- `gameFrame` 要求对应游戏目标正在推进；暂停/未播放时明确等待、超时或拒绝，不能代替用户启动游戏。
- `editorTick` 只表示编辑器 tick。两种时钟不互相降级。
- `consoleLog` 必须进入 Unity/Godot 控制台；外部 stdout 仅是运行报告，不能替代宿主证据。
- `capture` 必须等待文件实际写完，不自动修改用户相机或保存场景；输出限制在受控项目路径并检验路径逃逸。
- 不支持的截图模式报错，不用别的图像假装成功。截图产物必须被打开检查，不能只检查存在性。

## 5. 限额与诊断

| 项目 | v1 硬上限 |
|---|---|
| UTF-8 输入 | 128 KiB |
| token | 16384 |
| AST 节点 | 8192 |
| 表达式/对象嵌套深度 | 32 |
| 语句与变量 | 各 256 |
| 单个脚本字符串 | 16384 字符 |
| 单次宿主返回值 | 256 KiB，超限明确失败，不截断后继续算作完整数据 |
| 全任务数据与报告预算 | 8 MiB |

解析前检查输入上限；分析过程中持续检查 token/节点/深度，防止耗尽栈或内存。没有循环不意味着可以忽略嵌套和大载荷。

诊断至少含 `code / message / line / column / step / sourceSpan`；涉及宿主时附 requestId/runId/target/session/generation。语法错误不派发命令，运行错误保留已完成步骤，不声称回滚。

确认缺失、调用范围拒绝、目标不支持、对象失效与业务异常保留原始可操作错误码，不能都报“脚本失败”。

## 6. 验收与实施顺序

### 6.1 语言内核

1. 将父设计 §12 的完整脚本作为固定 fixture，包括多行数组/对象、注释、对象简写和减法。
2. 使用记录型假桥接验证准确调用顺序、参数、变量、快照不变性和 await 完成门；不得把测试中的假桥接接到生产路径。
3. 验证末尾语法错误、未定义变量、变量遮蔽、未知内建函数、非法 await、缺失分号、越限输入全部零派发。
4. 覆盖数字运算优先级、括号、除零/溢出、缺失字段、下标越界、重复对象键、无效句柄与类型错误。
5. 验证断言失败停止后续日志/截图；宿主异常、取消与 Unknown 不执行后续语句。
6. 在 CLI 的现有 Native AOT 发布路径验证，不借助动态代码生成或 JS/TS 编译器绕过实现。

### 6.2 宿主纵切

先完成真实对象发现/普通方法调用，再接日志、等帧、截图。最终运行用户完整示例时只允许按真实目录替换业务类型名，不能通过删功能或生成 C# 包装让测试通过。

- 桥接安装与业务代码首次编译完成后，记录初始会话、数值和编译状态。
- 同一播放会话连续执行两份不同参数的 YokiScript，数值在已有状态上继续变化。
- 命令证据中不出现 C# eval、属性入口运行、资产刷新或编译请求。
- 文件变化中不出现为任务生成/修改的 `.cs`；编译事件与 generation 不变，真实 Model/View 数值同步。
- 通过 CLI 读取宿主日志并打开截图验证。单元测试不能代替 Unity/Godot 真机证据。

接口或语法需要调整时，先修改本契约与固定 fixture，再实现；不得将“不支持用户示例”留作模糊的未来优化。
