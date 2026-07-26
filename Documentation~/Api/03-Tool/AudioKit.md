# AudioKit

> 面向读者：需要统一播放、逻辑 Bus、3D 跟随和后端切换的 Runtime 开发者
>
> 主要入口：`AudioKit`、`AudioVoiceHandle`
>
> 运行边界：跨宿主 Runtime；Unity/Godot 实现位于独立 Adapter
>
> 状态来源：`Documentation~/Api/00-GettingStarted/Kit_Status.md`

## 适用场景

AudioKit 是跨宿主音频播放 Tool Kit。需要统一播放、逻辑 Bus、音量、3D 跟随、预加载和后端替换时使用它。门面、参数和契约保持纯 C# 9；Unity AudioSource 与 Godot AudioStreamPlayer 具体实现位于 Tool 自有 Adapter。

## 入口与当前状态

| 项目 | 当前值 |
|---|---|
| Runtime | 已实现，位于 `Tools/AudioKit/Runtime`，程序集为 `YokiFrame.AudioKit` |
| Adapter | Unity/Godot 默认后端已实现，独立程序集接入宿主 API |
| Interaction | 已实现，只发布 `stats` 与 `get_workbench_snapshot` 两个只读 action |
| Workbench | 已实现，只读展示 Bus、active voice、播放进度、播放历史和稳定音频索引 |
| 状态入口 | `AudioKit/state` |

## 快速上手

宿主启动时只注册默认后端工厂；第一次播放或预加载才创建后端：

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

显式后端优先于默认工厂。替换或清除后端会增加 generation，旧 `AudioVoiceHandle` 不会停止新后端中恰好复用的 voice id。

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
| `Stop(AudioVoiceHandle handle)` | 停止当前 backend generation 的 voice；无效或过期句柄返回 `false`。 |
| `StopWithFade(AudioVoiceHandle handle, float fadeDuration)` | 按有限非负秒数淡出停止。 |
| `StopAll()` / `StopBus(string bus)` | 停止全部 voice 或指定 Bus 的 voice；传入 `Master` 等价 `StopAll`；没有后端时无副作用。 |
| `PauseAll()` / `ResumeAll()` | 暂停或恢复当前后端全部 voice；不会创建默认后端。 |

`AudioVoiceHandle` 是不可变值类型，公开成员为 `BackendGeneration`、`VoiceId`、`IsValid`、`Equals`、`==`、`!=` 和 `GetHashCode`。业务必须保存完整句柄，不能只保存 `VoiceId`。

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

`IAudioFollowTarget` 只有 `Name`、`IsAlive` 和 `Position`。Unity Adapter 可用 `Transform` 包装，Godot Adapter 可用 `Node3D` 包装；跟随和自然结束回收由 Core FrameLoop 驱动，没有公开 `AudioKit.Update`。

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

### 后端、资源加载和预加载

| API | 说明 |
|---|---|
| `BackendName` / `HasBackend` | 当前后端名称和创建状态；读取不创建。 |
| `SetBackend(IAudioBackend backend)` | 显式设置后端并接管其生命周期；null 抛 `ArgumentNullException`。 |
| `GetBackend()` | 获取已创建后端，不触发默认工厂。 |
| `ClearBackend()` | 停止、卸载并释放当前后端，但保留默认工厂和 Bus 配置。 |
| `Reset()` | 清理后端、音量、资源加载器、自定义 Bus 和工具诊断；保留默认工厂。 |
| `ResourceLoaderName` | 当前资源加载器名称。 |
| `SetResourceLoader(IAudioResourceLoader loader)` | 设置原生后端使用的显式加载器。 |
| `GetResourceLoader()` / `ClearResourceLoader()` | 获取显式加载器，或恢复共享 ResKit loader。 |
| `Preload(string path)` / `PreloadAsync(string path, CancellationToken)` | 预加载资源，会创建默认后端。 |
| `Unload(string path)` / `UnloadAll()` | 卸载缓存；没有后端时不会创建后端。 |

`IAudioBackend` 必须实现 `BackendName`、`Capabilities`、`Play`、`PlayAsync`、`Stop`、`StopWithFade`、`StopAll`、`StopBus`、`PauseAll`、`ResumeAll`、`Preload`、`PreloadAsync`、`Unload`、`UnloadAll`、`SetBusVolume`、`GetBusVolume`、`Update` 和 `Dispose`。用 `AudioBackendCapabilities` 声明真实支持的 `AsyncLoading`、`LoopOverride`、`SpatialAudio`、`RolloffOverride`、`FollowTarget` 和 `Preload`；不要虚报 capability。

`IAudioResourceLoader` 提供 `LoaderName`、`Load<T>`、`LoadAsync<T>` 和 `Release(object)`。默认 `ResKitAudioResourceLoader` 共享 ResKit；`DelegateAudioResourceLoader` 适合项目接入自有同步/异步资源函数。AudioKit 不再静默回退 Unity `Resources.Load`、Godot `ResourceLoader.Load` 或拼接 `Audio/{id}`。

## 宿主与工具入口

工具构建额外提供 `DiagnosticVersion`、`HistoryTotalCount`、`GetActiveVoices(List<AudioVoiceSnapshot>)`、`GetHistory(List<AudioHistoryEntry>)`、`GetBuses(List<AudioBusSnapshot>)` 和 `ClearHistory()`。历史最多 128 条，按最新优先复制；这些诊断 API 不会暴露为 Workbench 或通用 Interaction 的写操作。

Workbench 通过 `AudioKit/state` 只读消费 Master/Bus、active voice 和有界历史。页面按 Bus 分组，选中 Bus 后显示当前音频、播放进度与 `play_started` 播放历史；控制、音量和静音诊断不会进入播放历史列表。顶部只提供 Bus 覆盖率与裁剪/stale 警示、搜索、来源和仅活跃筛选，底部保留稳定索引抽屉。Interaction 只发布 `stats`、`get_workbench_snapshot`，不发布停止、音量、静音或清历史 UserAction；Workbench 周期刷新也不发送命令。游戏业务仍通过 Runtime `AudioKit` 门面管理播放与混音。

Audio 稳定索引属于 .NET 10 工具，不是 Runtime API：

```powershell
yoki audio index scan --project <projectRoot> --scan Assets/Art/Audio
yoki audio index generate --project <projectRoot> --scan Assets/Art/Audio --output Assets/Scripts/Generated/AudioIds.cs --manifest Assets/Settings/YokiFrame/audio-index.json --namespace GameAudio --class AudioIds --start-id 1001
```

Manifest 是“音频路径 -> 稳定整数 ID”的分配账本；已删除路径的历史分配保留，不因扫描排序变化重排已有 ID。生成代码和 manifest 以临时文件加原子替换写入。

## 生命周期与错误边界

- 后端由 AudioKit 接管；`ClearBackend`、宿主 reset 和后端替换会停止 voice、卸载资源并调用 `Dispose`。
- 后端 generation 变化后旧句柄必然失效；不要跨后端保存并复用旧句柄。
- `PlayAsync` 的取消只约束当前启动请求；底层资源系统是否取消由 backend/loader 决定。
- 音频更新由 Core FrameLoop 投递 scaled delta；业务不要再创建第二个更新循环。

## 限制与相关资料

- AudioKit 不静默回退 Unity `Resources.Load`、Godot `ResourceLoader.Load` 或 `Audio/{id}` 路径拼接
- 稳定音频索引和只读观察入口见 [Workbench、CLI 与 Installer](../../Guides/Tooling.md)
