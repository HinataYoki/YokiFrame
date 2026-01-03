# YokiFrame

一个轻量级的 Unity 开发框架，提供架构设计、事件系统、动作序列、状态机、UI管理等常用功能模块。

## 📦 安装

通过 Unity Package Manager 安装：
1. 打开 `Window > Package Manager`
2. 点击 `+` > `Add package from git URL`
3. 输入：`https://github.com/HinataYoki/YokiFrame.git`

## 🏗️ 核心架构 (Architecture)

基于服务定位器模式的轻量级架构，支持服务注册与获取。

```csharp
// 1. 定义你的架构
public class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void OnInit()
    {
        // 注册服务
        Register(new PlayerService());
        Register(new AudioService());
    }
}

// 2. 定义服务
public class PlayerService : AbstractService
{
    public int Health { get; set; } = 100;
    
    protected override void OnInit()
    {
        // 服务初始化逻辑
    }
}

// 3. 使用服务
var playerService = GameArchitecture.Interface.GetService<PlayerService>();
playerService.Health -= 10;
```

## 🎬 动作系统 (ActionKit)

链式调用的动作序列系统，支持延时、回调、并行、循环等。

```csharp
// 延时执行
ActionKit.Delay(2f, () => Debug.Log("2秒后执行"))
    .Start(this);

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
ActionKit.Repeat(3)  // 重复3次，-1为无限循环
    .Delay(1f, () => Debug.Log("循环中..."))
    .Start(this);

// Lerp 插值
ActionKit.Lerp(0f, 1f, 2f, value => 
{
    transform.localScale = Vector3.one * value;
}).Start(this);

// 下一帧执行
ActionKit.NextFrame(() => Debug.Log("下一帧执行")).Start(this);

// 协程支持
ActionKit.Coroutine(() => MyCoroutine()).Start(this);

// 异步Task支持
ActionKit.Task(async () => await SomeAsyncMethod()).Start(this);
```

### Lambda 嵌套写法

使用 Lambda 嵌套可以让复杂动作的层级结构更加清晰：

```csharp
// 嵌套写法示例
ActionKit.Sequence()
    .Repeat(r => 
    {
        r.Parallel(p => 
        {
            p.Callback(() => Debug.Log("并行A"));
            p.Callback(() => Debug.Log("并行B"));
        });
    }, 3)
    .Start(this);

// 复杂嵌套示例
ActionKit.Sequence()
    .Callback(() => Debug.Log("开始"))
    .Sequence(s => 
    {
        s.Delay(1f, () => Debug.Log("延时1秒"));
        s.Callback(() => Debug.Log("回调"));
    })
    .Parallel(p => 
    {
        p.Lerp(0f, 1f, 0.5f, v => canvasGroup.alpha = v);
        p.Delay(0.5f, () => { });
    })
    .Repeat(r => 
    {
        r.DelayFrame(1, () => Debug.Log("每帧执行"));
    }, -1, () => isRunning)  // 条件循环
    .Start(this);
```

## 📡 事件系统 (EventKit)

类型安全的全局事件系统，支持 TypeEvent 和 EnumEvent 两种模式。

### TypeEvent - 基于类型的事件

```csharp
// 定义事件
public struct PlayerDiedEvent
{
    public string PlayerName;
}

// 注册事件
EventKit.Type.Register<PlayerDiedEvent>(e => 
{
    Debug.Log($"{e.PlayerName} 死亡了");
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 发送事件
EventKit.Type.Send(new PlayerDiedEvent { PlayerName = "Player1" });

// 手动注销
EventKit.Type.UnRegister<PlayerDiedEvent>(OnPlayerDied);
```

### EnumEvent - 基于枚举的事件

适合用枚举定义游戏事件类型的场景，更轻量灵活。

