# 第三方库索引

## 总览

| 库 | 推荐级别 | 宏定义 | 影响范围 |
|---|---|---|---|
| UniTask | 推荐 | `YOKIFRAME_UNITASK_SUPPORT` | ResKit、SceneKit、AudioKit、UIKit、InputKit、LocalizationKit 等异步入口。 |
| YooAsset | 推荐 | `YOKIFRAME_YOOASSET_SUPPORT` | ResKit、SceneKit、UIKit 默认面板加载。 |
| Luban | TableKit 必需 | `YOKIFRAME_LUBAN_SUPPORT` | TableKit 生成、验证和运行时代码。 |
| FMOD | 按需 | `YOKIFRAME_FMOD_SUPPORT` | AudioKit FMOD 后端。 |
| DOTween | 按需 | `YOKIFRAME_DOTWEEN_SUPPORT` | UIKit / ActionKit 动画集成。 |
| Unity Input System | 按需 | `YOKIFRAME_INPUTSYSTEM_SUPPORT` | InputKit Unity 输入后端。 |
| ZString | 推荐 | `YOKIFRAME_ZSTRING_SUPPORT` | 高频字符串构建优化。 |
| Nino | 按需 | `YOKIFRAME_NINO_SUPPORT` | SaveKit 序列化后端。 |

## 安装入口

| 库 | 入口 |
|---|---|
| UniTask | `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask` |
| YooAsset | `https://github.com/tuyoogame/YooAsset.git` |
| Luban | `https://github.com/focus-creative-games/luban` |
| FMOD | `https://www.fmod.com/download#fmodforunity` |
| DOTween | `https://dotween.demigiant.com/download.php` |
| Unity Input System | Unity Package Manager: `com.unity.inputsystem` |
| ZString | `https://github.com/Cysharp/ZString/releases` |
| Nino | `https://github.com/JasonXuDeveloper/Nino.git` |

## 逐项说明

### UniTask

Unity 异步流程推荐安装。启用后，同名异步 API 从 `Task<T>` 切到 `UniTask<T>`，更适合取消令牌和 Unity PlayerLoop。

### YooAsset

用于生产级资源管理。切换到 YooAsset 后，仍然通过 `ResKit.SetProvider(new YooAssetResourceProvider())` 接入，不改变业务调用入口。

### Luban

TableKit 必需。Tauri 后端通过 `dotnet Luban.dll` 执行验证和生成；生成产物写入用户项目代码输出目录。

### FMOD

AudioKit 可选后端。使用 FMOD 时播放路径通常是 `event:/...`，不是普通音频文件路径。

### DOTween

UIKit / ActionKit 可选动画集成。安装后需要运行 DOTween Setup，确保平台配置完整。

### Unity Input System

InputKit 的 Unity 新输入系统后端。需要重绑、多设备、手柄和触屏时安装。

### ZString

用于减少热路径字符串构建分配。适合日志、快照和诊断文本。

### Nino

SaveKit 可选二进制序列化后端。适合大存档和频繁读写。

## 分层边界

| 层 | 规则 |
|---|---|
| Core Runtime | 不直接引用 UnityEngine、DOTween、FMOD、YooAsset、Nino 等宿主或第三方库。 |
| Unity Adapter | 可以封装 Unity 包和 Package Manager 依赖。 |
| Tools | 对外保持统一 Kit API，第三方实现通过 Provider / Backend 接入。 |
| TauriEditor | 只展示依赖状态、配置入口和文档，不写死 Unity-only 假设。 |
