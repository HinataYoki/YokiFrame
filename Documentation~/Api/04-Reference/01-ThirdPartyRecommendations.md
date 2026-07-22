# 第三方库推荐与索引

## 选择建议

| 优先级 | 工具 | 什么时候装 |
|---|---|---|
| Unity 协作推荐 | AIBridge | 需要 AI 执行 Unity 编译、日志、资源查询和验证时安装；不是 YokiFrame 的运行依赖。 |
| 推荐 | UniTask | Unity 项目有较多异步加载、UI、场景、取消流程。 |
| 推荐 | YooAsset | 项目需要 AssetBundle、RawFile、热更新或生产级资源管理。 |
| 推荐 | ZString | 高频日志、快照、诊断字符串构建较多。 |
| 按需 | DOTween | 需要 UI 动画、流程动画或补间演出。 |
| 按需 | Unity Input System | 需要 Unity 项目输入、重绑定、多设备、手柄或触屏。 |
| 按需 | Nino | 存档数据大、读写频繁、需要二进制序列化。 |
| TableKit 必需 | Luban | 使用 TableKit 配置表生成。 |

## 能力索引

| 库 | 推荐级别 | 宏定义 | 影响范围 |
|---|---|---|---|
| UniTask | 推荐 | `YOKIFRAME_UNITASK_SUPPORT` | ResKit 等异步入口，以及 ActionKit 的 Unity 可选异步 Action Integration。 |
| YooAsset | 推荐 | `YOKIFRAME_YOOASSET_SUPPORT` | ResKit 的 Unity 可选 `[2.3.0,4.0.0)` asset/raw Provider。 |
| Luban | TableKit 必需 | `YOKIFRAME_LUBAN_SUPPORT` | TableKit 生成、验证和运行时代码。 |
| DOTween | 按需 | `YOKIFRAME_DOTWEEN_SUPPORT` | ActionKit 与 UIKit 的 Unity 可选补间 Integration。 |
| Unity Input System | 按需 | `YOKIFRAME_INPUTSYSTEM_SUPPORT` | UIKit 可选键盘/手柄导航 Integration。 |
| ZString | 推荐 | `YOKIFRAME_ZSTRING_SUPPORT` | 高频字符串构建优化。 |
| Nino | 按需 | `YOKIFRAME_NINO_SUPPORT` | SaveKit 序列化后端。 |

## 安装入口

| 库 | 入口 |
|---|---|
| UniTask | `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` |
| YooAsset | `https://github.com/tuyoogame/YooAsset.git` |
| Luban | `https://github.com/focus-creative-games/luban` |
| DOTween | `https://dotween.demigiant.com/download.php` |
| Unity Input System | Unity Package Manager: `com.unity.inputsystem` |
| ZString | `https://github.com/Cysharp/ZString/releases` |
| Nino | `https://github.com/JasonXuDeveloper/Nino.git` |

## AIBridge

AIBridge 是独立的 Unity 自动化插件。YokiFrame 不依赖、检测或调用 AIBridge 才能运行、安装、使用 CLI 或 Workbench。AI 需要 Unity 自动化时，应读取 AIBridge 自己注册的 Skill/Agent 规则；两个插件可以在同一 Unity 项目中协作，但不形成运行时依赖。AIBridge 的 CLI、Harness、Workflow 和验证命令以其自身文档为准，YokiFrame 不复制这些入口。可选协作工具需要发送 YokiFrame FileBridge 命令时，统一使用产品中立的 `external-automation` 审计来源；YokiFrame 不为具体插件保留专属 source。

## Unity 项目增强库

| 库 | 直接收益 | 不装时 |
|---|---|---|
| UniTask | async API 返回 UniTask；ActionKit 可通过独立 Integration 编排并向 token factory 传播取消。 | ActionKit 使用 Task、回调、原生 Delay/Condition 或同步路径。 |
| YooAsset | ResKit asset/raw 可显式接入项目已初始化的 YooAsset `[2.3.0,4.0.0)` `ResourcePackage`，也可通过 `YooAssetInitializer` 一键初始化并安装 Provider；V2/V3 均只公开 raw bytes/text。 | 使用 Unity Resources 或项目自定义 Provider。 |
| ZString | 减少热路径字符串分配。 | 使用 `StringBuilder` 或普通字符串实现。 |
| DOTween | ActionKit 与 UIKit 分别通过独立 Unity Integration 接入补间。 | UIKit 无 DOTween 时使用内置 Fade/Scale/Slide 动画。 |
| Input System | 项目 gameplay 输入直接使用 Unity Input System；UIKit 可选 Integration 只映射 UI Navigate/Submit/Cancel/Tab。 | 不安装时仍可使用 UIKit 焦点 API，由项目输入层显式调用。 |
| Nino | SaveKit 可接高性能二进制序列化。 | 使用内置或项目自定义序列化。 |
| Luban | TableKit 可生成配置表代码和数据。 | TableKit 只能做环境提示和配置编辑。 |

