# 配表怎么做

配表由项目里配置的 Luban 完成。Workbench 的 TableKit 页面负责保存并校验 Luban 路径；具体的改表、填表和排错按下面的官方 Skill 执行。

## 什么时候走这里

- 加表、改表、设计 schema、填写 Excel
- Luban 生成失败、校验器、运行时加载
- 查询表结构、校验数据、列出表

## 配置在哪里

1. 找到游戏项目根：Unity 项目含 `ProjectSettings`，Godot 项目含 `project.godot`。
2. Unity 读取 `ProjectSettings/Packages/com.hinatayoki.yokiframe/tablekit-settings.json`。Godot 读取 `project.godot` 的 `[yokiframe/editor]` 中 `tablekit/document`。
3. 使用其中的 `LubanConfigPath`、`LubanWorkDir`、`LubanExecutablePath`、`LubanSkillsPath`、`LubanAgentExecutablePath`、`LubanMcpExecutablePath`。
4. 相对路径按项目根解析。`LubanSkillsPath` 可以是 `Luban.Skill` 根目录，也可以是其中的 `skills` 子目录，以该目录下存在 `*/SKILL.md` 为准。

## 按任务打开官方 Skill

直接读取对应 `SKILL.md` 并执行：

| 要做的事 | 官方 Skill |
|---|---|
| 加一张表 | `luban-add-table` |
| 填写 Excel | `luban-excel-fill` |
| 设计 schema、bean 或多态 | `luban-schema-design` |
| 写校验器 | `luban-validator` |
| 排查生成失败 | `luban-generate-debug` |
| 接入运行时加载 | `luban-runtime-load` |

生成代码和数据使用 Workbench 已配置的主 `Luban.dll`、target 和输出目录。`Luban.Agent.dll` 存在时，用官方 Agent 的 `validate`、`schema`、`list-tables`、`describe` 做结构化查询。`Luban.Mcp.dll` 存在时，对话式工具使用官方 MCP。

官方 Skill 或这些可选工具不在当前 Luban 版本里时，用 TableKit 页面完成校验和生成。路径已填写但文件不存在时，把缺失项告诉用户，并回到 TableKit 页面重新选择。
