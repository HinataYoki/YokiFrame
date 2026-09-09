# TableKit / Luban AI 路由

YokiFrame 不重复实现 Luban AI。Workbench 只保存并校验官方路径；对话中的配表需求由本 Skill 自动导入官方 Skill。

## 触发

用户提出以下任一需求时进入本路由，不要先让用户复制提示词或 MCP 配置：

- 加表、改表、设计 schema、Excel 填表
- Luban 生成失败、校验器、运行时加载
- 查询表结构、校验数据、列出表

## 读取配置

1. 定位当前游戏项目根（含 `ProjectSettings` 的 Unity 项目或含 `project.godot` 的 Godot 项目）
2. 读取 `ProjectSettings/Packages/com.hinatayoki.yokiframe/tablekit-settings.json`
3. 使用其中的：
   - `LubanConfigPath` / `LubanWorkDir` / `LubanExecutablePath`
   - `LubanSkillsPath`
   - `LubanAgentExecutablePath`
   - `LubanMcpExecutablePath`
4. 相对路径相对项目根解析。`LubanSkillsPath` 可以是 `Luban.Skill` 根目录或其中的 `skills` 子目录；以该目录下存在 `*/SKILL.md` 为准

## 导入官方 Skill

官方 Skill 存在时，按任务直接读取对应 `SKILL.md` 并执行，不要改写或复制到 YokiFrame 包内：

| 需求 | 官方 Skill |
|---|---|
| 加一张表 | `luban-add-table` |
| Excel 填表 | `luban-excel-fill` |
| schema / bean / 多态 | `luban-schema-design` |
| 校验器 | `luban-validator` |
| 生成失败排查 | `luban-generate-debug` |
| 运行时加载 | `luban-runtime-load` |

生成代码和数据仍使用 Workbench 已配置的主 `Luban.dll`、target 和输出目录。需要结构化查询或校验且 `Luban.Agent.dll` 存在时，调用官方 Agent：`validate`、`schema`、`list-tables`、`describe`。对话式工具且 `Luban.Mcp.dll` 存在时再使用官方 MCP。

## 缺失时

- 旧版 Luban 没有 Agent/MCP/Skill：继续用 TableKit 验证/生成，不假装官方 AI 可用
- 路径已填但文件不存在：报告具体缺失项，提示回 Workbench TableKit 页面重新选择
- 不要为了补齐能力在 Workbench 里再做一套 Agent UI 或复制提示词入口
