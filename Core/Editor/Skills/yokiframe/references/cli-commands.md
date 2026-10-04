# 用 yoki 做什么

`yoki` 位于当前项目 `.yokiframe/runtime/`。从 `current.json` 和 `tool-manifest.json` 得到可执行文件，下面记为 `$YOKI`。`--project` 传 Unity 或 Godot 项目根。

在线 engine 只有一个时可以省略 `--engine`。有多个时写明 engine。Godot 编辑器是 `godot-editor`，Godot 游戏运行态是 `godot-runtime`，两者可以同时在线。读取和诊断面向编辑器或 Tools 里的宿主。

## 怎么读结果

命令返回 compact JSON。

- 先看 `ok`、`state`、`issues`、`nextActions`。
- `state` 表示这次有没有读到数据：`Ready` 和 `Degraded` 时 `ok=true`。其余为 `Unavailable`、`Stale`、`Failed`、`Unknown`。
- Kit 是否在工作，看 `summary` 里该 Kit 自己的字段。
- `Stale` 时按 `nextActions` 刷新。`Unknown` 表示超时或取消，写操作重新确认后再执行。
- 默认返回摘要。要原始协议字段时加 `--detail full`。
- 选项名、必填项和数值范围以命令 schema 为准，错误会写在 JSON 里。

## 要查什么就用哪条命令

日常查看一个 Kit 用 `kit status`。它先读 telemetry，读不到再读 snapshot。

| 要做的事 | 命令 |
|---|---|
| 看静态能力 | `harness status` |
| 看项目模型、能力、心跳和在线 action | `harness catalog`。在线 action 加 `--refresh-commands` |
| 看项目模型 | `project status` |
| 重建项目模型 | `project refresh`。`--package` 可选，只在需要指定包根时使用。先加 `--dry-run` 查看 `writes[]` |
| 列出 engine | `engine list` |
| 看某个 Kit | `kit status` |
| 直接读共享内存 | `telemetry read` |
| 直接读 snapshot 文件 | `snapshot read` |
| 看命令桥 | `bridge status` |
| 看诊断摘要 | `doctor` |
| 看 FastChannel 端点 | `fastchannel status`。这里给出的是登记的端点 |
| 执行已声明 action | `command send`。action 来自 `harness catalog --refresh-commands` 的 `commandCatalog`，payload 与 Workbench 上同一次操作一致 |
| 查某次命令结果 | `command status`，带 `--request-id` |
| 看空间索引的数量、列表、密度和分析 | `spatialkit stats`、`spatialkit indexes`、`spatialkit density`、`spatialkit analyze`。`spatialkit indexes` 对应 Runtime action `list_indexes` |
| 预览音频 ID | `audio index scan` |
| 生成音频 ID 代码和 manifest | `audio index generate`。先 scan，确认目录、类名和已有 ID |
| 搜索或检查本地化 | `localization search`、`localization check` |
| 预览 Luban 本地化 JSON | `localization preview`，输出在 `Temp/LubanPreview/LocalizationKit` |
| 增加一条本地化 | `localization add`。先 `--dry-run`。覆盖已有文本时加 `--force` |
| 生成本地化 XML 和 Excel 模板 | `localization template generate`。先 `--dry-run`。覆盖已有文件时加 `--force` |
| 预览安装 | `installer plan` |
| 执行安装 | `installer apply`。参数与已查看的 plan 相同，用户确认后执行 |
| 导出 Godot 玩家包 | `player build`。`--configuration` 用 `debug` 或 `release`，`--output` 放在项目根内。先 `--dry-run`。Unity 玩家包使用 Unity 的构建流程 |

`--dry-run` 返回将要写入的 `writes[]`，失败原因与正式执行相同。`installer apply` 的预览命令是 `installer plan`，`audio index generate` 的预览命令是 `audio index scan`。

Runtime 缓存由 Workbench 或包根 `Documentation~/Guides/AI-Install.md` 的 bootstrap 准备。安装参数见 [installer.md](installer.md)。
