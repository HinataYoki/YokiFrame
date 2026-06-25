# AudioKit 音频

AudioKit 是跨引擎音频门面。业务代码统一调用 `YokiFrame.AudioKit`，具体播放、资源加载、3D 空间化和宿主生命周期由 `IAudioBackend` 与对应 Adapter 实现。Unity 当前提供默认 `UnityAudioKitBackend` 和可选 `FmodAudioKitBackend`，后续 Godot 或其它音频后端也应复用同一组静态 API。

## 核心类型

| 类型 | 作用 |
|------|------|
| `AudioKit` | 静态统一入口，负责播放、停止、音量、资源生命周期、调试状态和历史记录。 |
| `IAudioBackend` | 跨引擎后端接口，Unity/Godot/项目自定义音频系统都从这里接入。 |
| `AudioPlayOptions` | 播放参数：Bus、Loop、Volume、Pitch、Fade、3D、FollowTarget、Rolloff。 |
| `AudioBus` / `AudioChannel` | 默认总线与兼容通道：Master、Music、Sfx、Voice、Ambience、UI。 |
| `AudioVoiceDebugInfo` | 当前活跃 Voice 的调试信息，供命令桥、Tauri 工作台和 AI 读取。 |
| `AudioHistoryRecord` | 播放、停止、淡出和音量变化历史。 |
| `AudioKitStats` | 当前后端、活跃数量、历史数量和总线音量。 |

## 后端状态

| 属性/方法 | 说明 |
|-----------|------|
| `HasBackend` | 是否已安装后端。 |
| `BackendName` | 当前后端名称。 |
| `SetBackend(backend)` | 设置音频后端。 |
| `GetBackend()` | 获取当前后端。 |
| `ClearBackend()` | 清除后端。 |
| `Reset()` | 重置 AudioKit 状态。 |

## 设置后端

Unity 项目通常由 `UnityBootstrap` 安装默认后端：

```csharp
using YokiFrame.Unity;

_ = UnityBootstrap.Instance;
```

也可以手动设置：

```csharp
using YokiFrame.Unity;
using YokiFrame;

AudioKit.SetBackend(new UnityAudioKitBackend());
```

Unity 项目安装 FMOD 并启用 `YOKIFRAME_FMOD_SUPPORT` 后，可以改用 FMOD Studio 事件后端：

```csharp
using YokiFrame.Unity;
using YokiFrame;

AudioKit.SetBackend(new FmodAudioKitBackend());
AudioKit.PlayMusic("event:/Music/Bgm", loop: true, volume: 0.8f);
```

`FmodAudioKitBackend` 位于 Unity Adapter 的可选程序集 `YokiFrame.Unity.AudioKit.FMOD`，只在 Unity + FMOD 宏开启时参与编译，不会进入跨引擎 AudioKit Runtime。

项目已有音频系统时，实现 `IAudioBackend` 后注入即可。不要新增 `UnityAudioKit2` 或 `GodotAudioKit2` 这类平行 API。

```csharp
AudioKit.SetBackend(new ProjectAudioBackend());
```

## 播放与通道

```csharp
using YokiFrame;

int sfxVoice = AudioKit.PlaySfx("Audio/Click", volume: 0.8f);
int musicVoice = AudioKit.PlayMusic("Audio/Bgm", loop: true, volume: 0.6f);

AudioKit.SetChannelVolume(AudioChannel.Sfx, 0.7f);
AudioKit.MuteChannel(AudioChannel.Sfx, false);
AudioKit.SetGlobalVolume(0.9f);
```

通用播放使用 `AudioPlayOptions`：

```csharp
var options = AudioPlayOptions.Default;
options.Bus = AudioBus.Ambience;
options.Loop = true;
options.Volume = 0.5f;
options.FadeInDuration = 0.25f;
options.FadeOutDuration = 0.5f;

int voiceId = AudioKit.Play("Audio/Rain", options);
```

