# YokiFrame

<div align="center">

**面向 Unity 与 Godot .NET 的跨引擎 C# 游戏框架**

[![Unity](https://img.shields.io/badge/Unity-2022.3%2B-blue.svg?style=flat-square)](https://unity.com/)
[![Godot](https://img.shields.io/badge/Godot-4.x%20.NET-blue.svg?style=flat-square)](https://godotengine.org/)
[![Version](https://img.shields.io/badge/Version-2.0.0--preview-orange.svg?style=flat-square)](CHANGELOG.md)
[![License](https://img.shields.io/badge/License-MIT-green.svg?style=flat-square)](LICENSE)

</div>

---

## 📖 简介

**YokiFrame** 是一套用同一份 C# 业务代码同时支撑 **Unity 2022.3+** 与 **Godot .NET** 的跨引擎游戏框架。它把游戏开发中最常见的基础工作拆成一组可独立组合的 Kit：组织业务服务、传递事件、管理状态、编排流程、加载资源、播放音频、保存数据、处理本地化和搭建 Unity UI。业务规则写在纯 C# Core 中，引擎 API、生命周期与默认后端由各自的宿主 Adapter 提供，因此换引擎不换业务代码。

### ✨ 核心特性

- 🚀 **跨引擎** - 同一套 C# 业务代码，Unity 与 Godot .NET 通用，宿主差异由 Adapter 隔离
- 🧩 **Kit 即插即用** - 每个 Kit 可独立引入、组合或移除，按需取用
- 🛠️ **统一工具链** - Avalonia Workbench 可视化观测、`yoki` CLI 脚本化操作、Installer 一键安装
- ⚙️ **可选依赖解耦** - UniTask、YooAsset、DOTween、Luban、Nino 等按需接入，不引入不依赖
- 🧹 **零初始化仪式** - 无全局初始化入口，默认后端在第一次真实调用时惰性创建
- 🔍 **可观测性** - FileBridge 命令协议与共享内存遥测，为编辑器与 AI 工具提供可靠的诊断通道

---

## 📚 目录

- [快速开始](#-快速开始)
- [核心模块](#-核心模块)
- [工具链](#-工具链)
- [项目结构](#-项目结构)
- [系统要求](#-系统要求)
- [文档导航](#-文档导航)
- [贡献与支持](#-贡献与支持)

---

## 🚀 快速开始

### 环境要求

| 环境 | 要求 |
|------|------|
| Unity | 2022.3 或更高 |
| Godot | 4.x .NET（当前开发与验证基线为 4.7，不支持非 .NET Godot） |
| 工具链 | Windows / macOS / Linux，构建 Workbench 与 `yoki` 时需 .NET 10 SDK |
| 业务代码 | C# 9.0 兼容语法 |

### 安装：Unity

1. 打开 `Window > Package Manager`，点击 `+`，选择 **Add package from git URL**。
2. 输入：

   ```text
   https://github.com/HinataYoki/YokiFrame.git
   ```

3. 等待导入和编译完成。

### 安装：Godot .NET

从完整 YokiFrame 源码包根目录运行对应脚本，传入 Godot 项目路径：

```powershell
& "<packageRoot>\YokiFrameWorkbench~\scripts\runtime-bootstrap\install-godot.cmd" --project "<godotProjectRoot>"
```

Linux / macOS 使用同目录下的 `install-godot.sh` 或 `install-godot.command`。脚本会构建 Runtime 并打开 Installer 图形界面，在 Installer 中选择 **Godot local**、确认 plan 并执行 apply 即可。安装完成后请关闭并重新打开 Godot 项目，让编辑器重新扫描已生成的托管程序集。

> 💡 **提示**：构建失败时查看 Installer 返回的 dotnet 编译输出；详细说明见 [故障排查](Documentation~/Guides/Troubleshooting.md)。

### 交给 AI 安装

不想手动操作时，直接把本仓库地址（或本地源码包目录）和“安装 YokiFrame”一起交给 AI 即可。AI 会自动完成编译、Runtime 构建、安装计划与结果校验，详细流程见 [AI 安装指引](Documentation~/Guides/AI-Install.md)。

### 写一个流程

用一个例子串联起框架最常见的四件事：定义架构、注册服务、发布/订阅事件、编排异步流程。

```csharp
using YokiFrame;

// 1. 定义架构并注册业务服务
public sealed class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void OnInit() => Register<SessionService>(new SessionService());
}

public readonly struct SessionStarted
{
}

// 2. 业务服务通过 EventKit 发布事件
public sealed class SessionService : AbstractService
{
    public void StartSession() => EventKit.Type.Send(new SessionStarted());
}

// 3. 在业务入口调用服务
GameArchitecture.Interface
    .GetService<SessionService>()
    .StartSession();

// 4. 订阅事件；返回的 link 用于在模块停用时注销
LinkUnRegister<SessionStarted> link =
    EventKit.Type.Register<SessionStarted>(_ => LogKit.Info("Session started"));

// 5. 用 ActionKit 编排异步流程
IActionController flow = ActionKit.Sequence()
    .Callback(() => LogKit.Info("Loading"))
    .Delay(0.5f)
    .Callback(() => LogKit.Info("Ready"))
    .Start();
```

`GameArchitecture.Interface` 第一次访问时创建架构并初始化服务。事件订阅由订阅方注销；仍在运行的动作由创建它的业务 owner 调用 `Cancel()`。更多生命周期约定见 [生命周期与所有权](Documentation~/Guides/Lifecycle-and-Ownership.md)。

---

## 🧩 核心模块

### 架构

| 模块 | 作用 | 文档 |
|------|------|------|
| **Architecture** | 组合服务、模型和系统，提供统一的业务边界 | [Architecture](Documentation~/Api/01-Architecture/Architecture.md) |

### Core Kit

| 模块 | 作用 | 文档 |
|------|------|------|
| **EventKit** | 类型事件、枚举事件和对象内事件 | [EventKit](Documentation~/Api/02-Core/EventKit.md) |
| **FsmKit** | 有限状态机、状态转换和历史 | [FsmKit](Documentation~/Api/02-Core/FsmKit.md) |
| **LogKit** | 统一日志入口和宿主后端 | [LogKit](Documentation~/Api/02-Core/LogKit.md) |
| **PoolKit** | 对象复用和池生命周期 | [PoolKit](Documentation~/Api/02-Core/PoolKit.md) |
| **ResKit** | 资源加载、Provider、handle 和卸载 | [ResKit](Documentation~/Api/02-Core/ResKit.md) |
| **SingletonKit** | 纯 C# 单例 | [SingletonKit](Documentation~/Api/02-Core/SingletonKit.md) |
| **ToolClass** | 通用集合、绑定值和字符串工具 | [ToolClass](Documentation~/Api/02-Core/ToolClass.md) |
| **CodeGenKit** | 编辑器代码生成基础能力 | [CodeGenKit](Documentation~/Api/02-Core/CodeGenKit.md) |
| **InspectorKit** | Unity Inspector 基础控件 | [InspectorKit](Documentation~/Api/02-Core/InspectorKit.md) |

### Tool Kit

| 模块 | 作用 | 文档 |
|------|------|------|
| **ActionKit** | 顺序、并行、条件、延迟和异步动作 | [ActionKit](Documentation~/Api/03-Tool/ActionKit.md) |
| **AudioKit** | 音频资源、总线和播放状态 | [AudioKit](Documentation~/Api/03-Tool/AudioKit.md) |
| **LocalizationKit** | JSON 或 Luban 表格本地化 | [LocalizationKit](Documentation~/Api/03-Tool/LocalizationKit.md) |
| **SaveKit** | 存档读写、版本和迁移 | [SaveKit](Documentation~/Api/03-Tool/SaveKit.md) |
| **SceneKit** | 场景预加载、切换和卸载 | [SceneKit](Documentation~/Api/03-Tool/SceneKit.md) |
| **SpatialKit** | HashGrid、Quadtree 和 Octree 空间查询 | [SpatialKit](Documentation~/Api/03-Tool/SpatialKit.md) |
| **TableKit** | Luban 配置生成和运行时读取 | [TableKit](Documentation~/Api/03-Tool/TableKit.md) |
| **UIKit** | Unity 面板、绑定、动画和代码生成 | [UIKit](Documentation~/Api/03-Tool/UIKit.md) |

---

## 🛠️ 工具链

| 工具 | 用途 |
|------|------|
| **Workbench** | Avalonia 桌面工具，实时查看项目状态、Kit 运行态与诊断信息 |
| **`yoki` CLI** | 脚本化读取、诊断与执行已声明的受控操作，默认只读 |
| **Installer** | 图形化安装、更新与回滚，支持 Unity 本地 / Git URL 与 Godot 本地安装 |

工具链面向开发者和 AI 工具，运行时诊断不会进入 Player 构建。安装相关的自动化流程见 [AI 安装指引](Documentation~/Guides/AI-Install.md)。

---

## 📁 项目结构

```text
YokiFrame/                     # 包根（同时是 Unity Git URL 包根）
├── Core/                      # 跨引擎核心
│   ├── Runtime/               # 纯 C# 业务能力（YokiFrame 程序集）
│   ├── Editor/                # Unity Editor 与 Godot Tools 共用的纯 C# 工具
│   ├── Adapters/              # 宿主适配层（Unity / Godot）
│   ├── Integrations/          # 可选第三方依赖接入
│   └── Tests/
├── Tools/                     # 各 Kit（ActionKit / UIKit / SaveKit / ...）
├── YokiFrameWorkbench~/       # Workbench、CLI、Installer 源码（不参与 Unity 编译）
└── Documentation~/            # 文档（API、指南、第三方依赖建议）
```

---

## 💻 系统要求

| 项目 | 要求 |
|------|------|
| Unity | 2022.3 或更高 |
| Godot | 4.x .NET（当前开发与验证基线为 4.7） |
| .NET | 工具链（Workbench / CLI / Installer）基于 .NET 10 |
| C# | Runtime 业务代码保持 C# 9.0 兼容 |
| 平台 | 工具链支持 Windows、macOS、Linux |

---

## 📚 文档导航

| 文档 | 描述 |
|------|------|
| [框架概览](Documentation~/Api/00-GettingStarted/FrameworkOverview.md) | 新手入口：适用场景、Kit 状态与关键边界 |
| [AI 安装指引](Documentation~/Guides/AI-Install.md) | 安装与 AI 自动化安装的完整流程 |
| [生命周期与所有权](Documentation~/Guides/Lifecycle-and-Ownership.md) | 事件、资源、动作与异步工作的 owner 约定 |
| [故障排查](Documentation~/Guides/Troubleshooting.md) | 常见问题与诊断方法 |
| [第三方依赖建议](Documentation~/Api/04-Reference/01-ThirdPartyRecommendations.md) | YooAsset、UniTask、DOTween、Luban 等接入建议 |
| [Changelog](CHANGELOG.md) | 版本更新记录 |
| [License](LICENSE) | MIT 开源许可 |

---

## 🤝 贡献与支持

欢迎提交 [Issue](https://github.com/HinataYoki/YokiFrame/issues) 和 Pull Request。

如果 YokiFrame 对你有帮助，欢迎 [⭐ Star](https://github.com/HinataYoki/YokiFrame) 支持项目发展。

---

## 源框架

YokiFrame 是基于 [QFramework](https://github.com/liangxiegame/QFramework) 演化的衍生框架。
