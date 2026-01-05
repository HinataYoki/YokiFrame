# YokiFrame

一个轻量级的 Unity 开发框架，提供架构设计、事件系统、动作序列、状态机、UI管理、音频管理、存档系统等常用功能模块。

## 📑 目录

- [安装](#-安装)
- [核心模块 (Core)](#核心模块-core)
  - [架构系统 (Architecture)](#-核心架构-architecture)
  - [事件系统 (EventKit)](#-事件系统-eventkit)
  - [单例工具 (SingletonKit)](#-单例工具-singletonkit)
  - [对象池 (PoolKit)](#-对象池-poolkit)
  - [资源管理 (ResKit)](#-资源管理-reskit)
  - [日志系统 (KitLogger)](#-日志系统-kitlogger)
  - [数据绑定 (Bindable)](#-数据绑定-bindable)
  - [扩展方法 (FluentApi)](#-扩展方法-fluentapi)
- [工具模块 (Tools)](#工具模块-tools)
  - [动作系统 (ActionKit)](#-动作系统-actionkit)
  - [状态机 (FsmKit)](#-状态机-fsmkit)
  - [UI管理 (UIKit)](#-ui管理-uikit)
  - [音频管理 (AudioKit)](#-音频管理-audiokit)
  - [存档系统 (SaveKit)](#-存档系统-savekit)
- [License](#-license)

---

## 📦 安装

通过 Unity Package Manager 安装：
1. 打开 `Window > Package Manager`
2. 点击 `+` > `Add package from git URL`
3. 输入：`https://github.com/HinataYoki/YokiFrame.git`

---

# 核心模块 (Core)

## 🏗️ 核心架构 (Architecture)

基于服务定位器模式的轻量级架构，支持服务注册与获取。

```csharp
// 1. 定义你的架构
public class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void OnInit()
    {
        Register(new PlayerService());
        Register(new AudioService());
    }
}

// 2. 定义服务
public class PlayerService : AbstractService
{
    public int Health { get; set; } = 100;
    protected override void OnInit() { }
}

// 3. 使用服务
var playerService = GameArchitecture.Interface.GetService<PlayerService>();
playerService.Health -= 10;
```

## 📡 事件系统 (EventKit)

类型安全的全局事件系统，支持 TypeEvent 和 EnumEvent 两种模式。

### TypeEvent - 基于类型的事件

```csharp
// 定义事件
public struct PlayerDiedEvent { public string PlayerName; }

// 注册事件
EventKit.Type.Register<PlayerDiedEvent>(e => Debug.Log($"{e.PlayerName} 死亡了"))
    .UnRegisterWhenGameObjectDestroyed(gameObject);

// 发送事件
EventKit.Type.Send(new PlayerDiedEvent { PlayerName = "Player1" });
```

### EnumEvent - 基于枚举的事件

```csharp
public enum GameEvent { GameStart, GamePause, ScoreChanged }

// 注册无参事件
EventKit.Enum.Register(GameEvent.GameStart, () => Debug.Log("游戏开始"))
    .UnRegisterWhenGameObjectDestroyed(gameObject);

// 注册有参事件
EventKit.Enum.Register<GameEvent, int>(GameEvent.ScoreChanged, score => Debug.Log($"分数: {score}"));

// 发送事件
EventKit.Enum.Send(GameEvent.GameStart);
EventKit.Enum.Send(GameEvent.ScoreChanged, 100);
```

## 🔧 单例工具 (SingletonKit)

支持普通类和 MonoBehaviour 的单例模式。

```csharp
// 普通单例
public class GameManager : ISingleton
{
    public static GameManager Instance => SingletonKit<GameManager>.Instance;
    public void OnSingletonInit() { }
}

// Mono单例
[MonoSingletonPath("Managers/AudioManager")]
public class AudioManager : MonoBehaviour, ISingleton
{
    public static AudioManager Instance => SingletonKit<AudioManager>.Instance;
    public void OnSingletonInit() => DontDestroyOnLoad(gameObject);
}
```

## 🏊 对象池 (PoolKit)

高效的对象池管理。

```csharp
// 使用临时容器（自动回收）
Pool.List<int>(list => { list.Add(1); list.Add(2); });
Pool.Dictionary<string, int>(dict => { dict["key"] = 100; });

// 自定义对象池
public class Bullet : IPoolable
{
    public bool IsRecycled { get; set; }
    public void OnRecycled() => Debug.Log("子弹被回收");
}

var pool = new SimplePoolKit<Bullet>(() => new Bullet());
var bullet = pool.Allocate();
pool.Recycle(bullet);
```

## 📦 资源管理 (ResKit)

统一的资源加载接口，默认使用 Resources，支持扩展 YooAsset 等第三方加载方案。

```csharp
// 同步加载
var prefab = ResKit.Load<GameObject>("Prefabs/Player");

// 异步加载
ResKit.LoadAsync<GameObject>("Prefabs/Enemy", prefab => Instantiate(prefab));

// 实例化
var player = ResKit.Instantiate("Prefabs/Player");

// 使用句柄管理引用计数
var handler = ResKit.LoadAsset<GameObject>("Prefabs/Player");
handler.Release();  // 引用计数减少，归零时自动卸载

// 清理所有缓存
ResKit.ClearAll();
```

<details>
<summary>📖 扩展 YooAsset</summary>

```csharp
// 一行代码切换加载方案
ResKit.SetLoaderPool(new YooAssetResLoaderPool());

// YooAsset 加载池实现
public class YooAssetResLoaderPool : AbstractResLoaderPool
{
    protected override IResLoader CreateLoader() => new YooAssetResLoader(this);
}

public class YooAssetResLoader : IResLoader
{
    private readonly IResLoaderPool mPool;
    private AssetHandle mHandle;

    public YooAssetResLoader(IResLoaderPool pool) => mPool = pool;

    public T Load<T>(string path) where T : UnityEngine.Object
    {
        mHandle = YooAssets.LoadAssetSync<T>(path);
        return mHandle.AssetObject as T;
    }

    public void LoadAsync<T>(string path, Action<T> onComplete) where T : UnityEngine.Object
    {
        mHandle = YooAssets.LoadAssetAsync<T>(path);
        mHandle.Completed += handle => onComplete?.Invoke(handle.AssetObject as T);
    }

    public void UnloadAndRecycle()
    {
        mHandle?.Release();
        mHandle = null;
        mPool.Recycle(this);
    }
}
```

</details>

## 📝 日志系统 (KitLogger)

支持加密和文件写入的日志系统。

```csharp
KitLogger.Log("普通日志");
KitLogger.Warning("警告日志");
KitLogger.Error("错误日志");

KitLogger.Level = KitLogger.LogLevel.Warning;  // 只显示Warning及以上
KitLogger.AutoEnableWriteLogToFile = true;     // 启用文件写入
```

## 🔗 数据绑定 (Bindable)

响应式数据绑定。

```csharp
public class PlayerModel
{
    public BindValue<int> Health = new(100);
}

var model = new PlayerModel();
model.Health.Bind(value => healthText.text = $"HP: {value}")
    .UnRegisterWhenGameObjectDestroyed(gameObject);

model.Health.Value -= 10;  // 自动触发回调
model.Health.SetValueWithoutEvent(50);  // 不触发事件
```

## 🛠️ 扩展方法 (FluentApi)

便捷的链式扩展方法。

```csharp
transform.ResetTransform();  // 重置位置、旋转、缩放
var pos2d = transform.Position2D();
var button = gameObject.FindComponent<Button>("BtnStart");
gameObject.Parent(parentTransform);
```

---

# 工具模块 (Tools)

## 🎬 动作系统 (ActionKit)

链式调用的动作序列系统，支持延时、回调、并行、循环等。

```csharp
// 延时执行
ActionKit.Delay(2f, () => Debug.Log("2秒后执行")).Start(this);

// 序列动作
ActionKit.Sequence()
    .Delay(1f, () => Debug.Log("第1秒"))
    .Callback(() => Debug.Log("立即执行"))
    .Delay(1f, () => Debug.Log("第2秒"))
    .Start(this);

// 并行动作
ActionKit.Parallel()
    .Delay(1f, () => Debug.Log("任务A完成"))
    .Delay(2f, () => Debug.Log("任务B完成"))
    .Start(this);

// 循环动作
ActionKit.Repeat(3).Delay(1f, () => Debug.Log("循环中...")).Start(this);

// Lerp 插值
ActionKit.Lerp(0f, 1f, 2f, v => transform.localScale = Vector3.one * v).Start(this);
```

<details>
<summary>📖 Lambda 嵌套写法</summary>

```csharp
ActionKit.Sequence()
    .Callback(() => Debug.Log("开始"))
    .Sequence(s => {
        s.Delay(1f, () => Debug.Log("延时1秒"));
        s.Callback(() => Debug.Log("回调"));
    })
    .Parallel(p => {
        p.Lerp(0f, 1f, 0.5f, v => canvasGroup.alpha = v);
        p.Delay(0.5f, () => { });
    })
    .Repeat(r => {
        r.DelayFrame(1, () => Debug.Log("每帧执行"));
    }, -1, () => isRunning)
    .Start(this);
```

</details>

## 🔄 状态机 (FsmKit)

简洁的有限状态机实现。

```csharp
public enum PlayerState { Idle, Walk, Run }

public class IdleState : AbstractState<PlayerState, PlayerController>
{
    public IdleState(IFSM<PlayerState> fsm, PlayerController target) : base(fsm, target) { }
    
    public override void Start() => Debug.Log("进入Idle状态");
    public override void Update()
    {
        if (Input.GetKey(KeyCode.W)) FSM.Change(PlayerState.Walk);
    }
    public override void End() => Debug.Log("离开Idle状态");
}

// 使用
private FSM<PlayerState> fsm = new();
void Start()
{
    fsm.Add(PlayerState.Idle, new IdleState(fsm, this));
    fsm.Start(PlayerState.Idle);
}
void Update() => fsm.Update();
```

## 🖼️ UI管理 (UIKit)

带热度管理的UI面板系统，提供编辑器快速创建面板、组件绑定和代码生成功能。

```csharp
// 打开/关闭面板
UIKit.OpenPanel<MainMenuPanel>();
UIKit.ClosePanel<MainMenuPanel>();

// 带数据打开
UIKit.OpenPanel<ShopPanel>(UILevel.Common, new ShopData { Gold = 100 });

// 异步打开
UIKit.OpenPanelAsync<LoadingPanel>(panel => Debug.Log("加载完成"));

// 栈式管理
UIKit.PushOpenPanel<SettingsPanel>();
UIKit.PopPanel();

// 获取面板
var panel = UIKit.GetPanel<MainMenuPanel>();
```

<details>
<summary>📖 编辑器功能</summary>

### 快速创建面板
通过菜单 `YokiFrame > UIKit > CreatePanel` 或快捷键 `Shift + U` 打开创建窗口。

### 组件绑定
在 Hierarchy 中选中 UI 子物体，通过 `GameObject > UIKit > Add Bind` 或 `Alt + B` 添加绑定。

绑定类型：
- `Member` - 成员变量
- `Element` - UIElement，独立元素类
- `Component` - UIComponent，跨面板复用
- `Leaf` - 叶子节点，不生成代码

### 代码生成
选中预制体，右键 `Assets > UIKit - Create UICode` 重新生成代码。

</details>

## 🔊 音频管理 (AudioKit)

高扩展性的音频管理系统，支持 Unity 原生音频和 FMOD 等第三方方案。

**特点**：策略模式后端扩展 | 零 MonoBehaviour | 对象池复用 | 5通道分离 | int 类型 AudioId

```csharp
// 播放音效
AudioKit.Play(AudioIds.CLICK, AudioChannel.UI);
AudioKit.Play(AudioIds.BGM_MAIN, AudioChannel.Bgm);

// 使用配置播放
var config = AudioPlayConfig.Default
    .WithChannel(AudioChannel.Bgm)
    .WithVolume(0.8f)
    .WithLoop(true)
    .WithFadeIn(1f);
var handle = AudioKit.Play(AudioIds.BGM_BATTLE, config);

// 控制播放
handle.Pause();
handle.Resume();
handle.Stop();
handle.StopWithFade(0.5f);
```

### 3D 音效

```csharp
AudioKit.Play3D(AudioIds.EXPLOSION, new Vector3(10, 0, 5));
AudioKit.Play3D(AudioIds.ENGINE, enemyTransform);  // 跟随目标

var config = AudioPlayConfig.Create3D(position, minDistance: 2f, maxDistance: 50f);
AudioKit.Play(AudioIds.FOOTSTEP, config);
```

### 通道与全局控制

```csharp
// 通道控制
AudioKit.SetChannelVolume(AudioChannel.Bgm, 0.5f);
AudioKit.MuteChannel(AudioChannel.Voice, true);
AudioKit.StopChannel(AudioChannel.Bgm);

// 全局控制
AudioKit.SetGlobalVolume(0.7f);
AudioKit.PauseAll();
AudioKit.ResumeAll();
AudioKit.MuteAll(true);
```

### 配置与更新

```csharp
// 路径解析器
AudioKit.SetPathResolver(id => AudioConfigTable.Get(id)?.Path);

// 配置
AudioKit.SetConfig(new AudioKitConfig { MaxConcurrentSounds = 32, BgmVolume = 0.8f });

// 更新驱动（需要手动调用）
void Update() => AudioKit.Update(Time.deltaTime);
```

<details>
<summary>📖 扩展 FMOD 后端</summary>

```csharp
// 切换到 FMOD 后端
AudioKit.SetBackend(new FmodAudioBackend());
AudioKit.SetPathResolver(id => $"event:/{AudioConfigTable.Get(id).FmodPath}");

// FMOD 后端实现
public sealed class FmodAudioBackend : IAudioBackend
{
    private readonly Dictionary<int, EventReference> mEventCache = new();
    private readonly List<FmodAudioHandle> mPlayingHandles = new();
    
    public void Initialize(AudioKitConfig config) { /* 初始化 FMOD Bus */ }
    
    public IAudioHandle Play(int audioId, string path, AudioPlayConfig config)
    {
        if (!mEventCache.TryGetValue(audioId, out var eventRef))
        {
            eventRef = RuntimeManager.PathToEventReference(path);
            mEventCache[audioId] = eventRef;
        }
        var instance = RuntimeManager.CreateInstance(eventRef);
        // 配置并播放...
        return handle;
    }
    
    // 实现其他接口方法...
}
```

</details>

## 💾 存档系统 (SaveKit)

完整的游戏存档解决方案，支持多槽位、加密、版本迁移。

```csharp
// 创建存档
var saveData = SaveKit.CreateSaveData();
saveData.SetModule(new PlayerData { Level = 10, Gold = 1000 });
saveData.SetModule(new InventoryData { ItemIds = new List<int> { 1, 2, 3 } });

// 保存/加载
SaveKit.Save(0, saveData);
var loadedData = SaveKit.Load(0);
var player = loadedData.GetModule<PlayerData>();
```

### 槽位管理

```csharp
if (SaveKit.Exists(0)) { /* 存档存在 */ }
var meta = SaveKit.GetMeta(0);  // 获取元数据
var allSlots = SaveKit.GetAllSlots();
SaveKit.Delete(0);
SaveKit.SetMaxSlots(5);
```

### 加密与自动保存

```csharp
// 加密
SaveKit.SetEncryptor(new AesSaveEncryptor("MySecretPassword"));

// 自动保存
SaveKit.EnableAutoSave(0, saveData, 60f, () => Debug.Log("即将保存"));
SaveKit.DisableAutoSave();
```

<details>
<summary>📖 版本迁移</summary>

当存档结构变化时，使用迁移器升级旧存档：

```csharp
public class PlayerMigratorV1ToV2 : IRawByteMigrator
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public byte[] MigrateBytes(int oldTypeKey, byte[] rawBytes, out int newTypeKey)
    {
        newTypeKey = oldTypeKey;
        if (oldTypeKey != typeof(PlayerData).GetHashCode()) return null;

        var json = Encoding.UTF8.GetString(rawBytes);
        var jObject = JObject.Parse(json);
        
        // 重命名字段
        if (jObject.ContainsKey("Gold"))
        {
            jObject["Coins"] = jObject["Gold"];
            jObject.Remove("Gold");
        }
        
        // 添加新字段
        if (!jObject.ContainsKey("Experience"))
            jObject["Experience"] = 0;
        
        return Encoding.UTF8.GetBytes(jObject.ToString());
    }

    public SaveData Migrate(SaveData oldData) => oldData;
}

// 注册迁移器
SaveKit.RegisterMigrator(new PlayerMigratorV1ToV2());
SaveKit.SetCurrentVersion(2);
```

</details>

<details>
<summary>📖 自定义序列化器与加密器</summary>

```csharp
// 自定义序列化器
public class BinarySaveSerializer : ISaveSerializer
{
    public byte[] Serialize<T>(T data) => YourSerializer.Serialize(data);
    public T Deserialize<T>(byte[] bytes) => YourSerializer.Deserialize<T>(bytes);
}
SaveKit.SetSerializer(new BinarySaveSerializer());

// 自定义加密器
public class XorSaveEncryptor : ISaveEncryptor
{
    private readonly byte mKey;
    public XorSaveEncryptor(byte key = 0xAB) => mKey = key;
    
    public byte[] Encrypt(byte[] data)
    {
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
            result[i] = (byte)(data[i] ^ mKey);
        return result;
    }
    
    public byte[] Decrypt(byte[] data) => Encrypt(data);
}
SaveKit.SetEncryptor(new XorSaveEncryptor());
```

</details>

<details>
<summary>📖 Architecture 集成</summary>

```csharp
// 从 Architecture 收集所有 Model 数据
var saveData = SaveKit.CreateSaveData();
SaveKit.CollectFromArchitecture<GameArchitecture>(saveData);
SaveKit.Save(0, saveData);

// 加载并应用到 Architecture
var loadedData = SaveKit.Load(0);
SaveKit.ApplyToArchitecture<GameArchitecture>(loadedData);
```

</details>

---

## 📄 License

MIT License - 详见 [LICENSE](LICENSE) 文件
