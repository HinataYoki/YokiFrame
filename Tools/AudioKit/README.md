# AudioKit - FMOD 集成指南

AudioKit 支持 FMOD Studio 作为音频后端，提供专业级音频功能。

## 快速开始

### 1. 安装 FMOD

1. 从 [FMOD 官网](https://www.fmod.com/download) 下载 FMOD for Unity 插件
2. 导入到项目的 `Assets/Plugins/FMOD` 目录
3. 配置 FMOD Settings（Edit > Project Settings > FMOD）

### 2. 启用 FMOD 支持

在 **Project Settings > Player > Other Settings > Scripting Define Symbols** 添加：

```
YOKIFRAME_FMOD_SUPPORT
```

### 3. 初始化

```csharp
// 游戏启动时设置 FMOD 后端
AudioKit.SetBackend(new FmodAudioBackend());
```

## 基本用法

### 播放音频

```csharp
// 使用 FMOD 事件路径
AudioKit.Play("event:/SFX/Click", AudioChannel.UI);
AudioKit.Play("event:/Music/BGM_Main", AudioChannel.Bgm);

// 推荐：使用 int ID + PathResolver（避免魔法字符串）
AudioKit.SetPathResolver(id => AudioConfig.GetFmodPath(id));
AudioKit.Play(1001, AudioChannel.Sfx);
```

### 3D 音效

```csharp
// 在指定位置播放
AudioKit.Play3D("event:/SFX/Explosion", position);

// 跟随目标
AudioKit.Play3D("event:/SFX/Engine", vehicleTransform);
```

### 音频控制

```csharp
var handle = AudioKit.Play("event:/Music/BGM", AudioChannel.Bgm);

handle.Pause();           // 暂停
handle.Resume();          // 恢复
handle.Stop();            // 停止
handle.StopWithFade(1f);  // 淡出停止
handle.Volume = 0.5f;     // 调整音量
handle.Pitch = 1.2f;      // 调整音调
```

## FMOD 事件路径格式

```
event:/文件夹/事件名

示例：
event:/Music/BGM_Battle
event:/SFX/UI/Click
event:/Voice/NPC/Greeting
event:/Ambient/Forest
```

## 预加载

```csharp
// 同步预加载
AudioKit.Preload("event:/Music/BGM_Boss");

// 异步预加载
await AudioKit.PreloadUniTaskAsync("event:/Music/BGM_Boss");

// 卸载
AudioKit.Unload("event:/Music/BGM_Boss");
```

## 通道控制

```csharp
// 设置通道音量
AudioKit.SetChannelVolume(AudioChannel.Bgm, 0.5f);

// 静音通道
AudioKit.MuteChannel(AudioChannel.Voice, true);

// 停止通道所有音频
AudioKit.StopChannel(AudioChannel.Sfx);
```

## 架构说明

```
AudioKit (门面)
    │
    ├── IAudioBackend (策略接口)
    │       ├── UnityAudioBackend (Unity 原生)
    │       └── FmodAudioBackend (FMOD Studio)
    │
    └── IAudioHandle (句柄接口)
            ├── UnityAudioHandle
            └── FmodAudioHandle
```

## 注意事项

1. **事件路径** - FMOD 使用 `event:/...` 格式，不是文件路径
2. **Bank 管理** - FMOD Bank 由 RuntimeManager 自动管理
3. **3D 衰减** - 由 FMOD Studio 事件设置控制，AudioPlayConfig 的 MinDistance/MaxDistance 不影响 FMOD
4. **后端切换** - 切换前需调用 `AudioKit.StopAll()` 和 `AudioKit.UnloadAll()`