`voiceId > 0` 表示后端创建了有效声音；Unity 后端找不到 `AudioClip` 时会返回 0。

## 音量与静音控制

AudioKit 提供多层级的音量和静音控制：

### 全局控制

```csharp
AudioKit.SetGlobalVolume(0.8f);
float globalVol = AudioKit.GetGlobalVolume();
AudioKit.MuteAll(true);
bool isMuted = AudioKit.IsMuted();
```

### 总线控制

```csharp
AudioKit.SetBusVolume("Music", 0.6f);
float musicVol = AudioKit.GetBusVolume("Music");
AudioKit.MuteBus("Music", true);
```

### 通道控制

```csharp
AudioKit.SetChannelVolume(AudioChannel.Sfx, 0.7f);
float sfxVol = AudioKit.GetChannelVolume(AudioChannel.Sfx);
AudioKit.MuteChannel(AudioChannel.Sfx, true);
```

### 暂停与恢复

```csharp
AudioKit.PauseAll();
AudioKit.ResumeAll();
```

### 停止总线

```csharp
AudioKit.StopBus("Music");
AudioKit.StopChannel(AudioChannel.Sfx);
```

## 资源生命周期

AudioKit 属于 Tool 层，默认资源加载器走 Core 层 `ResKit`。项目需要接入 Addressables、YooAsset、FMOD 事件表或其它加载系统时，实现 `IAudioResourceLoader` 后覆盖即可；旧的 `SetResourceProvider` 仍作为兼容入口：

```csharp
AudioKit.SetResourceLoader(projectAudioLoader);
// 或兼容旧项目：
AudioKit.SetResourceProvider(projectResourceProvider);

AudioKit.Preload("Audio/Click");
AudioKit.PreloadAsync("Audio/Bgm", () => { });
AudioKit.Unload("Audio/Click");
AudioKit.UnloadAll();
```

### 资源加载器 API

| 属性/方法 | 说明 |
|-----------|------|
| `ResourceLoaderName` | 当前资源加载器名称。 |
| `SetResourceLoader(loader)` | 设置音频资源加载器。 |
| `GetResourceLoader()` | 获取当前资源加载器。 |
| `SetResourceProvider(provider)` | 兼容旧项目，设置资源 Provider。 |
| `LoadResource<T>(path)` | 同步加载音频资源。 |
| `LoadResourceAsync<T>(path, token)` | 异步加载音频资源。 |
| `ReleaseResource(asset)` | 释放音频资源。 |

### 预加载与卸载

| 方法 | 说明 |
|------|------|
| `Preload(path)` / `Preload(audioId)` | 同步预加载音频。 |
| `PreloadAsync(path, callback)` / `PreloadAsync(audioId, callback)` | 异步预加载音频。 |
| `Unload(path)` / `Unload(audioId)` | 卸载指定音频。 |
| `UnloadAll()` | 卸载所有已预加载的音频。 |

Unity `UnityAudioKitBackend` 当前通过 `AudioKit.GetResourceLoader()` 加载 `AudioClip`；未自定义时即 `ResKitAudioResourceLoader`。如果项目还没有配置 ResKit Provider，Unity 后端会继续兜底尝试 `Resources.Load<AudioClip>`，避免旧资源路径立刻失效。FMOD 后端使用 FMOD Studio 事件路径，`Preload/Unload` 对应 `EventDescription.loadSampleData/unloadSampleData`，不消费 `AudioClip` 资源加载器。

## 音频 ID 生成

Tauri AudioKit 页面内置音频索引生成器。填写扫描文件夹、输出文件、命名空间、类名和起始 ID 后，点击扫描预览，再点击生成即可写出 `AudioIds` 常量类和 `AudioPaths.Map` 路径表。生成逻辑运行在 Tauri 后端，不依赖 Unity EditorWindow。

## 3D 与跟随目标

固定位置播放：

