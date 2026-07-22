---
name: yokiframe
description: Use when Codex needs to select or use a YokiFrame Runtime API, verify a Kit migration state, or reason about Core, Adapter, Tool, Provider, Backend, and Integration boundaries. Route CLI work to yokiframe-cli and Avalonia Workbench or Installer work to yokiframe-workbench.
---

# YokiFrame Runtime API

## 职责与非目标

- 负责框架概览、Kit 选择和 Runtime API 使用边界
- 不负责 `yoki` 命令语法、Runtime 状态查询、FileBridge payload 或 Avalonia 页面操作
- 不负责 Unity 编译、Scene/Prefab/Asset/Play Mode/截图/输入自动化；这些能力属于当前环境中的外部工具
- 不直接构造、修改或删除 `.yokiframe` 协议文件

## 前置核实

1. 定位项目实际使用的 YokiFrame 包根，不假设固定为 `Assets/YokiFrame`
2. 读取 [Kit 能力索引](references/kit-index.md)，分别确认 Runtime API、Kit Interaction 和 Workbench 完成度
3. 需要具体行为或签名时，先读取对应 `Documentation~/Api` 主页面，再读取公开类型源码
4. 需要在线状态、snapshot、telemetry 或 command 时切换到 `yokiframe-cli`

## 执行步骤

1. 按 `kit-index.md` 选择已实现的 Runtime 门面，不用旧文档、空程序集或占位页面推断能力
2. 业务代码只依赖 Core 或当前 Tool 的公开 API；把宿主类型、生命周期和第三方实现留给既有 Adapter、Provider、Backend 或 Integration
3. 为事件订阅、资源 lease、状态机、动作 controller 和异步工作指定 owner、取消或释放路径
4. 首次真实调用允许既有宿主 Adapter 惰性创建默认 Store、Logger、Provider 或 Backend；显式注入始终优先
5. 改动公开 API、Kit 状态或宿主入口时，同步更新 Kit 主页面、`kit-index.md` 和相关 CLI/Workbench Skill

## 副作用边界

| 边界 | 规则 |
|---|---|
| Core | `YokiFrame` 不引用 Unity、Godot、Avalonia、Tools 或可选第三方库 |
| Adapter | 仅位于匹配 `Adapters/<Engine>` 独立边界，单向依赖 Core，并使用整文件宿主宏 |
| Tool | 只依赖 Core；不新建平行对象池、日志、资源加载、事件或状态机基础设施 |
| Runtime 初始化 | 不恢复全局 `YokiFrameKit.Initialize`；由宿主工厂惰性安装默认实现 |
| Interaction | 只在 Editor/Tools 编译；未完成 Provider 的 Kit 不伪造在线状态 |
| UIKit | Unity 专属；不创建 Godot Adapter、`IUIBackend` 或 `UIKit.SetBackend` |

## 引用路由

| 需要的信息 | 读取位置 |
|---|---|
| Kit 完成度与主入口 | [kit-index.md](references/kit-index.md) |
| 人类可读 API、示例和限制 | `Documentation~/Api/` 对应 Kit 主页面 |
| 所有新版入口 | `Documentation~/Api/00-GettingStarted/Entrypoints.md` |
| 面向用户的架构概览 | `Documentation~/Api/01-Architecture/Architecture.md` |
| CLI / Runtime evidence | `yokiframe-cli` |
| Workbench / Installer | `yokiframe-workbench` |

## 维护触发条件

- 新增或移除 Kit、公开 API、Provider、capability、Adapter 或 Integration
- Runtime、Interaction、Workbench 三层完成度任一变化
- 改变默认后端、资源所有权、线程、取消或生命周期语义
- 已确认旧 API、兼容壳或宿主入口不再存在
