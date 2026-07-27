# AudioKit 音频

## 适用场景

AudioKit 是跨宿主音频播放 Tool Kit。需要统一播放、逻辑 Bus、音量、3D 跟随、预加载和后端替换时使用它；Unity 与 Godot 的具体播放器由宿主接入提供。

## 使用前提

AudioKit 可用于 Unity 与 Godot .NET Runtime。宿主提供默认音频来源；Workbench 只读展示 Bus、播放进度和历史，不提供远程停止、静音或调音操作。

## 快速上手

第一次播放或预加载时会启用宿主默认音频来源：

```csharp
using YokiFrame;

AudioVoiceHandle music = AudioKit.PlayMusic("Audio/Music/Menu");
AudioVoiceHandle click = AudioKit.PlaySfx("Audio/Sfx/Click", 0.9f);

AudioKit.Stop(click);
AudioKit.StopWithFade(music, 0.5f);
```

项目需要自定义后端时显式注入：

```csharp
AudioKit.SetBackend(new ProjectAudioBackend());
```

显式后端优先于宿主默认来源。替换或清除后端会使已有 `AudioVoiceHandle` 失效；旧句柄不会误停新后端中的声音。

## 核心 API

| API | 说明 |
|---|---|
| `Play(string path)` | 使用 `AudioPlayOptions.Default` 同步播放。 |
| `Play(string path, AudioPlayOptions options)` | 按跨宿主参数同步播放。 |
| `PlayMusic(string path, bool loop = true, float volume = 1f)` | 使用 Music Bus 播放音乐。 |
| `PlaySfx(string path, float volume = 1f, float pitch = 1f)` | 使用 Sfx Bus 播放一次音效。 |
| `Play3D(string path, Vector3 position, AudioPlayOptions options = default)` | 在固定 `System.Numerics.Vector3` 位置播放 3D 音频。 |
| `Play3D(string path, IAudioFollowTarget target, AudioPlayOptions options = default)` | 播放跟随位置目标的 3D 音频。 |
| `PlayAsync(string path, AudioPlayOptions options, CancellationToken token = default)` | 异步加载并播放，返回 `Task<AudioVoiceHandle>`。 |
| `Stop(AudioVoiceHandle handle)` | 停止句柄对应的声音；无效或过期句柄返回 `false`。 |
| `StopWithFade(AudioVoiceHandle handle, float fadeDuration)` | 按有限非负秒数淡出停止。 |
| `StopAll()` / `StopBus(string bus)` | 停止全部 voice 或指定 Bus 的 voice；传入 `Master` 等价 `StopAll`；没有后端时无副作用。 |
| `PauseAll()` / `ResumeAll()` | 暂停或恢复当前后端全部 voice；不会创建默认后端。 |

`AudioVoiceHandle` 是不可变值类型。把它作为整体保存和传回 `Stop`；更换后端后旧句柄会自动失效，不要只保存其中的 `VoiceId`。

### `AudioPlayOptions` 与 3D

| 字段 | 说明 |
|---|---|
| `Bus` | 逻辑 Bus，默认 `Sfx`。播放时把 `Master` 作为普通播放 Bus 会回退到 `Sfx`。 |
| `Loop` | 是否循环；只有后端声明支持时才能覆盖后端默认语义。 |
| `Volume` / `Pitch` | 音量限制到 0..1；pitch 为音高倍率，0 解释为 1，负数拒绝。 |
| `FadeInDuration` / `FadeOutDuration` | 淡入和默认淡出秒数；负数限制为 0。 |
| `Is3D` / `Position` | 是否空间播放和固定位置。 |
| `FollowTarget` | 可选 `IAudioFollowTarget`；有效目标会把当前坐标写入 position。 |
| `MinDistance` / `MaxDistance` | 3D 衰减距离，非正值使用默认值 1 和 500，最大值不小于最小值。 |
| `RolloffMode` | `Logarithmic`、`Linear` 或 `Custom`。 |
| `AudioPlayOptions.Default` | `Sfx`、音量 1、pitch 1、距离 1..500、对数衰减。 |

NaN、Infinity、负 pitch 和未知枚举值会抛参数异常；音量、时长和距离按上述规则归一化。`Vector3` 使用 `System.Numerics`，Core/Tool Runtime API 不接受 Unity 或 Godot 类型。

`IAudioFollowTarget` 只有 `Name`、`IsAlive` 和 `Position`。Unity 可用 `Transform` 包装，Godot 可用 `Node3D` 包装；跟随和自然结束回收由框架统一更新，没有公开 `AudioKit.Update`。

### Bus、音量和静音

内置 Bus 为 `Master`、`Music`、`Sfx`、`Voice`、`Ambience`、`UI`，名称定义在 `AudioBus.MASTER` 等常量以及对应属性中。

