# 第三方库索引

除明确说明外，这些库都不是 `YokiFrame/Core/Runtime` 纯核心目录的硬依赖；它们应作为 Unity Adapter、Tool Kit 或具体项目的可选增强。

## 总览

| 库 | 类型 | 宏定义 | 主要使用方 | 作用 |
|----|------|--------|------------|------|
| UniTask | 推荐安装 | `YOKIFRAME_UNITASK_SUPPORT` | ResKit、SceneKit、ActionKit、AudioKit、UIKit、InputKit、LocalizationKit | Unity 异步 API、取消令牌和低分配 async/await。 |
| YooAsset | 推荐安装 | `YOKIFRAME_YOOASSET_SUPPORT` | ResKit、SceneKit、UIKit | 资源加载、RawFile、AssetBundle 和热更新后端。 |
| Luban | TableKit 必需 | `YOKIFRAME_LUBAN_SUPPORT` | TableKit | 配置表解析、代码生成和数据导出。 |
| FMOD | 可选 | `YOKIFRAME_FMOD_SUPPORT` | AudioKit | 专业音频后端、事件路径、3D 音频和动态音乐。 |
| DOTween | 可选 | `YOKIFRAME_DOTWEEN_SUPPORT` | ActionKit、UIKit | 补间动画、面板打开关闭动画和演出流程。 |
| Unity Input System | 可选增强 | `YOKIFRAME_INPUTSYSTEM_SUPPORT` | InputKit | 输入重绑、设备抽象、手柄和触屏输入。 |
| ZString | 推荐安装 | `YOKIFRAME_ZSTRING_SUPPORT` | 全局性能优化 | 热路径字符串构建和诊断文本拼接。 |
| Nino | 可选增强 | `YOKIFRAME_NINO_SUPPORT` | SaveKit | 高性能二进制序列化。 |

## UniTask

Cysharp 开源的 Unity async/await 库。它适合替代 Coroutine 处理资源加载、场景切换、UI 异步、延迟、等待条件和取消流程。

在 YokiFrame 中的作用：

| 项 | 内容 |
|----|------|
| 推荐级别 | 推荐安装 |
| 宏定义 | `YOKIFRAME_UNITASK_SUPPORT` |
| 使用方 | ResKit、SceneKit、ActionKit、AudioKit、UIKit、InputKit、LocalizationKit |
| 缺省行为 | 未安装时保留同步、回调或 Task 路径；Unity 专属 UniTask API 不启用。 |
| 安装入口 | `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` |

```csharp
// Package Manager -> Add package from git URL
// https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
```

## YooAsset

YooAsset 是 Unity 资产管理与热更新框架，适合生产项目替代简单 `Resources` 加载。

在 YokiFrame 中的作用：

| 项 | 内容 |
|----|------|
| 推荐级别 | 推荐安装 |
| 宏定义 | `YOKIFRAME_YOOASSET_SUPPORT` |
| 使用方 | ResKit、SceneKit、UIKit |
| 缺省行为 | 未安装时 ResKit 使用当前项目可用的默认 Provider 或 Unity 资源回退。 |
| 安装入口 | `https://github.com/tuyoogame/YooAsset.git` |

```csharp
// ResKit 侧配置 YooAsset Provider 或 LoaderPool
// 具体实现放在 Unity Adapter / 项目扩展层，不写入 Base。
```

## Luban

Luban 是配置表解决方案，负责 Excel / CSV 到代码与数据文件的生成。TableKit 的编辑器配置、预览、验证和生成流程都围绕 Luban 工作流组织。

在 YokiFrame 中的作用：

| 项 | 内容 |
|----|------|
| 推荐级别 | TableKit 必需 |
| 宏定义 | `YOKIFRAME_LUBAN_SUPPORT` |
| 使用方 | TableKit |
| 缺省行为 | 未配置 Luban 时，TableKit 页面只保留配置提示和路径检查。 |
| 安装入口 | `https://github.com/focus-creative-games/luban` |

```powershell
# Tauri 后端通过 dotnet Luban.dll 执行生成和验证
dotnet Luban.dll -t client -c cs-simple-json
```

