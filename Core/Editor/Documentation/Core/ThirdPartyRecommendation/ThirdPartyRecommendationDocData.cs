#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 第三方库推荐文档 — 推荐比官方方案更好用的第三方工具。
    /// 与「第三方库索引」不同，本模块聚焦实践验证过的优质替代方案，不限于框架依赖。
    /// </summary>
    internal sealed class ThirdPartyRecommendationDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "第三方库推荐",
                Icon = KitIcons.GITHUB,
                Category = "REFERENCE",
                Description = "与 YokiFrame 配合使用的优秀第三方工具推荐。聚焦经过实践验证、比官方方案更好用的工具选择。",
                Keywords = new List<string> { "推荐", "第三方", "AI协作", "AIBridge", "Unity MCP替代", "效率" },
                Sections = new List<DocSection>
                {
                    CreateAIBridgeSection(),
                }
            };
        }

        /// <summary>
        /// AIBridge — 比 Unity MCP 更好用的 Unity AI 协作方案。
        /// 类型：强烈推荐
        /// </summary>
        private static DocSection CreateAIBridgeSection()
        {
            return new DocSection
            {
                Title = "AIBridge — 比 Unity MCP 更好用的 AI 协作方案",
                Description = "liyingsong99 开源的 Unity AI 自动化桥梁工具，为 AI 编码助手（Claude Code、Cursor、Codex 等）提供稳定的命令行接口。\n\n"
                    + "与 Unity 官方 MCP 方案不同，AIBridge 采用文件 I/O 模型替代实时 WebSocket 连接，\n"
                    + "从根本上解决了 Domain Reload 导致的断连问题，是 Unity AI 协作的更优选择。\n\n"
                    + "核心能力：\n"
                    + "• 基于文件 I/O 的命令/结果模型 — 天然兼容 Unity 编译域重载（Domain Reload）\n"
                    + "• 资源搜索与操作 — 搜索 Asset、查找引用、管理资源\n"
                    + "• Prefab 编辑 — 创建/修改预制件、组件操作、属性写入\n"
                    + "• 场景编辑 — 通过 Unity API 检查场景、编辑对象层级\n"
                    + "• 项目编译 — 触发 Unity 编译并获取结果日志\n"
                    + "• 日志读取 — 读取控制台日志（Error/Warning/Info）\n"
                    + "• 截图/GIF 捕获 — 视觉验证与自动化截图\n"
                    + "• 测试运行 — 执行 Unity Test Runner 测试\n"
                    + "• 批量命令 — 多命令批处理，减少进程开销\n\n"
                    + "适用场景：\n"
                    + "• 使用 Claude Code / Cursor / Codex 等 AI 助手开发 Unity 项目\n"
                    + "• CI/CD 流水线中的 Unity 自动化构建与测试\n"
                    + "• 批量资源处理脚本（无需手写 Editor 工具）\n"
                    + "• 自动化视觉回归测试（截图对比）",
                Links = new List<PluginLink>
                {
                    new() { Name = "GitHub", Url = "https://github.com/liyingsong99/AIBridge" },
                },
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "安装方式（UPM Git URL）",
                        Code = "// 1. Unity: Window → Package Manager → + → Add package from git URL\nhttps://github.com/liyingsong99/AIBridge.git\n\n// 2. 安装 .NET 8.0 运行时（CLI 依赖）\n// 下载地址: https://dotnet.microsoft.com/download/dotnet/8.0\n\n// 3. 启动 Unity 编辑器，AIBridge 自动监听",
                        Explanation = "需要同时安装 Unity Package（编辑器端）和 .NET 8.0 运行时（CLI 端）。Unity 编辑器需保持运行。"
                    },
                    new()
                    {
                        Title = "CLI 常用命令速查",
                        Code = @"// CLI 路径: .aibridge/cli/AIBridgeCLI.exe（或使用 alias $CLI）

// 编译项目
$CLI compile unity

// 获取编译 Error
$CLI get_logs --logType Error

// 搜索 UI 相关资源
$CLI asset search --query ""UI"" --format paths

// 写入 Editor 日志
$CLI editor log --message ""处理完成"" --logType Warning

// 检查 Harness 连接状态
$CLI harness status

// 运行单个 host 命令
$CLI exec run --stdin

// 批量运行多个 host 命令
$CLI exec batch --stdin",
                        Explanation = "CLI 通过文件轮询与 Unity 编辑器通信，所有命令结果写入 .aibridge/ 目录，可追溯。"
                    },
                    new()
                    {
                        Title = "AIBridge vs Unity MCP — 对比表格",
                        Code = @"// ╔══════════════╦══════════════════════╦══════════════════════╗
// ║ 维度          ║ AIBridge              ║ Unity MCP            ║
// ╠══════════════╬══════════════════════╬══════════════════════╣
// ║ 连接模型      ║ 文件 I/O：命令入/结果出║ WebSocket 实时长连接   ║
// ║ 编译容错      ║ 跨 Domain Reload 恢复  ║ 编译后连接必然断开     ║
// ║ 部署复杂度    ║ 单个 CLI 可执行文件    ║ 需 MCP Server + 配置   ║
// ║ 可追溯性      ║ 命令/结果/日志/截图    ║ 依赖会话状态不易回溯   ║
// ║ 离线/异步     ║ 天然支持命令排队       ║ 连接即发，不支持排队     ║
// ║ Unity 版本    ║ 2019.4+               ║ 6000.3+ (Unity 6)    ║
// ║ .NET 要求     ║ .NET 8 运行时         ║ .NET 10 运行时       ║
// ╚══════════════╩══════════════════════╩══════════════════════╝",
                        Explanation = "AIBridge 在编译容错、部署复杂度、版本兼容性和可追溯性四个维度全面优于 Unity MCP。"
                    },
                    new()
                    {
                        Title = "为什么 AIBridge 比 Unity MCP 更好用",
                        Code = @"// ===== 1. 编译域重载是 Unity 开发的常态 =====
// Unity 开发中，每次修改 C# 脚本并切回编辑器，就会触发 Domain Reload。
// Unity MCP 基于 WebSocket 长连接，Domain Reload 会导致连接断开，
// AI 助手需要重新建立连接、重新获取上下文，整个工作流被打断。
//
// AIBridge 使用文件轮询模型，命令写入 .aibridge/ 目录后 CLI 等待结果文件，
// Unity 重载完成后继续读取并执行，AI 助手完全无感。

// ===== 2. 部署一条命令，不折腾 =====
// Unity MCP: 安装 Unity Package → 配置 MCP Server 启动参数 →
//           配置 AI 工具的 MCP 连接 → 调试连接 → 处理断连重试
// AIBridge: 安装 Unity Package → 下载 CLI 可执行文件 → 完成
//
// AIBridge CLI 是一个自包含的 .NET 单文件可执行程序，无依赖、免配置。

// ===== 3. 每条操作都有据可查 =====
// Unity MCP 的操作日志依赖 AI 助手的对话记录，会话关闭后难以回溯。
// AIBridge 的每次命令、每条结果、每张截图都保存在 .aibridge/ 目录下，
// 形成天然的审计追踪。CI 构建失败？翻一下命令结果文件就定位。

// ===== 4. 支持离线排队执行 =====
// Unity MCP 要求 Unity 编辑器保持运行且连接活跃。
// AIBridge 支持在没有 Unity 实例运行时预先写入命令文件，
// 待 Unity 启动后自动执行，适合 CI 流水线和异步批量任务。

// ===== 5. 版本兼容更友好 =====
// Unity MCP 要求 Unity 6 (6000.3+) + .NET 10，对新项目友好但存量项目尴尬。
// AIBridge 最低支持 Unity 2019.4 + .NET 8，覆盖绝大多数现役项目。

// ===== 总结 =====
// 如果你的 AI 辅助工作流中频繁修改 C# 脚本（这是常态），
// AIBridge 的文件模型让你无需在每次编译后手动重连 AI。
// 这就是「比 Unity MCP 更好用」的本质——不是功能更多，
// 而是在 Unity 特有的技术约束下，选择了更稳定的架构。",
                        Explanation = "五个维度的详细对比：编译容错是 AIBridge 最核心的优势，也是 Unity MCP 最薄弱的环节。"
                    }
                }
            };
        }
    }
}
#endif