## 逐项说明

### UniTask

Unity 异步流程推荐安装。启用后，同名异步 API 可以从 `Task<T>` 切到 `UniTask<T>`；ActionKit 额外提供独立 `YokiFrame.ActionKit.UniTask`，通过 `ActionKitUniTask.From`、`.UniTask(...)` 与 `UniTask.ToAction()` 编排真实 UniTask。需要跟随 ActionKit 取消时必须使用接收 `CancellationToken` 的 factory；原生 Delay、帧等待和 Condition 不建立重复 UniTask 快捷门面。

### YooAsset

用于生产级资源管理。YooAsset `[2.3.0,4.0.0)` Integration 提供 `YooAssetInitializationOptions`、`YooAssetInitializer` 和 `YooAssetInitializationBehaviour`，可一键初始化 package 并接入 ResKit；项目也可以先自行初始化，再调用 `YooAssetInitializer.InstallProvider(package)`。初始化器不销毁 package，V2/V3 均只通过 ResKit 公开 raw bytes/text。Inspector 的 `EncryptionMode` 同时决定构建加密与运行时解密：Unity Editor 只显示扫描到成对实现的方案，V2 扫描 `IEncryptionServices` / `IDecryptionServices`，V3 扫描 `IBundleEncryptor` / `IBundleDecryptor`；内置提供 XOR 流式、文件偏移和 AES-CBC 实现。扫描不进入 Player，参数化服务仍由 YokiFrame 构建入口直接注入，不依赖 YooAsset 的无参类型反射列表。

### Luban

TableKit 必需。Workbench 通过 `dotnet Luban.dll` 执行验证和生成；主 Luban 代码写入用户项目 TableKit 代码根下的 `Luban/` 子目录，Workbench 直接把门面、加载契约和宿主程序集文件写入父目录。

### DOTween

ActionKit 与 UIKit 分别在自己的 `Integrations/Unity/DOTween/Runtime` 下提供独立可选程序集。安装后先运行 DOTween Setup；`DependencyDefineService` 检测到 DLL/程序集后维护 `YOKIFRAME_DOTWEEN_SUPPORT`，业务代码无需手工维护宏。

### Unity Input System

Unity 侧可选输入包。YokiFrame 不提供统一 gameplay 输入门面；`DependencyDefineService` 检测到 `com.unity.inputsystem` 后维护 `YOKIFRAME_INPUTSYSTEM_SUPPORT`，只启用 `YokiFrame.UIKit.InputSystem`。项目通过 `UIKitInputSystemNavigator` 绑定 Navigate、Submit、Cancel 和 Tab 的 `InputActionReference`。

### ZString

用于减少热路径字符串构建分配，适合日志、快照和诊断文本。

### Nino

SaveKit 可选二进制序列化后端，适合大存档和频繁读写。Unity 由 `DependencyDefineService` 自动检测 `com.jasonxudeveloper.nino`、`Nino.Core` 或 `Nino.Core.dll` 并维护 `YOKIFRAME_NINO_SUPPORT`；SaveKit Nino Integration 的源文件、asmdef 和测试均在该宏下编译。Nino payload 迁移由 Nino 自己负责，SaveKit 不执行 JSON 或通用 raw-byte 迁移。

## 接入边界

| 层 | 规则 |
|---|---|
| Core Runtime | 不直接引用 UnityEngine、DOTween、YooAsset、Nino 等宿主或第三方库。 |
| Engine Adapter | 只封装宿主 API、生命周期和组合，不承载可选第三方库实现。 |
| Integration | 宿主 API 映射进入 `Adapters/<Engine>`；第三方库接入进入 `Integrations`。Core Kit 放 `Core/Integrations/<Engine>/<Kit>/<Dependency>/Runtime`，Tool Kit 放自身 `Integrations`，并使用独立程序集边界。 |
| Workbench | 只消费 Application 强类型模型和通用 capability，不直接引用第三方 Runtime 类型。 |

## 相关资料

各库在 Runtime API 中的具体用法、资源所有权和宿主限制以对应 Kit 主页面为准。`Api/00-GettingStarted/Entrypoints.md` 用于选择 Runtime、Workbench、CLI 或 Installer 入口。