| API | 说明 |
|---|---|
| `RegisterBus(string bus)` | 显式注册自定义 Bus；新增时返回 `true`。名称非空、最长 128 字符且不能含控制字符。 |
| `UnregisterBus(string bus)` | 移除自定义声明；不停止 active voice，也不删除音量/静音配置。 |
| `IsBusRegistered(string bus)` | 判断内置或显式注册 Bus。 |
| `SetGlobalVolume(float volume)` / `GetGlobalVolume()` | 设置或读取 Master 配置音量，范围 0..1。读取不受静音影响。 |
| `MuteAll(bool muted)` / `IsMuted()` | 设置或读取 Master 静音。 |
| `SetBusVolume(string bus, float volume)` / `GetBusVolume(string bus)` | 设置或读取普通 Bus 音量；读取值包含静音效果。 |
| `MuteBus(string bus, bool muted)` / `IsBusMuted(string bus)` | 设置或读取普通 Bus 静音。 |

只设置音量、静音或注册 Bus 不会创建默认后端；后端已存在时才立即同步。Bus 名称比较不区分大小写。动态使用但未显式注册的 Bus 仍可以由后端和工具观察到。

### 后端和资源加载

| API | 说明 |
|---|---|
| `BackendName` / `HasBackend` | 查看当前后端名称和是否已经启用；读取不会创建后端。 |
| `SetBackend(IAudioBackend backend)` | 显式设置后端并交由 AudioKit 管理；null 抛 `ArgumentNullException`。 |
| `GetBackend()` | 获取当前已启用的后端。 |
| `ClearBackend()` | 停止并释放当前后端，同时保留 Bus 配置。 |
| `Reset()` | 清理当前后端、音量、资源加载器、自定义 Bus 和工具观察数据。 |
| `ResourceLoaderName` | 当前资源加载器名称。 |
| `SetResourceLoader(IAudioResourceLoader loader)` | 设置原生后端使用的显式加载器。 |
| `GetResourceLoader()` / `ClearResourceLoader()` | 获取显式加载器，或恢复共享 ResKit loader。 |
| `Preload(string path)` / `PreloadAsync(string path, CancellationToken)` | 预加载资源，会创建默认后端。 |
| `Unload(string path)` / `UnloadAll()` | 卸载缓存；没有后端时不会创建后端。 |

实现自定义后端时，实现 `IAudioBackend` 的播放、停止、暂停、预加载和释放能力，并通过 `AudioBackendCapabilities` 声明实际支持的功能。后端不支持的选项应返回失败或使用后端默认行为，不要在业务层假设所有宿主能力都存在。

`IAudioResourceLoader` 提供 `LoaderName`、`Load<T>`、`LoadAsync<T>` 和 `Release(object)`。默认 `ResKitAudioResourceLoader` 共享 ResKit；`DelegateAudioResourceLoader` 适合项目接入自有同步/异步资源函数。AudioKit 不再静默回退 Unity `Resources.Load`、Godot `ResourceLoader.Load` 或拼接 `Audio/{id}`。

## 生命周期与错误边界

- 后端由 AudioKit 接管；`ClearBackend`、宿主 reset 和后端替换会停止 voice、卸载资源并调用 `Dispose`。
- 后端更换后旧句柄必然失效；不要跨后端保存并复用旧句柄。
- `PlayAsync` 的取消只约束当前启动请求；底层资源系统是否取消由 backend/loader 决定。
- 音频更新由框架统一驱动；业务不要再创建第二个更新循环。

## 在工具中查看

Workbench 只读展示 Bus、活动音频、播放进度和有限历史，不提供停止、音量、静音或清除历史操作。游戏业务仍通过 Runtime `AudioKit` 门面管理播放与混音。

Audio 稳定索引属于 .NET 10 工具，不是 Runtime API：

```powershell
yoki audio index scan --project <projectRoot> --scan Assets/Art/Audio
yoki audio index generate --project <projectRoot> --scan Assets/Art/Audio --output Assets/Scripts/Generated/AudioIds.cs --manifest Assets/Settings/YokiFrame/audio-index.json --namespace GameAudio --class AudioIds --start-id 1001
```

Manifest 是“音频路径 -> 稳定整数 ID”的分配账本。CLI 会保留历史分配，避免扫描顺序变化导致已有 ID 改变；不要手动重排或删除其中的历史记录。

## 限制与相关资料

- AudioKit 不静默回退 Unity `Resources.Load`、Godot `ResourceLoader.Load` 或 `Audio/{id}` 路径拼接
- 稳定音频索引使用本页的 `yoki audio index` 命令；运行态观察直接打开 Workbench 的 AudioKit 页面。
