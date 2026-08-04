# 常见问题

遇到问题时，先确认宿主、项目路径和当前 engine，再按下面的现象排查。不要直接编辑 `.yokiframe` 下的缓存或协议文件。

## 安装与启动

| 现象 | 处理 |
| --- | --- |
| Unity 无法打开 Workbench | 确认本机安装 .NET 10 SDK；Windows Native AOT 还需要 Visual Studio C++ Build Tools，然后重试 `Ctrl+E`。 |
| Godot 提示 Runtime 缺失或版本不匹配 | 图形 Installer 会先自动构建并重新生成 plan；构建期间右侧显示“正在为 Godot 构建 Runtime”和不确定进度，这是准备阶段，不代表安装事务失败。若构建失败，检查 .NET 10/C++ 工具链后重新点击“构建 Runtime”，或从当前源码包重新运行对应平台的 `install-godot` 脚本。 |
| Godot 提示无法加载 `YokiFrameGodotEditorPlugin.cs` 并自动禁用插件 | 先关闭正在运行的 Godot，再用当前源码包重新执行 Installer 的 Godot apply；安装器会替换受控 add-on、维护主 `.csproj`，并使用 `-p:GodotTarget=Editor` 自动执行目标项目 `dotnet restore`/`dotnet build`，确保 `TOOLS` 编辑器程序集真的编译，同时兼容 Godot 4.7 的 `.godot/mono/temp/bin/Debug` 输出布局。构建完成后还会重新读取并登记 `res://addons/yokiframe/plugin.cfg`，抵御 Godot 扫描失败时自动移除启用项。若构建失败，按 Installer 返回的编译输出修复项目代码或 SDK 后重试；不要把插件脚本单独复制到项目。 |
| Godot 项目没有 YokiFrame 菜单 | 确认使用的是 Godot .NET 版本，并且已由 Installer 安装 `addons/yokiframe`；不要手动复制源码目录。 |
| Installer 选中 Godot 项目后仍提示缺少主项目文件 | 确认项目使用 Godot .NET，并在 `project.godot` 中存在 `[dotnet]` section 或 `.godot/mono`；空项目会在 apply 事务中自动生成主 `.csproj`。若没有这些 .NET 证据，则该项目是普通 Godot 项目，当前不受支持。 |
| Unity 项目同时配置了 Git URL 和 local package | 只保留一种来源；两种来源不能同时生效。 |
| `.yokiframe` 文件数量持续增长 | 确认宿主或 Workbench 已启动；自动清理只处理 archive、deadletter、results 和启动日志，并按 TTL/数量上限删除已完成旧文件。pending、processing、snapshot、heartbeat 和当前 Runtime 会保留。 |

## Workbench 页面

1. 在“框架”页确认项目根目录和目标 engine。
2. 查看 Doctor 摘要，先修复编译、连接或缓存错误。
3. 页面显示 `offline`、`stale` 或 `truncated` 时，先恢复宿主连接并重新读取；不要把旧 snapshot 当成当前状态。
4. 某个 Kit 没有独立页面时，先回到包根 README 的能力表；没有独立页面不代表 Runtime API 不可用。

## CLI

CLI 报错时保留错误建议和 evidence path，并先运行：

```powershell
$YOKI = "<Workbench 框架页显示的 yoki 路径>"
& $YOKI project status --strict --project <projectRoot>
& $YOKI engine list --project <projectRoot>
& $YOKI doctor --engine <engineId> --project <projectRoot>
```

CLI 的未知选项、非法数值或缺失必填项会在进入命令前直接返回结构化 JSON；不要依赖错误输入静默回落默认值。长时间命令可按 Ctrl+C 取消，退出码为 `130`，结果中的 `error.code` 为 `Cancelled`。

读取运行态时优先使用 `telemetry read`，不可用时再使用 `snapshot read`。多个 engine 在线时必须显式传入 `--engine`。不要直接修改工具缓存或 `.yokiframe` 文件。

## Kit 特定问题

| 现象 | 处理 |
| --- | --- |
| TableKit 生成失败 | 先确认 Luban 路径、`luban.conf`、code/data target 和输出目录；生成前执行验证。 |
| 本地化文本显示缺失标记 | 确认已调用 `LocalizationKit.SetProvider`，目标语言已加载，且文本 ID 存在。 |
| ActionKit 不推进 | 确认宿主只接入一个 scheduler，并在销毁对象时取消 controller。 |
| FsmKit 不更新 | 确认 Unity/Godot 生命周期持续调用对应的更新入口，并且 FSM 已 `Start`。 |
| UIKit Root 设置无效 | 在第一次 UIKit 变更前注册 Prefab Variant；Root 创建后不能替换。 |
| ResKit 返回空资源 | 确认 Provider 已安装、路径是宿主定义的 location，并检查资源是否真的存在。 |

更多限制以对应 Kit 的 API 页面为准。