## FMOD

FMOD 是专业游戏音频中间件，适合需要复杂混音、动态音乐、事件路径和 3D 音频控制的项目。

在 YokiFrame 中的作用：

| 项 | 内容 |
|----|------|
| 推荐级别 | 可选 |
| 宏定义 | `YOKIFRAME_FMOD_SUPPORT` |
| 使用方 | AudioKit |
| 缺省行为 | 未安装时 AudioKit 使用 Unity AudioSource 后端或项目自定义后端。 |
| 安装入口 | `https://www.fmod.com/download#fmodforunity` |

```csharp
// FMOD 使用事件路径，而不是普通音频文件路径
AudioKit.Play("event:/Music/BGM_Boss");
```

## DOTween

DOTween 是 Unity 常用补间动画库。它适合 UI 面板动画、Transform 动画、材质动画和演出流程。

在 YokiFrame 中的作用：

| 项 | 内容 |
|----|------|
| 推荐级别 | 可选 |
| 宏定义 | `YOKIFRAME_DOTWEEN_SUPPORT` |
| 使用方 | ActionKit、UIKit |
| 缺省行为 | 未安装时使用内置动画或普通 Action 流程。 |
| 安装入口 | `https://dotween.demigiant.com/download.php` |

安装后需要运行 DOTween Setup，确保生成平台兼容配置。

## Unity Input System

Unity 官方新输入系统，适合需要输入重绑、设备抽象、触屏、手柄、动作映射和输入上下文的项目。

在 YokiFrame 中的作用：

| 项 | 内容 |
|----|------|
| 推荐级别 | 可选增强 |
| 宏定义 | `YOKIFRAME_INPUTSYSTEM_SUPPORT` |
| 使用方 | InputKit |
| 缺省行为 | 未安装时 InputKit 使用 Legacy Input 或项目自定义输入后端。 |
| 安装入口 | `com.unity.inputsystem` |

```csharp
// Package Manager -> Unity Registry -> Input System -> Install
```

## ZString

ZString 是 Cysharp 的零 GC 字符串构建工具，适合高频日志、状态快照、诊断文本和协议字符串拼接场景。

在 YokiFrame 中的作用：

| 项 | 内容 |
|----|------|
| 推荐级别 | 推荐安装 |
| 宏定义 | `YOKIFRAME_ZSTRING_SUPPORT` |
| 使用方 | 高频字符串构建路径 |
| 缺省行为 | 未安装时使用 `StringBuilder` 或普通 BCL 实现。 |
| 安装入口 | `https://github.com/Cysharp/ZString/releases` |

建议在 Unity 项目中使用 release 的 `.unitypackage`，避免原生依赖缺失。

## Nino

Nino 是高性能二进制序列化库，适合大存档、频繁读写和需要更小文件体积的项目。

在 YokiFrame 中的作用：

| 项 | 内容 |
|----|------|
| 推荐级别 | 可选增强 |
| 宏定义 | `YOKIFRAME_NINO_SUPPORT` |
| 使用方 | SaveKit |
| 缺省行为 | 未安装时 SaveKit 使用内置 JSON 或项目注入的序列化 Provider。 |
| 安装入口 | `https://github.com/JasonXuDeveloper/Nino.git` |

```csharp
// 安装后可在项目层把 SaveKit 切换到 Nino 序列化后端
// SaveKit.SetSerializer(new NinoSaveSerializer());
```

## 分层注意事项

| 层 | 处理原则 |
|----|----------|
| Base | 不直接引用 UnityEngine、DOTween、FMOD、YooAsset、Nino、System.Text.Json 等宿主或第三方库。 |
| Adapters/Unity | 可以封装 Unity 包、Package Manager 依赖和宿主生命周期。 |
| Tools | 对外保持统一 Kit API，具体第三方实现通过接口、Provider 或 Adapter 后端接入。 |
| TauriEditor | 只展示依赖状态、配置入口和文档，不把 Unity-only 假设写进跨引擎协议。 |
