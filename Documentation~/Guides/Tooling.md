# Workbench、CLI 与 Installer

> 面向读者：需要查看 YokiFrame 项目状态、使用离线工具或安装框架的开发者
>
> 主要入口：Avalonia Workbench、`yoki` CLI、Installer
>
> 运行边界：.NET 10 工具链，不进入 Unity Player 或 Godot 导出包
>
> 状态来源：`YokiFrameWorkbench~/src/YokiFrame.Cli/Program.cs`、Workbench 页面目录与项目 `.yokiframe/runtime/com.hinatayoki.yokiframe/current.json`

## 适用场景

Workbench 适合交互式查看 Kit、文档、项目诊断和受控工具操作；`yoki` 适合脚本化查询与明确的受控操作；Installer 适合 Unity embedded、Unity Git URL 和 Godot local 的安装、更新与回滚。

三者共享 Client 和 Application 层。它们不替代游戏业务 Runtime API，也不提供 Unity Scene、Prefab、Asset、Play Mode、截图或输入自动化。

## Workbench

在 Unity 中通过 `YokiFrame/Workbench/Open` 或 `Ctrl+E` 启动；在 Godot 中通过 `Project > Tools > YokiFrame > Open Workbench` 或 `Ctrl+E` 启动。若 Godot 的系统快捷键冲突，使用 `Ctrl+Alt+E`。

Git URL 与源码包不携带任何 Workbench、Installer 或 CLI 二进制。Unity 的 `Ctrl+E` 会先激活同一项目中已有的 Workbench，不为该次激活检查或构建 Runtime。只有没有可激活实例，且项目缓存缺失或 `current.json` 指向的 Runtime 不可用时，才会在项目 `.yokiframe/runtime/com.hinatayoki.yokiframe/<sourceFingerprint>/` 构建当前平台 Runtime；Windows 固定生成 `win-x64-aot`。Workbench 打开后会在后台计算源码指纹；发现新版时，页头显示“重新编译新版”按钮，只有用户点击后才启动构建。构建成功后写入新的 `current.json` 并清理旧 fingerprint 目录；仍被旧 Workbench 进程占用的目录会保留到后续启动再清理。窗口关闭会取消后台检查或构建，避免任务继续访问已销毁的 UI 或进程。

Godot 用户需要先从源码包显式运行安装入口。它会构建项目 `.yokiframe` Runtime 缓存，并直接打开与当前源码版本匹配的新 Installer；不需要手动查找缓存中的 GUI 或 CLI。Windows 示例：

```powershell
& "<packageRoot>\YokiFrameWorkbench~\scripts\runtime-bootstrap\install-godot.cmd" --project "<godotProjectRoot>"
```

Linux 使用 `install-godot.sh --project <godotProjectRoot>`，macOS 使用 `install-godot.command --project <godotProjectRoot>`。脚本要求本机已有 .NET 10 SDK；缓存可删除后重新生成，绝不写回只读的 Unity package 或 Godot 源码包。若已打开的 Installer 提示 Runtime 缓存与源码包不匹配，点击“构建 Runtime”即可执行同一显式构建流程并打开新的 Installer。

先从“框架”页确认项目、engine、heartbeat 和 Doctor 摘要。多个 engine 同时在线时选择明确目标；Godot 编辑器状态通常使用 `godot-editor`，游戏运行态使用 `godot-runtime`。Workbench 只显示已经具备真实数据链路和 Application read model 的页面，不显示未迁移 Kit 的占位页。

常见页面包括框架、文档、EventKit、FsmKit、LogKit、PoolKit、ResKit、ActionKit、AudioKit、SpatialKit、UIKit、TableKit、LocalizationKit 和 SaveKit。Architecture 没有独立页面；它的 Runtime API 与只读诊断仍可通过 API 文档和 CLI 使用。

AudioKit 页面只按 Bus 观察当前播放、播放进度与播放历史，不会停止音频、修改音量/静音或清空 Runtime 历史。稳定索引扫描和生成只写入项目 C# 映射与 manifest，不影响当前运行中的音频。

文档页的紧凑标题栏支持按关键词检索包内文档的标题、路径和正文；结果会在左侧显示命中摘要，打开后正文高亮关键词并定位到首个命中位置。它不读取或修改项目文件。

## `yoki` CLI

项目缓存根的 `current.json` 决定当前源码指纹；该指纹目录内 `tool-manifest.json` 决定当前平台入口。不要硬编码指纹或包内路径。当前 Windows 可按以下方式解析 `yoki`：

