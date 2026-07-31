# YokiFrame

面向 Unity 与 Godot .NET 的跨引擎 C# 游戏框架。YokiFrame 用一组可独立组合的 Kit，覆盖游戏中常见的基础工作：组织业务服务、传递事件、管理状态、编排流程、加载资源、播放音频、保存数据、处理本地化、查询空间以及搭建 Unity UI。

## 快速上手

下面只做三件事：把框架加入项目、跑通一个业务流程、按需求选择 Kit。

### 1. 安装

**Unity Git URL**

1. 打开 `Window > Package Manager`，点击 `+`，选择 **Add package from git URL**。
2. 输入：

   ```text
   https://github.com/HinataYoki/YokiFrame.git
   ```

3. 等待导入和编译完成。需要固定版本时，在地址后追加 `#<tag-or-commit>`。

Unity Git URL 与本地包只能选一种，不要把 Git URL 包复制到 `Assets/`。

**Godot .NET**

从完整 YokiFrame 源码包根目录运行对应脚本，并传入 Godot 项目路径。该脚本会先构建 Runtime，再打开 Installer 图形界面：

```powershell
& "<packageRoot>\YokiFrameWorkbench~\scripts\runtime-bootstrap\install-godot.cmd" --project "<godotProjectRoot>"
```

Linux 和 macOS 使用同目录下的 `install-godot.sh` 或 `install-godot.command`，然后在 Installer 中选择 **Godot local**、确认 plan 并执行 apply。也可以直接打开图形 Installer：首次选择 Godot 项目时，如果项目 Runtime 缓存不存在，Installer 会自动构建缓存并继续生成安装计划。

新建的 Godot .NET 空项目即使还没有顶层 `.csproj` 也可以安装；Installer 会根据 Godot .NET 证据和 `project.godot` 的程序集名在 apply 事务中生成主项目文件。提交后，Installer 会在需要时使用 `-p:GodotTarget=Editor` 自动执行目标项目的 `dotnet restore` 和 `dotnet build`，编译包含 `TOOLS` 的 Editor 程序集，并确认 `.godot/mono/temp/bin/Debug/<assembly>.dll` 已生成；构建完成后还会重新登记 `res://addons/yokiframe/plugin.cfg`，防止 Godot 扫描竞态导致插件被禁用。普通非 .NET Godot 项目不受支持。

Godot 安装完成后如果编辑器当时已经打开，请先关闭并重新打开项目，让 Godot 重新扫描已生成的托管程序集；不要单独复制或修改 `YokiFrameGodotEditorPlugin.cs`。构建失败时直接查看 Installer 返回的 dotnet 编译输出。

仓库 clone 下来的源码包不包含预编译的 Workbench 和 `yoki`。首次从源码包启动 Godot Installer，或让 AI 执行自动安装时，会先构建项目 Runtime；Windows 需要 `.NET 10 SDK` 和 `Visual Studio 2022 C++ Build Tools`。Unity Git URL 本身不需要这些工具。

需要 AI 自动完成安装时，直接把本 README 地址和“安装 YokiFrame”一起交给 AI；AI 会继续读取 [AI 安装指引](Documentation~/Guides/AI-Install.md)，完成编译、Runtime bootstrap、安装计划和结果校验。

### `.yokiframe` 存储清理

YokiFrame 会在项目 `.yokiframe` 中保存 FileBridge 命令证据、Workbench 启动诊断和 Runtime 缓存指针。宿主启动以及命令处理期间会自动清理已完成的旧文件：`archive` 和 `results` 默认保留 7 天或最近 200 个文件，`deadletter` 默认保留 30 天或最近 200 个文件，Workbench 启动日志默认保留 14 天或最近 20 个文件。清理只触及这些白名单目录，不删除 pending/processing 命令、snapshot、heartbeat、项目模型或当前 Runtime 指纹；被占用的文件会留到下一轮重试。

不要手动删除或修改 `.yokiframe` 下的协议文件。需要保留更长诊断证据时，应在清理前复制对应目录中的文件。

### 2. 写一个流程

先定义业务架构和服务，再用 EventKit 解耦模块，用 ActionKit 编排等待和回调：

```csharp
using YokiFrame;

public sealed class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void OnInit()
    {
        Register<SessionService>(new SessionService());
    }
}

public readonly struct SessionStarted
{
}

public sealed class SessionService : AbstractService
{
    protected override void OnInit()
    {
    }

    public void StartSession()
    {
        EventKit.Type.Send(new SessionStarted());
    }
}

// 在业务入口调用
GameArchitecture.Interface
    .GetService<SessionService>()
    .StartSession();

// 在模块启用时订阅，在模块停用时注销
LinkUnRegister<SessionStarted> link =
    EventKit.Type.Register<SessionStarted>(_ => LogKit.Info("Session started"));

IActionController flow = ActionKit.Sequence()
    .Callback(() => LogKit.Info("Loading"))
    .Delay(0.5f)
    .Callback(() => LogKit.Info("Ready"))
    .Start();
```

