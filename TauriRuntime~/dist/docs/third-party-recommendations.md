# 第三方库推荐

## 推荐顺序

| 优先级 | 工具 | 什么时候装 |
|---|---|---|
| 必装于本仓库开发 | AIBridge | 需要 AI 执行 Unity 编译、日志、资源查询和验证。 |
| 推荐 | UniTask | Unity 项目有较多异步加载、UI、场景、取消流程。 |
| 推荐 | YooAsset | 项目需要 AssetBundle、RawFile、热更新或生产级资源管理。 |
| 推荐 | ZString | 高频日志、快照、诊断字符串构建较多。 |
| 按需 | DOTween | 需要 UI 动画、流程动画或补间演出。 |
| 按需 | FMOD | 需要事件音频、动态音乐、复杂混音。 |
| 按需 | Unity Input System | 需要 Unity 项目输入、重绑定、多设备、手柄或触屏。 |
| 按需 | Nino | 存档数据大、读写频繁、需要二进制序列化。 |
| TableKit 必需 | Luban | 使用 TableKit 配置表生成。 |

## AIBridge

AIBridge 是本仓库 Unity 自动化入口。Codex 修改 C# 后，优先用它验证。

```powershell
./.aibridge/cli/AIBridgeCLI.exe compile unity
./.aibridge/cli/AIBridgeCLI.exe get_logs --logType Error
./.aibridge/cli/AIBridgeCLI.exe asset search --query "UI" --format paths
./.aibridge/cli/AIBridgeCLI.exe harness status
```

| 场景 | 命令 |
|---|---|
| 编译验证 | `compile unity` |
| 看 Console Error | `get_logs --logType Error` |
| 找已导入资源 | `asset search/find --format paths` |
| 看工具能力 | `harness status` |

## Unity 项目增强库

| 库 | 直接收益 | 不装时 |
|---|---|---|
| UniTask | async API 返回 UniTask，取消流程更适合 Unity 生命周期。 | 回退 Task、回调或同步路径。 |
| YooAsset | ResKit raw、asset、scene 统一切到 YooAsset Provider。 | 使用 Unity Resources 或项目自定义 Provider。 |
| ZString | 减少热路径字符串分配。 | 使用 `StringBuilder` 或普通字符串实现。 |
| DOTween | UIKit / ActionKit 可接入补间动画。 | 使用内置动画或普通 Action。 |
| FMOD | AudioKit 可接 FMOD 事件路径。 | 使用 Unity AudioSource 或项目音频后端。 |
| Input System | 项目 gameplay 输入直接使用 Unity Input System；UIKit 可按需开启 Unity 侧导航集成。 | 使用 Unity legacy 输入或项目自定义输入层。 |
| Nino | SaveKit 可接高性能二进制序列化。 | 使用内置或项目自定义序列化。 |
| Luban | TableKit 可生成配置表代码和数据。 | TableKit 只能做环境提示和配置编辑。 |

## 分层规则

- Base/Core 不直接依赖这些库。
- Unity 专属包放 Unity Adapter 或项目层。
- Tool Kit 对外保持统一 API，通过 Provider / Backend 接入第三方实现。
