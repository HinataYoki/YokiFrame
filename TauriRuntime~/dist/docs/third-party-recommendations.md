# 第三方库推荐

YokiFrame  的核心目标是把框架能力尽量沉到纯 C# 层，并通过 Unity / Godot Adapter 接入宿主。第三方库推荐因此分成两类：框架协作工具，以及在 Unity 适配层或具体 Kit 中经过验证的增强组件。


## AIBridge

AIBridge 是 Unity AI 自动化桥梁工具，为 Codex、Claude Code、Cursor 等 AI 编码助手提供稳定的命令行接口。

推荐理由：

| 维度 | AIBridge | Unity MCP |
|------|----------|-----------|
| 连接模型 | 文件 I/O：命令入、结果出 | WebSocket 长连接 |
| Domain Reload | 命令文件可恢复，编译后继续轮询 | 编译域重载后容易断线 |
| 部署复杂度 | Unity Package + CLI | MCP Server + 连接配置 |
| 证据追踪 | 命令、结果、日志、截图可落盘 | 更依赖会话状态 |
| 适配场景 | AI 开发、CI、批处理、视觉验证 | 实时连接型工具 |

常用命令：

```powershell
# Unity 编译
./.aibridge/cli/AIBridgeCLI.exe compile unity

# 获取 Error 日志
./.aibridge/cli/AIBridgeCLI.exe get_logs --logType Error

# 搜索已导入资源路径
./.aibridge/cli/AIBridgeCLI.exe asset search --query "UI" --format paths

# 查看当前能力快照
./.aibridge/cli/AIBridgeCLI.exe harness status
```

适用场景：

| 场景 | 建议 |
|------|------|
| AI 修改 Unity C# 后需要确认是否编译通过 | 优先 `compile unity` |
| 需要知道 Console 是否有 Error | `get_logs --logType Error` |
| 需要查找 Prefab、Scene、贴图、材质等已导入资源 | 优先 asset search/find |
| 需要保留命令证据、截图或批量结果 | 使用 AIBridge 的文件结果与 artifact |