`GameArchitecture.Interface` 第一次访问时会创建架构并初始化服务。事件订阅由订阅方注销；仍在运行的动作由创建它的业务 owner 调用 `Cancel()`。

### 3. 选择你要解决的问题

| 你要做什么 | 使用 Kit |
| --- | --- |
| 组合服务、模型和系统 | [Architecture](Documentation~/Api/01-Architecture/Architecture.md) |
| 解耦模块之间的通知 | [EventKit](Documentation~/Api/02-Core/EventKit.md) |
| 管理业务状态和状态转换 | [FsmKit](Documentation~/Api/02-Core/FsmKit.md) |
| 编排等待、并行、回调和异步流程 | [ActionKit](Documentation~/Api/03-Tool/ActionKit.md) |
| 加载资源、管理 handle 和场景 | [ResKit](Documentation~/Api/02-Core/ResKit.md)、[SceneKit](Documentation~/Api/03-Tool/SceneKit.md) |
| 记录日志、复用对象、管理单例 | [LogKit](Documentation~/Api/02-Core/LogKit.md)、[PoolKit](Documentation~/Api/02-Core/PoolKit.md)、[SingletonKit](Documentation~/Api/02-Core/SingletonKit.md) |
| 播放音频、保存数据、做本地化 | [AudioKit](Documentation~/Api/03-Tool/AudioKit.md)、[SaveKit](Documentation~/Api/03-Tool/SaveKit.md)、[LocalizationKit](Documentation~/Api/03-Tool/LocalizationKit.md) |
| 查询实体空间位置 | [SpatialKit](Documentation~/Api/03-Tool/SpatialKit.md) |
| 从 Luban 数据表生成 C# 类型 | [TableKit](Documentation~/Api/03-Tool/TableKit.md) |
| 搭建 Unity 面板、绑定和 Inspector | [UIKit](Documentation~/Api/03-Tool/UIKit.md)、[InspectorKit](Documentation~/Api/02-Core/InspectorKit.md) |

## Kit 详细文档

快速上手之后，按具体能力阅读对应 Kit 页面。每个页面都按“使用前提 → 快速上手 → API → 生命周期与限制”的顺序组织。

### 架构

- [Architecture](Documentation~/Api/01-Architecture/Architecture.md)：服务、模型和系统的组合边界。

### Core Kit

- [EventKit](Documentation~/Api/02-Core/EventKit.md)：类型事件、枚举事件和对象内事件。
- [FsmKit](Documentation~/Api/02-Core/FsmKit.md)：有限状态机、状态转换和历史。
- [LogKit](Documentation~/Api/02-Core/LogKit.md)：统一日志入口和宿主后端。
- [PoolKit](Documentation~/Api/02-Core/PoolKit.md)：对象复用和池生命周期。
- [ResKit](Documentation~/Api/02-Core/ResKit.md)：资源加载、Provider、handle 和卸载。
- [SingletonKit](Documentation~/Api/02-Core/SingletonKit.md)：纯 C# 单例。
- [ToolClass](Documentation~/Api/02-Core/ToolClass.md)：通用集合、绑定值和字符串工具。
- [CodeGenKit](Documentation~/Api/02-Core/CodeGenKit.md)：编辑器代码生成基础能力。
- [InspectorKit](Documentation~/Api/02-Core/InspectorKit.md)：Unity Inspector 基础控件。

### Tool Kit

- [ActionKit](Documentation~/Api/03-Tool/ActionKit.md)：顺序、并行、条件、延迟和异步动作。
- [AudioKit](Documentation~/Api/03-Tool/AudioKit.md)：音频资源、总线和播放状态。
- [LocalizationKit](Documentation~/Api/03-Tool/LocalizationKit.md)：JSON 或 Luban 表格本地化。
- [SaveKit](Documentation~/Api/03-Tool/SaveKit.md)：存档读写、版本和迁移。
- [SceneKit](Documentation~/Api/03-Tool/SceneKit.md)：场景预加载、切换和卸载。
- [SpatialKit](Documentation~/Api/03-Tool/SpatialKit.md)：HashGrid、Quadtree 和 Octree 空间查询。
- [TableKit](Documentation~/Api/03-Tool/TableKit.md)：Luban 配置生成和运行时读取。
- [UIKit](Documentation~/Api/03-Tool/UIKit.md)：Unity 面板、绑定、动画和代码生成。

### 第三方依赖

需要 YooAsset、UniTask、DOTween、FMOD、Nino 或 Luban 时，先看 [第三方依赖建议](Documentation~/Api/04-Reference/01-ThirdPartyRecommendations.md)，再打开对应 Kit 页面中的接入章节。

许可见 [LICENSE](LICENSE)。

## 源框架

YokiFrame 是基于 [QFramework](https://github.com/liangxiegame/QFramework) 演化的衍生框架。
