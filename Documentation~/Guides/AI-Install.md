# YokiFrame AI 安装指引

本文供 AI 执行安装任务使用。人类用户只需阅读包根 [README](../../README.md)；当用户说“安装 YokiFrame”时，先读取本文，再执行下面的检查和命令。

本文的目标是把 YokiFrame 安装到用户指定的 Unity 或 Godot .NET 项目。不要把“源码编译成功”当成“安装完成”：安装还必须生成项目 Runtime、执行 Installer 事务并完成目标项目校验。

## 执行契约

1. 先识别源码包根和目标项目根，确认路径没有指向 `addons/yokiframe`、`.yokiframe` 或构建缓存。
2. 源码包没有预编译的 Workbench 和 `yoki`。第一次安装必须先执行 Runtime bootstrap，让它编译并发布 Workbench 与 CLI。
3. 先运行 `installer plan`，检查目标路径、动作、warning 和冲突；确认计划可接受后再运行同参数的 `installer apply`。
4. 不直接复制 `addons/yokiframe`，不手工修改 `.yokiframe`，不在发现用户修改时静默覆盖。
5. 只有退出码、CLI JSON 状态和目标文件校验都成功，才能向用户报告安装完成。

## 输入与路径

从用户消息、当前工作目录或文件系统中确定以下变量：

| 变量 | 含义 | 识别方式 |
| --- | --- | --- |
| `<packageRoot>` | 完整 YokiFrame 源码包根 | 同时存在 `package.json`、`Core/`、`Tools/` 和 `YokiFrameWorkbench~/` |
| `<projectRoot>` | 目标 Unity 或 Godot 项目根 | Unity 或 Godot 项目文件位于该目录顶层 |
| `<source>` | 本地安装来源 | 通常等于 `<packageRoot>` |
| `<yoki>` | 项目 Runtime 中的 CLI | bootstrap 输出的 `CLI:` 路径，或从 `tool-manifest.json` 解析 |

不要假设 clone 目录中存在 `yoki.exe`、`yoki` 或 Workbench 可执行文件；这些文件只会在目标项目的 `.yokiframe/runtime/` 中生成。

## 编译前置条件

### 所有平台

- 必须安装 .NET 10 SDK。检查 `dotnet --list-sdks`，输出中必须有 `10.x`；只有运行时没有 SDK 不够。
- AI 必须拥有源码包和目标项目的读取、写入权限。

如果 bootstrap 的自动 restore 失败，再检查网络是否能访问 NuGet 源，或本机是否已有所需依赖缓存；这属于编译依赖问题，不需要把 NuGet 作为额外安装环境交给用户准备。

### Windows

当前 Windows 默认发布 profile 是 `win-x64-aot`，Workbench GUI 和 `yoki` CLI 都会执行 Native AOT 发布。因此需要：

- Visual Studio 2022 或 Visual Studio 2022 Build Tools。
- `Desktop development with C++` 工作负载。
- MSVC x64/x86 构建工具和 Windows SDK。

