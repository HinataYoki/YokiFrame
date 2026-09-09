---
name: yokiframe
description: Use when writing game code with YokiFrame Runtime APIs in a Unity or Godot project (architecture, events, FSM, pooling, resources, actions, singleton, save, audio, localization, spatial, tables), selecting the right Kit, or checking what a Kit can and cannot do. Route CLI work to yokiframe-cli and Avalonia Workbench or Installer work to yokiframe-workbench.
---

# YokiFrame Runtime API

## 受众与角色

本 Skill 面向**用户游戏项目中的 AI 助手**：项目已安装 YokiFrame，你的任务是用框架写业务代码、查询能力边界。它不是 YokiFrame 框架本身的开发手册；修改框架源码、迁移 Kit 或维护包内文档属于框架开发者职责。

## 职责与非目标

- 负责框架概览、Kit 选择和 Runtime API 使用边界
- 不负责 `yoki` 命令语法、Runtime 状态查询、FileBridge payload 或 Avalonia 页面操作
- 不负责 Unity 编译、Scene/Prefab/Asset/Play Mode/截图/输入自动化；这些能力属于当前环境中的外部工具
- 不直接构造、修改或删除 `.yokiframe` 协议文件
- 不修改已安装包内的源码或文档：Unity 包目录与 Godot `addons/yokiframe` 是 Installer 的受管交付物，发现文档与实际行为不一致时向用户报告差异并建议升级包版本，不自行改动

## 前置核实

1. 定位项目已安装的 YokiFrame 包根：Unity 本地嵌入为 `Packages/com.hinatayoki.yokiframe`；Unity Git URL 以 `Packages/manifest.json` 解析到的实际目录为准（通常在 `Library/PackageCache`），不要手改；Godot 为 `addons/yokiframe/package/YokiFrame`。`Documentation~/Api` 相对该包根解析
2. 读取 [Kit 能力索引](references/kit-index.md)，分别确认 Runtime API、Kit Interaction 和 Workbench 完成度
3. 需要具体行为或签名时，先读取对应 `Documentation~/Api` 主页面，再读取公开类型源码
4. 需要编写业务代码时，从 [usage-patterns.md](references/usage-patterns.md) 取对应 Kit 的最小骨架与生命周期归属，再回到 Kit 主页面核对完整约束；不要凭记忆编造 API
5. 需要在线状态、snapshot、telemetry 或 command 时切换到 `yokiframe-cli`
6. 处理配表、Luban schema、Excel 填表、生成失败或运行时加载时，读取 `references/tablekit-luban.md`，按 Workbench 保存的 TableKit 路径导入官方 Luban Skill，不要让用户复制提示词

## 执行步骤

1. 按 `kit-index.md` 选择已实现的 Runtime 门面，不用旧文档、空程序集或占位页面推断能力
2. 业务代码只依赖 Core 或当前 Tool 的公开 API；把宿主类型、生命周期和第三方实现留给既有 Adapter、Provider、Backend 或 Integration
3. 为事件订阅、资源 lease、状态机、动作 controller 和异步工作指定 owner、取消或释放路径
4. 首次真实调用允许既有宿主 Adapter 惰性创建默认 Store、Logger、Provider 或 Backend；显式注入始终优先
5. 发现框架行为与 Skill/文档描述不一致、或怀疑包版本过期时，向用户报告差异与证据；升级包版本属于用户决策，不由 AI 直接改动受管包
6. 官方 Luban Skill 存在时按其 `SKILL.md` 执行；生成仍走 Workbench/TableKit 已配置的主 `Luban.dll`。YokiFrame 只负责项目路径、TableKit 约束和官方 Skill 路由

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
| 人类使用入口 | 包根 `README.md`；快速上手之后进入对应 Kit 文档 |
| 面向用户的框架概览 | `Documentation~/Api/00-GettingStarted/FrameworkOverview.md` |
| 高频用法骨架、生命周期归属与常见误用 | [usage-patterns.md](references/usage-patterns.md) |
| CLI / Runtime evidence | `yokiframe-cli` |
| TableKit / Luban AI | [tablekit-luban.md](references/tablekit-luban.md) |
| Workbench / Installer | `yokiframe-workbench` |

## 维护触发条件（仅 YokiFrame 包开发者适用）

以下条目供框架开发者维护本 Skill 时使用；用户项目 AI 只读本 Skill，不执行维护。

- 新增或移除 Kit、公开 API、Provider、capability、Adapter 或 Integration
- Runtime、Interaction、Workbench 三层完成度任一变化
- 改变默认后端、资源所有权、线程、取消或生命周期语义
- 已确认旧 API、兼容壳或宿主入口不再存在
