# 框架概览

> 面向读者：使用 YokiFrame 编写游戏业务、接入宿主或使用工具链的开发者
>
> 主要入口：Runtime Kit、Avalonia Workbench、`yoki` CLI 与 Installer
>
> 运行边界：Runtime API 按 Kit 和宿主进入构建；工具链不进入 Player
>
> 状态来源：`Api/00-GettingStarted/Kit_Status.md`

## 适用场景

YokiFrame 是面向 Unity 2022.3+ 与 Godot .NET 的跨宿主 C# 游戏框架。它把可复用的业务规则放在纯 C# Core，把引擎 API 与生命周期差异放入独立 Adapter，并把观察、诊断、安装和项目工具放入 .NET 10 Workbench 工具链。

它适合需要复用事件、状态机、资源、对象池、日志、动作、音频、存档、空间索引或本地化能力的项目。UIKit 是 Unity 专属 Tool，仅提供 Unity 实现。

YokiFrame 不替代 Unity/Godot 编辑器，也不提供通用 Scene、Prefab、Asset、Play Mode、截图或输入自动化。使用本页先确定任务应从哪个新版入口开始；不要把诊断或安装入口当成业务 Runtime API，也不要直接读写 `.yokiframe` 协议文件。

## 入口与当前状态

选择入口时先区分三层事实：业务 Runtime API、Editor/Tools 条件下的 Kit Interaction、Avalonia Workbench 页面。三层完成度不同，页面存在或文档标题都不能单独证明一个 Kit 已完成迁移；精确状态以 `Api/00-GettingStarted/Kit_Status.md` 为准。

| 目标 | 推荐入口 | 说明 |
|---|---|---|
| 编写游戏业务和跨宿主规则 | 下表的 Runtime Kit | 只依赖公开门面与匹配的宿主 Adapter |
| 查看当前项目、Kit 或运行态证据 | Avalonia Workbench | 面向交互式人工诊断，页面只显示已完成真实数据链路的能力 |
| 脚本化查询、诊断或已声明的受控 action | `yoki` CLI | 默认只读；显式 action 先核实当前 capability catalog |
| 安装、更新或回滚 YokiFrame | Installer | 先生成 plan，确认后才 apply |
| 操作 Unity Scene、Prefab、Play Mode、截图或输入 | 对应宿主或外部自动化工具 | 不属于 YokiFrame 的 API 面 |

## 快速上手

从一个小的业务边界开始，选择对应 Kit 并直接调用当前公开 API：

```csharp
using YokiFrame;

public sealed class CombatStateMachine
{
    private readonly FSM<CombatState> mMachine = new();

    public void Start()
    {
        EventKit.Type.Send(new CombatStarted());
        mMachine.ChangeState(CombatState.Opening);
    }
}
```

宿主 Adapter 为 Runtime Settings、日志和默认后端提供当前宿主实现，并在首次真实使用时创建。项目显式注入 Provider 或 Backend 时始终优先。

## Runtime API

| 能力 | 公开入口 | 宿主范围 |
|---|---|---|
| 框架组合 | `Architecture<T>` | 跨宿主 Runtime |
| 事件 | `EventKit.Type`、`EventKit.Enum` | 跨宿主 Runtime |
| 状态机 | `FSM<TEnum>`、`FSM<TEnum,TArgs>` | 跨宿主 Runtime |
| 日志 | `LogKit` | 跨宿主 Runtime；诊断位于 Editor/Tools |
| 对象池 | `PoolKit` | 跨宿主 Runtime |
| 资源与场景 Provider | `ResKit`、`IResSceneProvider` | 跨宿主 Runtime；宿主实现独立 |
| 单例 | `Singleton<T>`、`SingletonKit<T>` | 跨宿主 Runtime |
| 通用类型 | `BindValue<T>`、`FastDictionary<TKey,TValue>`、`PooledLinkedList<T>`、`SpanSplitter` | 跨宿主 Runtime |
| Editor C# 生成 | `CodeGenKit` | Editor/Tools 专属 |
| Unity Inspector 基础设施 | `InspectorKit` | Unity Adapter Editor 专属 |
| 动作编排 | `ActionKit` | 跨宿主 Runtime；Coroutine/UniTask/DOTween 为独立接入 |
| 音频 | `AudioKit` | 跨宿主 Runtime；Unity/Godot 原生实现独立 |
| 场景生命周期 | `SceneKit` | 跨宿主 Runtime；默认跟随 ResKit 场景 Provider |
| 本地化 | `LocalizationKit` | 跨宿主 Runtime |
| 存档 | `SaveKit` | 跨宿主 Runtime |
| 空间索引 | `SpatialKit` | 跨宿主 Runtime；Unity Gizmo 为 Editor 接入 |
| 数据表生成 | Workbench TableKit 生成器与生成后的门面 | 离线工具；未生成时不提供 Runtime 类型 |
| Unity UI | `UIKit`、`UIPanel` | Unity 专属 Runtime/Editor |

具体类型、最小示例和生命周期约束位于相应 Kit 主页面；请从文档目录的 `Api/01-Architecture`、`Api/02-Core` 或 `Api/03-Tool` 打开对应页面。