官方入口：[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)；[Visual C++ Build Tools](https://visualstudio.microsoft.com/visual-cpp-build-tools/)。如果发布日志出现 `Platform linker not found`、`cl.exe`、`Visual Studio 2022` 或 `Microsoft.VisualStudio.Component.VC.Tools.x86.x64`，先修复 C++ 工具链，不要重复执行安装。

### Linux 与 macOS

当前 profile 为 managed 发布，不使用 Windows Native AOT 的 C++ 工具链；仍然需要 .NET 10 SDK、平台可用的 `dotnet` 和目标项目写入权限。当前允许的 profile 是 `linux-x64`、`osx-x64` 和 `osx-arm64`。

## 编译并生成 Runtime

不要先运行不存在的 `<yoki>`。首次安装的权威入口是 `runtime bootstrap`，它会编译并发布 Workbench GUI、CLI，并把产物写入目标项目的 `.yokiframe/runtime/`。Windows PowerShell 示例：

```powershell
$packageRoot = (Resolve-Path "<packageRoot>").Path
$projectRoot = (Resolve-Path "<projectRoot>").Path
$packagingProject = Join-Path $packageRoot "YokiFrameWorkbench~\src\YokiFrame.Packaging\YokiFrame.Packaging.csproj"

dotnet run --project $packagingProject -- runtime bootstrap `
  --package-root $packageRoot `
  --project-root $projectRoot `
  --configuration Release
```

Linux/macOS 使用同一命令，将路径分隔符改为 `/` 并去掉 PowerShell 的反引号换行。不要追加 `--open-installer`：该开关会启动 GUI，不能用于无交互 AI 安装。

如果只执行 `dotnet build YokiFrame.Workbench.slnx`，它只能验证源码或生成开发构建，不能替代上面的 Runtime bootstrap；Installer CLI 必须来自目标项目 Runtime 缓存。

bootstrap 成功时会输出 `Source fingerprint`、`Runtime root`、`GUI`、`CLI` 和 `Manifest`。正常布局为：

```text
<projectRoot>/.yokiframe/runtime/com.hinatayoki.yokiframe/
├── current.json
└── <sourceFingerprint>/
    ├── tool-manifest.json
    └── <profile>/
        ├── YokiFrame.Workbench.Avalonia[.exe]
        └── yoki[.exe]
```

实际 `current.json` 路径是：

```text
<projectRoot>/.yokiframe/runtime/com.hinatayoki.yokiframe/current.json
```

Godot local 安装会校验 `current.json` 中的 `sourceFingerprint` 是否与当前 `<packageRoot>` 一致，并要求 manifest 中存在当前 profile 的 GUI 和 CLI。源码包发生变化后，重新执行 bootstrap。

## 解析 `<yoki>`

优先使用 bootstrap 输出中的 `CLI:`。如果输出已经丢失，可以读取 `current.json` 和对应指纹目录下的 `tool-manifest.json`：

```powershell
$cacheRoot = Join-Path $projectRoot ".yokiframe\runtime\com.hinatayoki.yokiframe"
$current = Get-Content (Join-Path $cacheRoot "current.json") -Raw | ConvertFrom-Json
$fingerprintRoot = Join-Path $cacheRoot $current.sourceFingerprint
$manifest = Get-Content (Join-Path $fingerprintRoot "tool-manifest.json") -Raw | ConvertFrom-Json

$platform = $manifest.platforms | Where-Object { $_.runtimeIdentifier -eq "win-x64-aot" } | Select-Object -First 1
$yoki = Join-Path $fingerprintRoot $platform.cliEntry.Replace('/', '\')
if (-not (Test-Path -LiteralPath $yoki)) {
    throw "YokiFrame CLI was not generated: $yoki"
}
```

上面的解析示例假设 Windows。更可靠的做法是按当前操作系统选择 profile：Windows `win-x64-aot`，Linux `linux-x64`，macOS Intel `osx-x64`，macOS Apple Silicon `osx-arm64`，再从 `platforms[].cliEntry` 组合绝对路径。

## 目标项目前置检查

### Unity

目标目录必须包含：

- `Assets/`
- `Packages/manifest.json`
- `ProjectSettings/ProjectVersion.txt`

`ProjectVersion.txt` 中的 Unity 版本必须是 `2022.3` 或更高。Unity Git URL 与 local embedded package 不能同时生效。

### Godot .NET

目标目录必须包含：

- `project.godot`
- 顶层 C# `.csproj`

项目必须使用 `Godot.NET.Sdk/4.7+`，目标框架必须是 `net8.0+`。Godot 安装是完整的 `addons/yokiframe` 投影，不是把源码目录复制进去。

## 执行安装事务

以下命令使用 PowerShell；Linux/macOS 去掉命令前的 `&`，并按本机路径修改分隔符。每种模式都必须先 plan，再 apply。

### Godot local

```powershell
& $yoki installer plan `
  --mode godot-local `
  --source $packageRoot `
  --target $projectRoot `
  --repair-godot true `
  --enable-godot true

& $yoki installer apply `
  --mode godot-local `
  --source $packageRoot `
  --target $projectRoot `
  --repair-godot true `
  --enable-godot true
```

### Unity local

```powershell
& $yoki installer plan `
  --mode unity-local `
  --source $packageRoot `
  --target $projectRoot

& $yoki installer apply `
  --mode unity-local `
  --source $packageRoot `
  --target $projectRoot
```

### Unity Git URL

```powershell
$gitUrl = "https://github.com/HinataYoki/YokiFrame.git"
& $yoki installer plan --mode unity-git --target $projectRoot --git-url $gitUrl
& $yoki installer apply --mode unity-git --target $projectRoot --git-url $gitUrl
```

需要固定版本时，在 Git URL 后追加 `#<tag-or-commit>`。如果没有本地源码包并且目标项目也没有可用 `<yoki>`，不能执行上述 CLI；转回 README 的 Unity Package Manager 安装方式，或先取得完整源码包并完成 bootstrap。

## 结果判定

### plan

`installer plan` 必须满足：

- 进程退出码为 `0`。
- JSON 的 `session.status` 为 `PlanReady`。
- 检查 `session.plan.actions`、`session.plan.warnings`、`session.plan.targetProjectRoot` 和 `session.plan.packageTarget`。
- `session.conflicts` 非空时停止，不直接 apply。

### apply

`installer apply` 必须满足：

- 进程退出码为 `0`。
- JSON 的 `session.status` 为 `Succeeded`。
- 检查 `session.result.targetPath`、`session.result.changed` 和 `session.evidence`。

失败时读取 `error.code`、`error.message`；如果返回 `session`，继续读取 `session.conflicts`、`session.logs`、`session.evidence` 和 `rollbackSucceeded`。`InstallerConflict` 表示需要先处理冲突，不能靠重复 apply 解决。

最后执行目标文件校验：

- Unity：确认 `Packages/manifest.json` 或 embedded package 已指向 YokiFrame，且包目录存在。
- Godot：确认 `addons/yokiframe/plugin.cfg`、插件 bootstrap 和 `project.godot` 安装项存在；重新打开项目后确认插件可加载。

## 覆盖与安全边界

- legacy 受管内容只有在用户明确同意接管时，才可追加 `--take-over true` 并重新 plan。
- 当前受管包中的用户修改不能用 `--take-over` 绕过；先报告冲突和 evidence。
- 不直接编辑 `Packages/manifest.json`、`project.godot`、`addons/yokiframe` 或 `.yokiframe` 来绕过 Installer 事务。
- 没有终端、文件写入或必要编译环境时，只报告阻塞条件和可复制命令，不声称已经安装。
