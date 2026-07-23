# YokiFrame

> 面向 Unity 与 Godot .NET 的跨引擎 C# 游戏框架。

YokiFrame 是一个用 C# 构建游戏业务与工具链的模块化框架。它把可复用的游戏规则放在不依赖引擎的 Core 中，把 Unity、Godot 的 API 与生命周期差异隔离在独立 Adapter 中；同一套业务能力可以按项目需要组合，而不是被锁定在某个引擎或单体框架里。

它适合希望在 C# 游戏项目中获得清晰业务边界、可替换宿主实现，以及配套诊断与安装工具的团队。运行时保持 C# 9 兼容；Workbench、Installer 和 `yoki` CLI 使用 .NET 10，且不会进入游戏 Player。

**安装入口：**Unity 通过 Package Manager 的 Git URL 安装；Godot .NET 必须使用 Installer 安装，Installer 由源码包中的自举脚本在目标项目现场构建后启动。详细步骤见[安装](#安装)。

## 能做什么

| 你的目标 | YokiFrame 提供的能力 |
| --- | --- |
| 组织项目级业务 | `Architecture<T>` 用于组合服务、模型与系统，并管理初始化和释放。 |
| 构建游戏流程 | EventKit 提供类型/枚举事件；FsmKit 提供普通和带启动参数的状态机；ActionKit 用于顺序、并行、循环等动作编排。 |
| 管理常用运行时基础设施 | PoolKit、SingletonKit、LogKit、ResKit 与 SceneKit 分别覆盖对象复用、单例、日志、资源和场景生命周期。 |
| 实现常见游戏功能 | AudioKit、SaveKit、LocalizationKit、SpatialKit 与 TableKit 覆盖音频、存档、本地化、空间索引和数据表生成。 |
| 制作 Unity UI 与编辑器工具 | UIKit 提供面板、层级、动画、对话框、绑定和代码生成；InspectorKit 与 CodeGenKit 支持 Inspector 与结构化 C# 生成。 |
| 查看和维护项目 | Avalonia Workbench、`yoki` CLI 和 Installer 提供文档、状态诊断、受控操作、安装、更新与回滚。 |

## 核心设计

- **纯 C# Core**：业务规则不引用 Unity、Godot、Avalonia 或可选第三方库。
- **宿主 Adapter 隔离**：引擎 API、生命周期和默认后端位于独立 Adapter，依赖方向始终是 Adapter → Core。
- **按需组合**：Kit 可以独立使用；UniTask、YooAsset、DOTween、Luban、Nino 等接入保持可选。
- **运行时与工具链分离**：游戏代码走 Runtime API；诊断、项目配置、安装和命令行工作流留在 Editor/Tools 与 .NET 工具链中。
- **先读后写的工具协议**：Workbench 与 CLI 默认读取项目和运行态证据；会改写项目或宿主状态的操作必须显式触发。

## 能力地图

### 跨引擎 Runtime

| 分类 | Kit |
| --- | --- |
| 项目组合与通用类型 | Architecture、ToolClass |
| 事件、状态与日志 | EventKit、FsmKit、LogKit |
| 资源与生命周期 | ResKit、SceneKit、PoolKit、SingletonKit |
| 游戏功能 | ActionKit、AudioKit、SaveKit、LocalizationKit、SpatialKit |
| 数据与生成 | TableKit、CodeGenKit |

### Unity 专属能力

| 分类 | Kit |
| --- | --- |
| UI 框架 | UIKit：面板、层级、模态、动画、对话框、绑定与代码生成 |
| 编辑器基础设施 | InspectorKit 与 UIKit Inspector/生成工作流 |

`UIKit` 当前仅支持 Unity；它不会为 Godot 提供兼容壳。TableKit 是离线生成入口，只有生成项目代码后才会出现对应的 Runtime 类型。各 Kit 的 Runtime、运行态观察和 Workbench 页面完成度彼此独立，请以 [状态与入口](Documentation~/Api/00-GettingStarted/Kit_Status.md) 为准。

## 使用方式

业务代码直接依赖对应 Kit 的公开 API；宿主 Adapter 会在第一次真正需要时注册当前引擎的默认实现，项目也可以显式注入自己的 Provider 或 Backend。

```csharp
using YokiFrame;

public sealed class CombatFlow
{
    private readonly FSM<CombatState> mStateMachine = new();

    public void Start()
    {
        EventKit.Type.Send(new CombatStarted());
        mStateMachine.ChangeState(CombatState.Opening);
    }
}
```

从一个清晰的业务边界开始，选择一个 Kit，而不是先接入所有模块。更多最小示例和生命周期约束见[框架概览](Documentation~/Api/00-GettingStarted/Entrypoints.md)。

## 支持范围

| 项目 | 当前范围 |
| --- | --- |
| Unity | `2022.3+`；支持本地 embedded package 与 Git URL 安装。 |
| Godot | 仅支持 .NET 版本；当前开发和验证基线为 `4.7 .NET`，这不是产品支持上限。 |
| Runtime | C# 9 兼容的跨宿主 Core 与匹配 Adapter。 |
| 工具链 | .NET 10 + Avalonia Workbench、Installer 与 `yoki` CLI。 |

YokiFrame 不替代 Unity 或 Godot 的 Scene、Prefab、Asset、Play Mode、截图和输入自动化能力；这些仍应由对应引擎或专用工具负责。

## 安装

### 安装前准备

| 目标 | 必要条件 |
| --- | --- |
| Unity | Unity `2022.3+`，并让 Unity Package Manager 可使用 Git。首次打开 Workbench 时，本机还需要 .NET 10 SDK 来构建项目工具 Runtime。 |
| Godot | Godot **.NET** 版本、本机 .NET 10 SDK，以及一个完整的本地 YokiFrame 源码包。标准版 Godot 不支持 C#，不能使用本框架。 |

YokiFrame 的 Git URL 和源码包不携带 Workbench、Installer 或 CLI 二进制。工具 Runtime 会在需要时按当前源码生成到目标项目的 `.yokiframe/runtime/com.hinatayoki.yokiframe/<sourceFingerprint>/`；它是项目缓存，可以删除后重新生成，绝不会写回源码包。

### Unity：通过 Git URL 安装

1. 在 Unity 中打开目标项目，进入 `Window > Package Manager`。
2. 点击左上角 `+`，选择 **Add package from git URL**。
3. 输入以下地址并确认：

   ```text
   https://github.com/HinataYoki/YokiFrame.git
   ```

   需要固定版本时，在地址后追加 `#<tag-or-commit>`，例如：

   ```text
   https://github.com/HinataYoki/YokiFrame.git#<tag-or-commit>
   ```

4. 等待 Package Manager 完成解析和编译。导入完成后，通过 `YokiFrame/Workbench/Open` 或 `Ctrl+E` 打开 Workbench。
5. 若当前项目没有可复用的 Workbench，首次 `Ctrl+E` 会使用本机 .NET 10 SDK，把与当前 Git 源码匹配的工具 Runtime 构建到该 Unity 项目的 `.yokiframe` 缓存中，再打开 Workbench；已有 Runtime 时会直接打开，不等待重复编译。
6. Workbench 打开后会在后台检查源码是否有新版。发现新版时，页头会显示“重新编译新版”按钮；只有点击该按钮才会构建新 Runtime。窗口关闭会取消尚未完成的检查或构建，已被旧进程占用的缓存目录会延迟到后续启动清理。

也可以手动在 `Packages/manifest.json` 中声明 Git 依赖：

```json
{
  "dependencies": {
    "com.hinatayoki.yokiframe": "https://github.com/HinataYoki/YokiFrame.git#<tag-or-commit>"
  }
}
```

Unity Git URL 与 Installer 管理的本地 embedded package 是互斥来源；同一项目只能选择其中一种。不要把 Git URL 包直接复制到 `Assets/`，也不要手动修改 Unity 的 Package Cache。

### Godot .NET：从源码包启动 Installer

Godot 不支持直接把 Git URL 或源码目录复制到 `addons/yokiframe`。必须由 Installer 完成安装、校验、备份和替换。开始前获取完整源码包，例如：

```powershell
git clone https://github.com/HinataYoki/YokiFrame.git
```

这里的“源码包根”是包含 `package.json`、`Core/`、`Tools/` 与 `YokiFrameWorkbench~/` 的目录。请从**源码包**运行下列脚本，传入目标 Godot 项目根；不要从目标项目的 `addons/yokiframe` 或 `.yokiframe` 缓存目录运行它。

#### Windows

```powershell
$packageRoot = "D:\Source\YokiFrame"
$godotProjectRoot = "D:\Games\MyGodotGame"

& "$packageRoot\YokiFrameWorkbench~\scripts\runtime-bootstrap\install-godot.cmd" --project $godotProjectRoot
```

#### Linux

```sh
package_root="/path/to/YokiFrame"
godot_project_root="/path/to/MyGodotGame"

sh "$package_root/YokiFrameWorkbench~/scripts/runtime-bootstrap/install-godot.sh" --project "$godot_project_root"
```

#### macOS

```sh
package_root="/path/to/YokiFrame"
godot_project_root="/path/to/MyGodotGame"

sh "$package_root/YokiFrameWorkbench~/scripts/runtime-bootstrap/install-godot.command" --project "$godot_project_root"
```

脚本会执行以下工作：

1. 检查本机是否可用 .NET 10 SDK。
2. 根据当前源码计算指纹，在**目标 Godot 项目**的 `.yokiframe/runtime/com.hinatayoki.yokiframe/<sourceFingerprint>/` 构建或复用当前平台的 Workbench、Installer 和 `yoki` Runtime。
3. 将该项目缓存标记为当前源码版本，然后自动打开 Installer，并预填源码包与 Godot 项目路径。

Installer 打开后，选择或确认 **Godot local** 模式，核对源码包和目标项目路径，先生成安装 plan；确认 plan 后再执行 apply。Installer 会完整暂存并校验新的 `addons/yokiframe`，备份旧目录，再原子替换；失败时会恢复备份。

安装完成后重新打开 Godot 项目，等待插件导入完成。更新时从新的或更新后的源码包重复运行同一 `install-godot` 脚本，再在 Installer 中审阅 plan 并 apply。不要手动修改 `addons/yokiframe`：Godot 更新不会进行文件级合并，受管目录中的手工修改会在完整替换时丢失。

如果 Installer 提示 Runtime 缓存缺失、过期或与所选源码包指纹不一致，请再次从该源码包运行 `install-godot`；也可以在 Installer 中使用“构建 Runtime 并重新打开”恢复。

## 工具链

- **Workbench**：以图形界面查看框架、Kit、离线文档、项目状态和已接入的运行态诊断。
- **`yoki` CLI**：用于脚本化查询、诊断和已声明的受控操作；默认以只读查询为主。
- **Installer**：支持 Unity embedded package、Unity Git URL 和 Godot 本地安装；每次安装或更新都会先生成 plan，并提供校验与回滚边界。

完整的工具命令、安装事务和运行时诊断说明见[工具链指南](Documentation~/Guides/Tooling.md)。

## 从这里开始阅读

- [框架概览与第一个 Runtime API](Documentation~/Api/00-GettingStarted/Entrypoints.md)
- [状态与入口](Documentation~/Api/00-GettingStarted/Kit_Status.md)
- [Core API](Documentation~/Api/02-Core)
- [游戏功能 Kit](Documentation~/Api/03-Tool)
- [Workbench、CLI 与 Installer](Documentation~/Guides/Tooling.md)
- [第三方依赖建议](Documentation~/Api/04-Reference/01-ThirdPartyRecommendations.md)

## 仓库结构

```text
Core/                 跨引擎 Runtime、共享 Editor 能力与宿主 Adapter
Tools/                可选游戏功能 Kit
Documentation~/       公开 API 与使用指南，可由 Workbench 离线浏览
YokiFrameWorkbench~/  Avalonia Workbench、Installer、CLI 与测试源码
```

## 版本与兼容性

YokiFrame 2.x 是一次全新架构落地，不承担 1.x 的 API、序列化资产、配置或存档兼容。历史更新记录不再维护；请以当前 README、[Kit 状态](Documentation~/Api/00-GettingStarted/Kit_Status.md) 和各 Kit 文档判断可用能力与边界。

许可见 [LICENSE](LICENSE)。