## 生命周期与错误边界

- Core 不引用 UnityEngine、UnityEditor、Godot、Avalonia 或可选第三方库。
- Adapter 只处理宿主 API、生命周期和组合，并单向依赖 Core。
- 资源 handle、事件订阅、状态机、动作 controller 和异步工作必须由业务 owner 明确释放或取消。
- Runtime Settings、默认资源 Provider 和日志后端按第一次真实使用惰性创建；查询和诊断读取不应隐式创建后端。
- Workbench、Installer 与 `yoki` 通过 Client 和 Application 层访问协议；业务代码和脚本不直接写 `.yokiframe`。

## `yoki` CLI

`yoki` 位于当前项目 `.yokiframe/runtime/com.hinatayoki.yokiframe/<sourceFingerprint>/`；`current.json` 选择源码指纹，目录内 `tool-manifest.json` 的 `cliEntry` 选择当前平台入口。Git URL 和源码包不携带任何 Runtime 二进制。Unity 用户按 `Ctrl+E` 时仅在缺少可用 Workbench Runtime 的情况下生成缓存；已有 Runtime 会立即打开，Workbench 再后台检查源码更新，并由页头按钮显式触发新版构建。窗口关闭会取消检查或构建；成功发布新指针后会清理旧 fingerprint 目录，仍被旧进程占用的目录延迟到后续启动处理。Godot 用户显式运行 `YokiFrameWorkbench~/scripts/runtime-bootstrap/install-godot` 对应平台脚本，脚本构建缓存后直接打开 Installer。CLI 没有稳定的 `--help` 契约，应以源码、实际错误提示和 AI Skill 的命令参考为准。

| 类别 | 命令 |
|---|---|
| Project Model | `project status`、`project refresh` |
| 能力与 engine | `harness status`、`harness catalog`、`engine list` |
| 运行态读取 | `telemetry read`、`snapshot read`、`bridge status`、`doctor`、`fastchannel status` |
| 已声明 action | `command send` |
| SpatialKit 只读查询 | `spatialkit stats`、`spatialkit indexes`、`spatialkit density`、`spatialkit analyze` |
| AudioKit 索引 | `audio index scan`、`audio index generate` |
| LocalizationKit | `localization search`、`localization check`、`localization add`、`localization template generate`、`localization preview` |
| 安装事务 | `installer plan`、`installer apply` |
| Godot Player 导出 | `player build --engine godot` |

默认读取顺序是 Project Model、capability catalog、engine、telemetry，再回落 snapshot。`project refresh`、`audio index generate`、`localization add`、`localization template generate`、`installer apply`、`player build` 与 UserAction 都会写入项目或宿主，必须由明确意图触发。`localization preview` 只生成项目 Temp 下的 Luban JSON 预览，不改作者 Excel 或配置。Godot Player 输出必须位于项目根内；YokiFrame CLI 当前不提供 Unity Player 构建，请使用 Unity Editor 或自行选择外部自动化工具。面向人的使用顺序位于 `Guides/Tooling.md`。

## Avalonia Workbench 与 Installer

Workbench 的导航只包含已经有 Application 强类型 read model 和真实页面的能力：框架、文档、EventKit、FsmKit、LogKit、PoolKit、ResKit、ActionKit、AudioKit、SpatialKit、UIKit、TableKit、LocalizationKit 和 SaveKit。Architecture 保留 Runtime/Interaction/CLI 诊断，但没有独立页面；未完成的 Kit 不显示占位页。

Installer 与 Workbench 使用同一个 Avalonia 程序。它支持 Unity embedded、Unity Git URL、Godot local 三种互斥来源；任何安装或更新都先审阅 plan 和 rollback 条件。Godot local 会完整替换 `addons/yokiframe`，不做文件级合并，失败时恢复备份。Installer 检测到 Godot Runtime 缓存缺失或源码指纹失配时，会提供“构建 Runtime”恢复动作；该动作从用户已选源码包构建缓存并启动新 Installer，不会修改包内目录。详细流程位于 `Guides/Tooling.md`。

## 限制与相关资料

- Runtime API 的可用性、Interaction 和 Workbench 完成度是独立事实；不要仅凭文档标题或页面名称推断完整迁移。
- Unity 自动化、Scene/Prefab/Asset 编辑、Play Mode、截图和输入不属于 YokiFrame CLI 或 Workbench。
- AI 的执行规则位于三个包内 Skill；人类使用者需要理解具体 API 时继续阅读对应 Kit 主页面。
- `Kit_Status.md` 提供各层完成度；本目录与 `Documentation~/Guides/` 是 Workbench 的公开人类文档边界，AI 使用三个包内 Skill 获取操作路由。
- 第三方依赖选择、宏和安装入口位于 `Api/04-Reference/01-ThirdPartyRecommendations.md`。
- 本页不提供本地 Markdown 文档跳转链接，避免 Workbench 离线文档页在跳转时进入黑屏状态；需要更多资料时，从上述路径在文档目录中打开。