```csharp
// 定义事件枚举
public enum GameEvent { GameStart, GamePause, GameOver, ScoreChanged }

// 注册无参事件
EventKit.Enum.Register(GameEvent.GameStart, () => 
{
    Debug.Log("游戏开始");
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 注册有参事件
EventKit.Enum.Register<GameEvent, int>(GameEvent.ScoreChanged, score => 
{
    Debug.Log($"分数变化: {score}");
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 注册可变参数事件
EventKit.Enum.Register(GameEvent.GameOver, args => 
{
    var winner = args[0] as string;
    var score = (int)args[1];
    Debug.Log($"游戏结束，胜者: {winner}, 分数: {score}");
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 发送无参事件
EventKit.Enum.Send(GameEvent.GameStart);

// 发送有参事件
EventKit.Enum.Send(GameEvent.ScoreChanged, 100);

// 发送可变参数事件
EventKit.Enum.Send(GameEvent.GameOver, "Player1", 9999);

// 注销指定枚举的所有事件
EventKit.Enum.UnRegister(GameEvent.GameStart);
```

## 🔄 状态机 (FsmKit)

简洁的有限状态机实现。

```csharp
// 定义状态枚举
public enum PlayerState { Idle, Walk, Run, Jump }

// 定义状态类
public class IdleState : AbstractState<PlayerState, PlayerController>
{
    public IdleState(IFSM<PlayerState> fsm, PlayerController target) : base(fsm, target) { }
    
    public override void Start() => Debug.Log("进入Idle状态");
    public override void Update()
    {
        if (Input.GetKey(KeyCode.W))
            FSM.Change(PlayerState.Walk);
    }
    public override void End() => Debug.Log("离开Idle状态");
}

// 使用状态机
public class PlayerController : MonoBehaviour
{
    private FSM<PlayerState> fsm = new();
    
    void Start()
    {
        fsm.Add(PlayerState.Idle, new IdleState(fsm, this));
        fsm.Add(PlayerState.Walk, new WalkState(fsm, this));
        fsm.Start(PlayerState.Idle);
    }
    
    void Update() => fsm.Update();
}
```

## 🖼️ UI管理 (UIKit)

带热度管理的UI面板系统，提供编辑器快速创建面板、组件绑定和代码生成功能。

### 基础用法

```csharp
// 打开面板
UIKit.OpenPanel<MainMenuPanel>();

// 带数据打开
UIKit.OpenPanel<ShopPanel>(UILevel.Common, new ShopData { Gold = 100 });

// 异步打开
UIKit.OpenPanelAsync<LoadingPanel>(panel => 
{
    Debug.Log("面板加载完成");
});

// 关闭面板
UIKit.ClosePanel<MainMenuPanel>();

// 栈式管理（适合多级菜单）
UIKit.PushOpenPanel<SettingsPanel>();  // 打开并压栈
UIKit.PopPanel();  // 弹出并关闭

// 获取已打开的面板
var panel = UIKit.GetPanel<MainMenuPanel>();
```

### 编辑器功能

#### 1. 快速创建 UI 面板

通过菜单 `YokiFrame > UIKit > CreatePanel` 或快捷键 `Shift + U` 打开创建窗口：

- 设置 UI 脚本所在的程序集名称
- 设置脚本命名空间
- 选择脚本和预制体的生成目录
- 输入面板名称后点击创建

创建后自动生成：
- `{PanelName}.prefab` - UI预制体
- `{PanelName}.cs` - 面板逻辑代码（可编辑）
- `{PanelName}.Designer.cs` - 自动生成的成员定义（勿手动修改）

#### 2. 组件绑定 (Bind)

在 Hierarchy 中选中 UI 子物体，通过菜单 `GameObject > UIKit > Add Bind` 或快捷键 `Alt + B` 添加绑定组件。

绑定类型说明：
- `Member` - 绑定为成员变量，可选择挂载的组件类型（Button、Image、Text等）
- `Element` - 绑定为 UIElement，会生成独立的元素类，适合复用的UI模块
- `Component` - 绑定为 UIComponent，跨面板复用的组件
- `Leaf` - 叶子节点，不生成代码，仅作为层级标记

Inspector 面板中可设置：
- 字段名称 - 生成代码中的变量名
- 类名称 - Element/Component 的类名
- 组件列表 - Member 类型可选择绑定的组件
- 注释 - 生成代码中的注释说明

#### 3. 代码生成

在 Project 窗口选中 UI 预制体，右键菜单选择 `Assets > UIKit - Create UICode` 重新生成代码。