```powershell
$projectRoot = "<projectRoot>"
$cacheRoot = Join-Path $projectRoot ".yokiframe/runtime/com.hinatayoki.yokiframe"
$fingerprint = (Get-Content (Join-Path $cacheRoot "current.json") -Raw | ConvertFrom-Json).sourceFingerprint
$runtimeRoot = Join-Path $cacheRoot $fingerprint
$manifest = Get-Content (Join-Path $runtimeRoot "tool-manifest.json") -Raw | ConvertFrom-Json
$entry = $manifest.platforms | Where-Object platform -eq "win-x64-aot" | Select-Object -First 1
$YOKI = Join-Path $runtimeRoot $entry.cliEntry
& $YOKI project status --strict --project $projectRoot
```

先查看 Project Model、capability catalog 和 engine，再读取 telemetry；telemetry 不可用时读取 snapshot。`yoki` 没有稳定的 `--help` 契约，命令面以当前发布程序、CLI 源码和错误建议为准。

| 目标 | 命令 |
|---|---|
| Project Model | `project status`、`project refresh` |
| 能力和 engine | `harness status`、`harness catalog`、`engine list` |
| 运行态读取 | `telemetry read`、`snapshot read`、`bridge status`、`doctor`、`fastchannel status` |
| 已声明 Runtime action | `command send` |
| SpatialKit 查询 | `spatialkit stats`、`spatialkit indexes`、`spatialkit density`、`spatialkit analyze` |
| AudioKit 索引 | `audio index scan`、`audio index generate` |
| LocalizationKit | `localization search`、`localization check`、`localization add`、`localization template generate`、`localization preview` |
| 安装事务 | `installer plan`、`installer apply` |
| Godot Player | `player build --engine godot` |

查询命令不修改作者资产。`localization preview` 会重建项目 `Temp/LubanPreview/LocalizationKit` 下的临时 JSON；`project refresh`、音频索引生成、本地化补充/模板生成、Godot Player 导出和非只读 `command send` 会写入项目或当前宿主，应在执行前确认目标、输入和预期结果。Godot 导出要求显式提供 .NET 可执行文件、preset、debug/release 和项目内输出路径；YokiFrame CLI 当前不提供 Unity Player 构建，请使用 Unity Editor 或自行选择外部自动化工具。

## Installer

| 模式 | 目标 | 关键约束 |
|---|---|---|
| Unity local | `Packages/com.hinatayoki.yokiframe` | 与 Unity Git URL 来源互斥 |
| Unity Git URL | `Packages/manifest.json` | 只接受显式绝对 `file:`、`https:` 或 `git:` URI |
| Godot local | `addons/yokiframe` | 先验证项目 Runtime 缓存，再完整替换受管 add-on |

任何安装或更新都先生成 plan，再写 staging、校验、备份和原子替换；最后 post-verify，失败时 rollback。Unity embedded 更新会保持旧包可见直到 staging 完成，只在短暂目录切换后登记本地 file 依赖；依赖文本、`package.json`、asmdef/asmref、插件或编译器响应文件变化时才会刷新 manifest 让 Package Manager 重建程序集图，普通脚本更新不会触发全量依赖解析。检测到 Unity 受管包被用户修改时会停止；仅 `YokiFrameWorkbench~/.artifacts*` 例外，它是 `Ctrl+E` 的可再生构建缓存，更新时会被安全丢弃并按需重建。Godot 更新不做文件级 diff、合并或修改冲突阻断：Installer 会备份旧 `addons/yokiframe`，完整替换，再在任何失败时恢复备份。

YokiFrame 的脚本图标由包内 `.meta` 交付。Unity Git URL、local embedded 和域加载期间不会自动重写 MonoImporter，因此安装包不会因图标维护再次触发自身导入。只有维护 `Assets/YokiFrame` 源码树时，才可以从 `YokiFrame/Developer/Apply Kit Script Icons` 显式补齐图标。

```powershell
& $YOKI installer plan --mode unity-local --source <packageRoot> --target <unityProject>
& $YOKI installer apply --mode unity-local --source <packageRoot> --target <unityProject>

& $YOKI installer plan --mode godot-local --source <packageRoot> --target <godotProject>
& $YOKI installer apply --mode godot-local --source <packageRoot> --target <godotProject>
```

只有在明确确认安装或更新后才执行 `apply`。Godot `apply` 会删除旧 add-on 目录再替换其内容；它不能绕过当前发布包未提供的 legacy Kit API 扫描。

## 限制与相关资料

- Workbench 和 CLI 不直接读写协议 JSON；它们通过 Client 和 Application 层完成路径校验、原子写入和 terminal response 处理。
- 未完成的 Kit Interaction 不会因为有 Runtime API 或 Workbench 辅助页而自动产生 Runtime command。
- UIKit Workbench 仅面向 Unity；它不会远程 Open、Close、Show、Hide 或修改 Runtime UI。
- 进一步选择 Runtime API，参见 [新版入口总览](../Api/00-GettingStarted/Entrypoints.md)；安装架构概览参见 [架构](../Api/01-Architecture/Architecture.md)。
