# AudioKit 音频

## 配置后端

统一初始化会安装 Unity 默认后端：

```csharp
using YokiFrame;

YokiFrameKit.Initialize(YokiFrameEngine.Unity);
```

手动设置：

```csharp
using YokiFrame;
using YokiFrame.Unity;

AudioKit.SetBackend(new UnityAudioKitBackend());
```

FMOD 可选后端：

```csharp
#if YOKIFRAME_FMOD_SUPPORT
AudioKit.SetBackend(new FmodAudioKitBackend());
AudioKit.PlayMusic("event:/Music/Bgm", loop: true, volume: 0.8f);
#endif
```

已有项目音频系统时实现 `IAudioBackend`，不要新增平行 `UnityAudioKit2` / `GodotAudioKit2`。

## 播放和停止

```csharp
int sfx = AudioKit.PlaySfx("Audio/Click", volume: 0.8f);
int music = AudioKit.PlayMusic("Audio/Bgm", loop: true, volume: 0.6f);

AudioKit.Stop(sfx);
AudioKit.StopWithFade(music, 0.5f);
AudioKit.StopAll();
```

通用参数：

```csharp
var options = AudioPlayOptions.Default;
options.Bus = AudioBus.Ambience;
options.Loop = true;
options.Volume = 0.5f;
options.FadeInDuration = 0.25f;
options.FadeOutDuration = 0.5f;

int voiceId = AudioKit.Play("Audio/Rain", options);
```

`voiceId > 0` 表示后端创建了有效 voice。

## 音量和静音

```csharp
AudioKit.SetGlobalVolume(0.9f);
AudioKit.MuteAll(false);

AudioKit.SetBusVolume("Music", 0.6f);
AudioKit.MuteBus("Music", true);

AudioKit.SetChannelVolume(AudioChannel.Sfx, 0.7f);
AudioKit.MuteChannel(AudioChannel.Sfx, false);
```

## 资源加载

默认音频资源加载器走 ResKit。需要接 Addressables、YooAsset、FMOD 表或项目系统时，实现 `IAudioResourceLoader`。

```csharp
AudioKit.SetResourceLoader(projectAudioLoader);

AudioKit.Preload("Audio/Click");
AudioKit.PreloadAsync("Audio/Bgm", OnLoaded);
AudioKit.Unload("Audio/Click");
AudioKit.UnloadAll();
```

Unity `UnityAudioKitBackend` 默认通过当前 `AudioKit.GetResourceLoader()` 加载 `AudioClip`；未配置时会兜底尝试 `Resources.Load<AudioClip>`。

## 3D 音频

```csharp
var options = AudioPlayOptions.Default;
options.MinDistance = 2f;
options.MaxDistance = 30f;
options.RolloffMode = AudioRolloffMode.Linear;

AudioKit.Play3D("Audio/Explosion", new YokiVector3(1f, 2f, 3f), options);
```

跟随对象时传 `IEngineObject`，Unity `Transform` 或 Godot `Node` 要由 Adapter 包装后再传入。

## 音频 ID 生成

Tauri AudioKit 页面内置音频索引生成器。它扫描音频文件夹，生成 `AudioIds` 常量类和 `AudioPaths.Map`，用于减少字符串路径散落。

## 工作台诊断

AudioKit 页面用于查看后端、总线、活跃 voice、播放历史、音量状态和音频索引生成器。

| 在工作台里看什么 | 用途 |
|---|---|
| Backend | 确认音频后端是否已配置。 |
| Bus / Volume | 检查总线音量、静音和全局音量。 |
| Active Voices | 查看正在播放的声音、循环状态和来源。 |
| History | 回看播放、停止、清理历史。 |
| Audio Id / Path 工具 | 生成或核对项目里的音频标识。 |

没有声音时，先看后端和总线音量，再看 Active Voices 是否出现目标声音。停止声音、清空历史等按钮会改变运行状态，只在明确需要时使用。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 后端未配置 | 初始化宿主或 `AudioKit.SetBackend(...)`。 |
| `PlaySfx` 返回 0 | 检查路径、Provider、Resources 或后端资源加载器。 |
| FMOD 不播放 | 确认宏、包、事件路径 `event:/...` 都正确。 |
| 3D 没空间效果 | 使用 `Play3D` 并检查距离和 rolloff。 |
| 工作台没数据 | 先看 System 页连接状态，再回到 AudioKit 页刷新。 |
| 活跃列表不清理 | 确认宿主循环调用 `AudioKit.Update(deltaTime)`。 |