```csharp
var options = AudioPlayOptions.Default;
options.MinDistance = 2f;
options.MaxDistance = 30f;
options.RolloffMode = AudioRolloffMode.Linear;

AudioKit.Play3D("Audio/Explosion", new YokiVector3(1f, 2f, 3f), options);
```

跟随跨引擎对象：

```csharp
AudioKit.Play3D("Audio/Engine", playerEngineObject, AudioPlayOptions.Default);
```

`IEngineObject` 只暴露引擎无关的 `Name/IsActive/Position` 等能力，Unity `Transform`、Godot `Node` 等对象必须在 Adapter 层包装后再传入。

## 停止与淡出

```csharp
AudioKit.Stop(voiceId);
AudioKit.StopWithFade(voiceId);
AudioKit.StopWithFade(voiceId, 0.6f);
AudioKit.StopChannel(AudioChannel.Sfx);
AudioKit.StopAll();
```

`StopWithFade(voiceId)` 会使用播放时的 `AudioPlayOptions.FadeOutDuration`。显式传入时长时，负数会归零。


## Tauri 工作台

Tauri AudioKit 页面读取顺序为：

1. `read_telemetry("AudioKit", "state")`
2. `read_snapshot("AudioKit", "state")`
3. `send_command("AudioKit", "get_workbench_snapshot")`

Unity Editor 侧的 `KitStateSnapshotPublisher` 会节流发布 `.yokiframe/engines/<engineId>/snapshots/AudioKit/state.json`，并写入共享内存 telemetry。页面只在缺少 telemetry/snapshot、用户点击刷新或执行停止/清空动作时走命令桥，避免高频 UI 刷新占用 command/result 队列。

## 诊断与调试

AudioKit 提供诊断 API 用于调试和工具面板：

| 方法 | 说明 |
|------|------|
| `GetStats()` | 获取当前统计信息（后端、活跃数量、历史数量、总线音量）。 |
| `GetActiveVoices(list)` | 获取当前活跃 Voice 列表。 |
| `GetHistory(list)` | 获取播放和停止历史。 |
| `GetBuses(list)` | 获取总线调试信息。 |
| `ClearHistory()` | 清空历史记录。 |

```csharp
var stats = AudioKit.GetStats();
Debug.Log($"Backend: {stats.BackendName}, Active: {stats.ActiveVoiceCount}");

var voices = new List<AudioVoiceDebugInfo>();
AudioKit.GetActiveVoices(voices);

var buses = new List<AudioBusDebugInfo>();
AudioKit.GetBuses(buses);
```

## 查询运行时数据

业务或自定义工具可以直接读取 Base 调试 API：

```csharp
var stats = AudioKit.GetStats();

var activeVoices = new List<AudioVoiceDebugInfo>();
AudioKit.GetActiveVoices(activeVoices);

var history = new List<AudioHistoryRecord>();
AudioKit.GetHistory(history);

AudioKit.ClearHistory();
```

这些 API 适合调试和工具面板，不建议在热路径里频繁构造临时列表。

## 常见问题

| 问题 | 处理方式 |
|------|----------|
| `AudioKit backend is not configured` | 先创建 `UnityBootstrap`，或调用 `AudioKit.SetBackend(...)`。 |
| `PlaySfx` 返回 0 | Unity 后端没有找到对应 `AudioClip`，检查 Provider、Resources 路径或手动 `RegisterClip()`。 |
| FMOD 事件不播放 | 确认已安装 FMOD Unity、启用 `YOKIFRAME_FMOD_SUPPORT`，并使用 `event:/...` 事件路径。 |
| 3D 音频没有空间效果 | 确认使用 `Play3D`，并检查 `MinDistance/MaxDistance/RolloffMode`。 |
| Tauri 工作台没有数据 | 先看 engine 是否在线；再检查 `AudioKit/state` snapshot，必要时发送 `AudioKit/get_workbench_snapshot`。 |
| 活跃列表没有自动清理 | 确认宿主循环调用 `AudioKit.Update(deltaTime)`；Unity 后端读取活跃声音时也会做一次轻量清理。 |