生成的面板代码结构：
```csharp
// MainMenuPanel.cs - 可编辑的逻辑代码
public partial class MainMenuPanel : UIPanel
{
    protected override void OnInit(IUIData uiData = null)
    {
        mData = uiData as MainMenuPanelData ?? new MainMenuPanelData();
        // 初始化逻辑
        BtnStart.onClick.AddListener(OnStartClick);
    }
    
    protected override void OnOpen(IUIData uiData = null) { }
    protected override void OnShow() { }
    protected override void OnHide() { }
    protected override void OnClose() { }
    
    private void OnStartClick() => UIKit.OpenPanel<GamePanel>();
}

// MainMenuPanel.Designer.cs - 自动生成，勿手动修改
public partial class MainMenuPanel
{
    /// <summary>
    /// 开始按钮
    /// </summary>
    [SerializeField]
    public Button BtnStart;
    
    [SerializeField]
    public Text TxtTitle;
    
    // ...
}
```

## 🔧 单例工具 (SingletonKit)

支持普通类和 MonoBehaviour 的单例模式。

```csharp
// 普通单例
public class GameManager : ISingleton
{
    public static GameManager Instance => SingletonKit<GameManager>.Instance;
    
    public void OnSingletonInit()
    {
        Debug.Log("GameManager 初始化");
    }
}

// Mono单例
[MonoSingletonPath("Managers/AudioManager")]
public class AudioManager : MonoBehaviour, ISingleton
{
    public static AudioManager Instance => SingletonKit<AudioManager>.Instance;
    
    public void OnSingletonInit()
    {
        DontDestroyOnLoad(gameObject);
    }
}
```

## 📝 日志系统 (KitLogger)

支持加密和文件写入的日志系统。

```csharp
// 基础日志
KitLogger.Log("普通日志");
KitLogger.Warning("警告日志");
KitLogger.Error("错误日志");

// 配置日志级别
KitLogger.Level = KitLogger.LogLevel.Warning;  // 只显示Warning及以上

// 启用文件写入
KitLogger.AutoEnableWriteLogToFile = true;
```

## 📦 资源管理 (ResKit)

统一的资源加载接口，默认使用 Resources，支持扩展 YooAsset 等第三方加载方案。

### 基础用法

```csharp
// 同步加载
var prefab = ResKit.Load<GameObject>("Prefabs/Player");
var sprite = ResKit.Load<Sprite>("Sprites/Icon");

// 异步加载
ResKit.LoadAsync<GameObject>("Prefabs/Enemy", prefab => 
{
    Instantiate(prefab);
});

// 实例化预制体
var player = ResKit.Instantiate("Prefabs/Player");

// 异步实例化
ResKit.InstantiateAsync("Prefabs/Enemy", instance => 
{
    instance.transform.position = spawnPoint;
});

// 使用句柄管理引用计数
var handler = ResKit.LoadAsset<GameObject>("Prefabs/Player");
// 使用资源...
handler.Release();  // 引用计数减少，归零时自动卸载

// 清理所有缓存
ResKit.ClearAll();
```

### 扩展机制

ResKit 提供了统一的加载器接口，可以轻松扩展支持 YooAsset、Addressables 等第三方资源管理方案。

核心接口：
- `IResLoader` - 资源加载器接口，负责具体的加载/卸载逻辑
- `IResLoaderPool` - 加载器池接口，负责加载器的分配和回收
- `AbstractResLoaderPool` - 抽象加载池基类，提供池化复用逻辑

设置自定义加载池后，ResKit 和 UIKit 都会自动使用新的加载方案：

```csharp
// 一行代码切换加载方案，全局生效
ResKit.SetLoaderPool(new YooAssetResLoaderPool());

// 之后所有加载都走 YooAsset
ResKit.Load<GameObject>("Player");      // 使用 YooAsset
UIKit.OpenPanel<MainMenuPanel>();       // 也使用 YooAsset
```

### 扩展 YooAsset 完整示例

