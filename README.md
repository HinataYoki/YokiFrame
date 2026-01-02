# YokiFrame

一个专为Unity开发设计的轻量级、模块化框架，提供完整的架构系统、常用工具包和UI管理解决方案。

## 📋 目录

- [框架介绍](#框架介绍)
- [框架层级结构](#框架层级结构)
- [核心模块](#核心模块)
- [常用工具使用指南](#常用工具使用指南)
- [快速开始](#快速开始)

## 🎯 框架介绍

YokiFrame是一个面向Unity开发的框架，采用模块化设计，提供了从底层架构到上层UI管理的完整解决方案。框架支持Unity 2021.3及以上版本。

### 主要特性

- ✅ **模块化设计**：核心模块与工具模块分离，按需使用
- ✅ **服务架构**：基于IoC的服务注册与管理
- ✅ **对象池系统**：高效的对象复用机制
- ✅ **事件系统**：类型安全的事件通信
- ✅ **UI框架**：完整的UI面板管理与生命周期控制
- ✅ **动作系统**：链式动作执行，支持顺序、并行、延迟等
- ✅ **状态机**：灵活的状态管理方案
- ✅ **代码生成**：自动化UI代码生成工具

## 🏗️ 框架层级结构

```
YokiFrame/
├── Core/                          # 核心模块
│   ├── Architecture/              # 架构系统
│   │   └── Architecture.cs        # IArchitecture, IService, IModel 接口定义
│   └── Kit/                       # 工具包集合
│       ├── EventKit/              # 事件系统
│       ├── PoolKit/               # 对象池系统
│       ├── SingletonKit/         # 单例管理
│       ├── LogKit/                # 日志系统
│       ├── CodeGenKit/            # 代码生成工具
│       ├── FluentApi/             # 扩展方法集合
│       └── ToolClass/             # 工具类
│
└── Tools/                         # 工具模块
    ├── ActionKit/                 # 动作系统
    ├── FsmKit/                    # 状态机系统
    └── UIKit/                     # UI框架
        ├── Scripts/               # 运行时脚本
        └── Editor/                # 编辑器工具
```

### 程序集定义

框架采用程序集分离设计，便于模块化管理：

- `YokiFrame.asmdef` - 核心框架
- `YokiFrame.ActionKit.asmdef` - 动作系统模块
- `YokiFrame.UIKit.asmdef` - UI框架模块
- `YokiFrame.UIKit.Editor.asmdef` - UI编辑器工具

## 📦 核心模块

### 1. Architecture（架构系统）

提供基于IoC的服务注册与管理机制。

**核心接口：**
- `IArchitecture` - 架构接口，提供服务注册与获取
- `IService` - 服务接口，所有服务需实现此接口
- `IModel` - 数据模型接口，继承自IService

**使用示例：**

```csharp
// 定义架构
public class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void OnInit()
    {
        // 注册服务
        Register(new PlayerModel());
        Register(new GameService());
    }
}

// 定义服务
public class PlayerModel : AbstractModel
{
    protected override void OnInit()
    {
        // 服务初始化
    }
    
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        // 序列化实现
    }
}

// 使用服务
var playerModel = GameArchitecture.Interface.GetService<PlayerModel>();
```

### 2. EventKit（事件系统）

提供类型安全的事件通信机制，支持类型事件、枚举事件和字符串事件。

**使用示例：**

```csharp
// 类型事件（推荐）
EventKit.Type.Register<PlayerData>(OnPlayerDataChanged);
EventKit.Type.Send(new PlayerData { Level = 10 });
EventKit.Type.UnRegister<PlayerData>(OnPlayerDataChanged);

// 枚举事件
public enum GameEvent
{
    PlayerLevelUp,
    GameOver
}

EventKit.Enum.Register(GameEvent.PlayerLevelUp, OnLevelUp);
EventKit.Enum.Send(GameEvent.PlayerLevelUp);
```

### 3. PoolKit（对象池系统）

提供高效的对象复用机制，减少GC压力。

**使用示例：**

```csharp
// 创建对象池
var pool = new SimplePoolKit<Bullet>(
    factoryMethod: () => new Bullet(),
    resetMethod: bullet => bullet.Reset(),
    initCount: 10
);

// 从池中获取对象
var bullet = pool.Allocate();

// 回收对象
pool.Recycle(bullet);

// 使用全局对象池（如果已注册）
var list = Pool.List<int>(list => {
    list.Add(1);
    list.Add(2);
    // 使用完毕后自动回收
});
```

### 4. SingletonKit（单例管理）

提供线程安全的单例管理，支持MonoBehaviour和普通C#类。

**使用示例：**

```csharp
// 普通类单例
public class GameManager : ISingleton
{
    public static GameManager Instance => SingletonKit<GameManager>.Instance;
    
    public void OnSingletonInit()
    {
        // 单例初始化
    }
}

// MonoBehaviour单例
[MonoSingletonPath("YokiFrame/GameManager")]
public class AudioManager : MonoSingleton<AudioManager>
{
    public override void OnSingletonInit()
    {
        // 单例初始化
    }
}
```

### 5. LogKit（日志系统）

提供功能完善的日志系统，支持文件写入、加密、日志过滤等。

**使用示例：**

```csharp
// 基本日志
KitLogger.Log("普通日志");
KitLogger.Warning("警告日志");
KitLogger.Error("错误日志");
KitLogger.Exception(exception);

// 配置日志级别
KitLogger.Level = KitLogger.LogLevel.All; // All, Warning, Error, None

// 启用文件写入（编辑器）
KitLogger.SaveLogInEditor = true;

// 自动启用文件写入（运行时）
KitLogger.AutoEnableWriteLogToFile = true;
```

### 6. Bindable（数据绑定）

提供数据绑定机制，支持值变化监听。

**使用示例：**

```csharp
// 创建绑定值
var playerLevel = new BindValue<int>(1);

// 绑定值变化监听
var unregister = playerLevel.Bind(level => {
    Debug.Log($"玩家等级变化: {level}");
});

// 修改值（会自动触发回调）
playerLevel.Value = 10;

// 取消绑定
unregister.UnRegister();

// 静默修改（不触发回调）
playerLevel.SetValueWithoutEvent(20);
```

## 🛠️ 常用工具使用指南

### ActionKit（动作系统）

ActionKit提供了链式动作执行系统，支持顺序执行、并行执行、延迟、插值等。

#### Sequence（顺序执行）

```csharp
// 创建顺序动作链
ActionKit.Sequence()
    .Append(ActionKit.Delay(1f, () => Debug.Log("1秒后执行")))
    .Append(ActionKit.Callback(() => Debug.Log("执行回调")))
    .Append(ActionKit.Delay(2f, () => Debug.Log("再等2秒")))
    .Start(this); // this 是 MonoBehaviour

// 链式调用
ActionKit.Sequence()
    .Delay(1f)
    .Callback(() => Debug.Log("延迟后执行"))
    .Delay(2f)
    .Callback(() => Debug.Log("完成"))
    .Start(this);
```

#### Parallel（并行执行）

```csharp
// 并行执行多个动作
ActionKit.Parallel(waitAll: true)
    .Append(ActionKit.Delay(1f, () => Debug.Log("动作1完成")))
    .Append(ActionKit.Delay(2f, () => Debug.Log("动作2完成")))
    .Append(ActionKit.Delay(3f, () => Debug.Log("动作3完成")))
    .Start(this); // 等待所有动作完成

// 嵌套使用
ActionKit.Sequence()
    .Delay(1f)
    .Parallel(waitAll: true, parallel => {
        parallel.Delay(1f);
        parallel.Delay(2f);
    })
    .Callback(() => Debug.Log("并行任务完成"))
    .Start(this);
```

#### 常用动作类型

```csharp
// 延迟
ActionKit.Delay(2f, () => Debug.Log("延迟2秒"));

// 延迟帧
ActionKit.DelayFrame(5, () => Debug.Log("5帧后执行"));
ActionKit.NextFrame(() => Debug.Log("下一帧执行"));

// 插值
ActionKit.Lerp(0f, 100f, 2f, 
    value => transform.position = new Vector3(value, 0, 0),
    () => Debug.Log("插值完成")
);

// 重复执行
ActionKit.Repeat(5, () => {
    Debug.Log("重复执行");
    return false; // 返回true时提前结束
}).Start(this);

// 协程支持
ActionKit.Coroutine(() => MyCoroutine()).Start(this);

// Task支持
ActionKit.Task(async () => {
    await Task.Delay(1000);
    Debug.Log("Task完成");
}).Start(this);
```

### FsmKit（状态机）

提供灵活的状态管理方案，支持基于枚举的状态机。

**使用示例：**

```csharp
// 定义状态枚举
public enum PlayerState
{
    Idle,
    Walk,
    Run,
    Jump
}

// 定义状态类
public class IdleState : AbstractState
{
    public override void Start()
    {
        Debug.Log("进入待机状态");
    }
    
    public override void Update()
    {
        // 状态更新逻辑
    }
    
    public override void End()
    {
        Debug.Log("退出待机状态");
    }
}

// 创建状态机
var fsm = new FSM<PlayerState>();
fsm.Add(PlayerState.Idle, new IdleState());
fsm.Add(PlayerState.Walk, new WalkState());

// 启动状态机
fsm.Start(PlayerState.Idle);

// 切换状态
fsm.Change(PlayerState.Walk);

// 更新状态机（在Update中调用）
fsm.Update();
```

### UIKit（UI框架）

提供完整的UI面板管理系统，支持面板生命周期、栈管理、热度管理等。

#### 创建UI面板

```csharp
// 定义UI面板
public class MainPanel : UIPanel
{
    protected override void OnInit(IUIData data)
    {
        // 面板初始化
    }
    
    protected override void OnOpen()
    {
        // 面板打开时调用
    }
    
    protected override void OnShow()
    {
        // 面板显示时调用
    }
    
    protected override void OnHide()
    {
        // 面板隐藏时调用
    }
    
    protected override void OnClose()
    {
        // 面板关闭时调用
    }
}
```

#### 使用UIKit

```csharp
// 打开面板
var panel = UIKit.OpenPanel<MainPanel>(UILevel.Common);

// 异步打开面板
UIKit.OpenPanelAsync<MainPanel>(panel => {
    Debug.Log("面板加载完成");
}, UILevel.Common);

// 获取已存在的面板
var mainPanel = UIKit.GetPanel<MainPanel>();

// 显示/隐藏面板
UIKit.ShowPanel<MainPanel>();
UIKit.HidePanel<MainPanel>();

// 关闭面板
UIKit.ClosePanel<MainPanel>();

// 面板栈管理
UIKit.PushOpenPanel<MainPanel>(UILevel.Common); // 打开并压栈
UIKit.PopPanel(); // 弹出栈顶面板
```

#### UI层级

UIKit支持以下UI层级（从低到高）：

- `UILevel.AlwayBottom` - 始终在底部
- `UILevel.Bg` - 背景层
- `UILevel.Common` - 普通层
- `UILevel.Pop` - 弹窗层
- `UILevel.AlwayTop` - 始终在顶部
- `UILevel.CanvasPanel` - Canvas面板层

#### 面板状态

面板有三种状态：
- `PanelState.Open` - 已打开
- `PanelState.Hide` - 已隐藏
- `PanelState.Close` - 已关闭

## 🚀 快速开始

### 1. 安装框架

将YokiFrame文件夹放入Unity项目的Assets目录下。

### 2. 初始化架构

```csharp
public class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void OnInit()
    {
        // 注册你的服务
        Register(new PlayerModel());
    }
}

// 在游戏启动时初始化
void Start()
{
    var arch = GameArchitecture.Interface;
}
```

### 3. 使用事件系统

```csharp
// 注册事件
EventKit.Type.Register<PlayerData>(OnPlayerDataChanged);

// 发送事件
EventKit.Type.Send(new PlayerData { Level = 10 });
```

### 4. 创建UI面板

```csharp
// 继承UIPanel创建面板
public class MainPanel : UIPanel
{
    protected override void OnInit(IUIData data)
    {
        // 初始化UI
    }
}

// 打开面板
UIKit.OpenPanel<MainPanel>();
```

### 5. 使用动作系统

```csharp
// 创建动作链
ActionKit.Sequence()
    .Delay(1f)
    .Callback(() => Debug.Log("延迟完成"))
    .Start(this);
```

## 📝 注意事项

1. **程序集引用**：确保在使用模块前正确配置程序集引用
2. **生命周期管理**：注意及时释放事件监听和对象池对象
3. **UI面板管理**：使用UIKit管理UI面板，避免直接使用GameObject
4. **日志系统**：生产环境建议设置合适的日志级别

## 📄 许可证

详见 [LICENSE](LICENSE) 文件

## 👤 作者

**HinataYoki**

- GitHub: [@HinataYoki](https://github.com/HinataYoki/YokiFrame)

## 🙏 致谢

感谢所有为YokiFrame做出贡献的开发者！

---

**版本**: 1.0.5  
**Unity版本**: 2021.3+