```csharp
using System;
using UnityEngine;
using YooAsset;
using YokiFrame;

/// <summary>
/// YooAsset 扩展
/// </summary>
public static class ResKitWithYooAsset
{
    /// <summary>
    /// 初始化并设置 YooAsset 为默认加载器
    /// </summary>
    public static void Init()
    {
        ResKit.SetLoaderPool(new YooAssetResLoaderPool());
    }

    /// <summary>
    /// YooAsset 加载池
    /// </summary>
    public class YooAssetResLoaderPool : AbstractResLoaderPool
    {
        protected override IResLoader CreateLoader() => new YooAssetResLoader(this);
    }

    /// <summary>
    /// YooAsset 加载器
    /// </summary>
    public class YooAssetResLoader : IResLoader
    {
        private readonly IResLoaderPool mPool;
        private AssetHandle mHandle;

        public YooAssetResLoader(IResLoaderPool pool) => mPool = pool;

        public T Load<T>(string path) where T : UnityEngine.Object
        {
            if (mHandle != null && mHandle.IsDone)
            {
                return mHandle.AssetObject as T;
            }
            mHandle = YooAssets.LoadAssetSync<T>(path);
            return mHandle.AssetObject as T;
        }

        public void LoadAsync<T>(string path, Action<T> onComplete) where T : UnityEngine.Object
        {
            if (mHandle != null && mHandle.IsDone)
            {
                onComplete?.Invoke(mHandle.AssetObject as T);
                return;
            }
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
}
```

使用方式：

```csharp
// 游戏启动时初始化
public class GameLauncher : MonoBehaviour
{
    async void Start()
    {
        // 1. 初始化 YooAsset
        YooAssets.Initialize();
        var package = YooAssets.CreatePackage("DefaultPackage");
        YooAssets.SetDefaultPackage(package);
        // ... YooAsset 初始化流程
        
        // 2. 设置 ResKit 使用 YooAsset
        ResKitWithYooAsset.Init();
        
        // 3. 正常使用，全部走 YooAsset
        var player = ResKit.Load<GameObject>("Player");
        UIKit.OpenPanel<MainMenuPanel>();
    }
}
```

## 🏊 对象池 (PoolKit)

高效的对象池管理。


```csharp
// 使用临时List（自动回收）
Pool.List<int>(list => 
{
    list.Add(1);
    list.Add(2);
    // 使用完自动回收
});

// 使用临时Dictionary
Pool.Dictionary<string, int>(dict => 
{
    dict["key"] = 100;
});

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

## 🔗 数据绑定 (Bindable)

响应式数据绑定。

```csharp
public class PlayerModel
{
    public BindValue<int> Health = new(100);
    public BindValue<string> Name = new("Player");
}

// 绑定数据变化
var model = new PlayerModel();
model.Health.Bind(value => 
{
    healthText.text = $"HP: {value}";
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 修改数据会自动触发回调
model.Health.Value -= 10;

// 绑定并立即执行一次
model.Health.BindWithCallback(value => UpdateUI(value));

// 设置值但不触发事件
model.Health.SetValueWithoutEvent(50);
```

对于值类型（int、float、bool 等），BindValue 可以直接判断值是否变化。对于引用类型或复杂类型，需要设置全局比较函数（同一类型共享）：

```csharp
// 引用类型需要设置全局比较函数（静态方法，同类型全局生效）
public class ItemData
{
    public int Id;
    public string Name;
}

// 在初始化时设置一次即可，所有 BindValue<ItemData> 共享此比较函数
BindValue<ItemData>.SetCompareFunc((a, b) => 
{
    if (a == null && b == null) return true;
    if (a == null || b == null) return false;
    return a.Id == b.Id && a.Name == b.Name;
});

// List 类型示例
BindValue<List<int>>.SetCompareFunc((a, b) => 
{
    if (a == null && b == null) return true;
    if (a == null || b == null) return false;
    return a.SequenceEqual(b);
});
```

## 🛠️ 扩展方法 (FluentApi)

便捷的链式扩展方法。

```csharp
// Transform 扩展
transform.ResetTransform();  // 重置位置、旋转、缩放
var pos2d = transform.Position2D();  // 获取2D位置

// 查找子物体组件
var button = gameObject.FindComponent<Button>("BtnStart");

// GameObject 扩展
gameObject.Parent(parentTransform);  // 设置父物体
```

## 📄 License

MIT License - 详见 [LICENSE](LICENSE) 文件
