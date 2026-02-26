# YokiFrame

<p align="center">
  <img src="Core/Editor/UISystem/Resources/yoki.png" alt="YokiFrame Logo" width="128" height="128">
</p>

<p align="center">
  <b>一个轻量级的 Unity 开发框架</b><br>
  提供架构设计、事件系统、动作序列、状态机、UI管理、音频管理、存档系统等常用功能模块。
</p>

---

## 📋 目录

- [安装](#-安装)
- [快速开始](#-快速开始)
- [功能导航](#-功能导航-ai-专用)
- [核心模块](#-核心模块-core)
- [工具模块](#-工具模块-tools)
- [编辑器工具](#-编辑器工具)
- [文档索引](#-文档索引)
- [License](#-license)

---

## 📦 安装

通过 Unity Package Manager 安装：
1. 打开 `Window > Package Manager`
2. 点击 `+` > `Add package from git URL`
3. 输入：`https://github.com/HinataYoki/YokiFrame.git`

---

## ⚡ 快速开始

### 最小示例

```csharp
using YokiFrame;

// 事件系统 - 类型安全的事件通信
EventKit.Type.Register<PlayerDiedEvent>(e => Debug.Log($"{e.PlayerName} 死亡"))
    .UnRegisterWhenGameObjectDestroyed(gameObject);
EventKit.Type.Send(new PlayerDiedEvent { PlayerName = "Player1" });

// 动作序列 - 链式时间轴
ActionKit.Sequence()
    .Delay(1f, () => Debug.Log("1秒后"))
    .Callback(() => Debug.Log("立即执行"))
    .Start(this);

// UI 管理 - 现代化面板系统
UIKit.OpenPanel<MainMenuPanel>();
UIKit.ClosePanel<MainMenuPanel>();

// 音频播放 - 统一音频接口
AudioKit.Play("Audio/BGM/MainTheme", AudioChannel.Bgm);
AudioKit.Play("Audio/SFX/Click");
```

### 进阶用法

查看 [完整代码示例](#完整代码示例) 或按 `Ctrl+E` 打开编辑器工具面板查看文档。

---

## 🧭 功能导航

### 按场景查找功能

| 用户需求关键词 | 推荐模块 | 核心功能 | 代码示例 | 源码位置 |
|--------------|---------|---------|---------|---------|
| 事件/通信/解耦/消息 | EventKit | 类型安全事件系统 | [示例](#事件系统-eventkit) | [Core/Runtime/EventKit](Core/Runtime/EventKit) |
| 对象池/性能/GC/复用 | PoolKit | GameObject/C# 对象池 | [示例](#对象池-poolkit) | [Core/Runtime/PoolKit](Core/Runtime/PoolKit) |
| 状态机/状态/AI/行为 | FsmKit | 有限状态机 | [示例](#状态机-fsmkit) | [Core/Runtime/FsmKit](Core/Runtime/FsmKit) |
| 单例/全局/管理器 | SingletonKit | 单例模式基类 | [示例](#单例-singletonkit) | [Core/Runtime/SingletonKit](Core/Runtime/SingletonKit) |
| 资源/加载/AssetBundle | ResKit | 统一资源加载 | [示例](#资源管理-reskit) | [Core/Runtime/ResKit](Core/Runtime/ResKit) |
| UI/界面/面板/窗口 | UIKit | UI 面板管理系统 | [示例](#ui-管理-uikit) | [Tools/UIKit](Tools/UIKit) |
| 音频/音效/BGM/声音 | AudioKit | 音频管理（FMOD） | [示例](#音频管理-audiokit) | [Tools/AudioKit](Tools/AudioKit) |
| 动画/时间轴/延时/序列 | ActionKit | 链式动作序列 | [示例](#动作序列-actionkit) | [Tools/ActionKit](Tools/ActionKit) |
| 配置表/Excel/数据表 | TableKit | Luban 配置表工具 | [示例](#配置表-tablekit) | [Tools/TableKit](Tools/TableKit) |
| 存档/保存/持久化 | SaveKit | 多槽位存档系统 | [示例](#存档系统-savekit) | [Tools/SaveKit](Tools/SaveKit) |
| 场景/切换/加载 | SceneKit | 异步场景管理 | [示例](#场景管理-scenekit) | [Tools/SceneKit](Tools/SceneKit) |
| 多语言/本地化/翻译 | LocalizationKit | 本地化系统 | [示例](#本地化-localizationkit) | [Tools/LocalizationKit](Tools/LocalizationKit) |
| Buff/增益/减益/属性 | BuffKit | Buff/Debuff 系统 | [示例](#buff-系统-buffkit) | [Tools/BuffKit](Tools/BuffKit) |
| 空间查询/范围/最近邻 | SpatialKit | 空间分区查询 | [示例](#空间查询-spatialkit) | [Tools/SpatialKit](Tools/SpatialKit) |

---

## 🧩 核心模块 (Core)

> **依赖规则**：核心模块间可相互依赖，工具模块仅可依赖核心模块

| 模块 | 功能 | 使用场景 | 关键特性 |
|------|------|---------|---------|
| **Architecture** | 轻量级服务架构 | 依赖注入、模块化管理 | IOC 容器、MVC/MVVM 基类 |
| **EventKit** | 类型安全事件系统 | 模块解耦、消息通信 | 类型/枚举/字符串事件、自动注销 |
| **SingletonKit** | 单例模式支持 | 全局管理器、服务访问 | MonoBehaviour/C# 单例 |
| **PoolKit** | 对象池管理 | 性能优化、避免 GC | GameObject 池、C# 对象池、IPoolable |
| **ResKit** | 统一资源加载 | 资源管理、YooAsset 扩展 | 同步/异步加载、多后端支持 |
| **FsmKit** | 有限状态机 | 状态管理、AI 行为 | 状态转换、生命周期回调 |
| **LogKit** | 日志系统 | 调试、加密日志、运行时显示 | 分级日志、条件编译、自定义输出 |
| **FluentApi** | 链式扩展方法 | 提高代码可读性 | GameObject/Transform/Component 扩展 |
| **ToolClass** | 工具类集合 | 通用工具方法 | 数学、字符串、集合、文件操作 |
| **CodeGenKit** | 代码生成工具 | 编辑器代码生成 | 类/方法/属性生成、代码写入器 |

---

## 🛠️ 工具模块 (Tools)

> **依赖规则**：工具模块间禁止相互依赖，仅可依赖核心模块

| 模块 | 功能 | 使用场景 | 关键特性 |
|------|------|---------|---------|
| **ActionKit** | 链式动作序列 | 时间轴动画、延时回调 | Sequence/Parallel、Delay/Callback、Repeat |
| **UIKit** | 现代化 UI 系统 | 面板管理、动画、导航 | 生命周期、堆栈导航、手柄支持、数据绑定 |
| **AudioKit** | 音频管理 | 音效、BGM、FMOD 支持 | 多通道、音量控制、3D 音效 |
| **SaveKit** | 存档系统 | 多槽位、加密、版本迁移 | 模块化存档、自动序列化、加密支持 |
| **TableKit** | 配置表工具 | Luban 集成、代码生成 | Excel/CSV 导入、自动代码生成、LINQ 查询 |
| **BuffKit** | Buff 系统 | Buff/Debuff、属性修改 | 容器管理、堆叠模式、标签系统 |
| **LocalizationKit** | 本地化系统 | 多语言、参数化文本 | 动态切换、格式化文本、缺失检测 |
| **SceneKit** | 场景管理 | 异步加载、过渡效果 | 进度回调、预加载、过渡动画 |
| **InputKit** | 输入系统 | 统一输入接口 | 键鼠/手柄/触摸、输入映射 |
| **SpatialKit** | 空间查询 | 范围查询、最近邻 | 空间分区、高效查询、动态更新 |

---

## 📝 完整代码示例

### 事件系统 (EventKit)

```csharp
// 类型事件 - 类型安全，支持数据传递
public struct PlayerDiedEvent { public string PlayerName; }

EventKit.Type.Register<PlayerDiedEvent>(e => Debug.Log($"{e.PlayerName} 死亡"))
    .UnRegisterWhenGameObjectDestroyed(gameObject);
EventKit.Type.Send(new PlayerDiedEvent { PlayerName = "Player1" });

// 枚举事件 - 轻量级，适合简单通知
EventKit.Enum.Register<GameEvent, int>(this, GameEvent.LevelUp, OnLevelUp);
EventKit.Enum.Send(GameEvent.LevelUp, 10);

// 字符串事件 - 动态事件名
EventKit.String.Register(this, "OnScoreChanged", OnScoreChanged);
EventKit.String.Send("OnScoreChanged", 100);
```

### 对象池 (PoolKit)

```csharp
// GameObject 池
var bullet = PoolKit.Spawn("Prefabs/Bullet", parent);
PoolKit.Recycle(bullet);

// C# 对象池
var list = ListPool<int>.Get();
list.Add(1);
ListPool<int>.Release(list);

// 自定义对象池
public class Enemy : IPoolable
{
    public void OnSpawn() { /* 初始化 */ }
    public void OnRecycle() { /* 清理 */ }
}
```

### 状态机 (FsmKit)

```csharp
var fsm = new Fsm<StateType>();
fsm.AddState(StateType.Idle, new IdleState());
fsm.AddState(StateType.Move, new MoveState());
fsm.Start(StateType.Idle);
fsm.ChangeState(StateType.Move);
```

### 单例 (SingletonKit)

```csharp
// MonoBehaviour 单例
public class GameManager : MonoSingleton<GameManager> { }
var mgr = GameManager.Instance;

// C# 单例
public class ConfigManager : Singleton<ConfigManager> { }
var cfg = ConfigManager.Instance;
```

### 资源管理 (ResKit)

```csharp
// 同步加载
var prefab = ResKit.Load<GameObject>("Prefabs/Player");

// 异步加载（UniTask）
var prefab = await ResKit.LoadAsync<GameObject>("Prefabs/Player");

// 实例化
var obj = ResKit.Instantiate("Prefabs/Enemy", parent);
```

### 动作序列 (ActionKit)

```csharp
ActionKit.Sequence()
    .Delay(1f, () => Debug.Log("1秒后"))
    .Callback(() => Debug.Log("立即执行"))
    .Parallel(
        transform.MoveTo(target, 2f),
        transform.ScaleTo(Vector3.one * 2, 2f)
    )
    .Repeat(3)
    .Start(this);
```

### UI 管理 (UIKit)

```csharp
// 基础操作
UIKit.OpenPanel<MainMenuPanel>();
UIKit.ClosePanel<MainMenuPanel>();

// 带数据传递
await UIKit.OpenPanel<SettingsPanel>(new SettingsData { Volume = 0.8f });

// 堆栈导航
UIKit.PushOpenPanel<SettingsPanel>();
UIKit.PopPanel(); // 返回上一级

// 预加载
await UIKit.PreloadPanelUniTaskAsync<HeavyPanel>();

// 动画
panel.SetShowAnimation(UIAnimationFactory.CreateFadeIn(0.3f));
panel.SetHideAnimation(UIAnimationFactory.CreateFadeOut(0.3f));

// 手柄导航
panel.EnableGamepadNavigation();
```

### 音频管理 (AudioKit)

```csharp
// 播放音效
AudioKit.Play("Audio/SFX/Click");

// 播放背景音乐
AudioKit.Play("Audio/BGM/MainTheme", AudioChannel.Bgm);

// 音量控制
AudioKit.SetVolume(AudioChannel.Bgm, 0.8f);
AudioKit.SetVolume(AudioChannel.Sfx, 0.6f);
```

### 配置表 (TableKit)

```csharp
// 初始化（需先通过 TableKit 工具生成代码）
TableKit.Init();

// 查询配置
var item = TableKit.Tables.TbItem.Get(1001);
Debug.Log($"物品名称: {item.Name}");

// LINQ 查询
var weapons = TableKit.Tables.TbItem.DataList
    .Where(x => x.Type == ItemType.Weapon);
```

### 存档系统 (SaveKit)

```csharp
// 创建存档
var saveData = SaveKit.CreateSaveData();
saveData.SetModule(new PlayerData { Level = 10 });
SaveKit.Save(0, saveData);

// 读取存档
var data = SaveKit.Load(0);
var playerData = data.GetModule<PlayerData>();

// 删除存档
SaveKit.Delete(0);
```

### 场景管理 (SceneKit)

```csharp
// 异步加载
await SceneKit.LoadSceneAsync("GameScene", SceneLoadMode.Single,
    onProgress: progress => Debug.Log($"加载进度: {progress:P0}"));

// 带过渡效果
await SceneKit.SwitchSceneAsync("GameScene", new FadeTransition(0.5f));

// 预加载
var handler = SceneKit.PreloadSceneAsync("NextLevel");
await SceneKit.ActivatePreloadedScene(handler);
```

### 本地化 (LocalizationKit)

```csharp
// 初始化
var provider = new JsonLocalizationProvider();
provider.LoadFromResources();
LocalizationKit.SetProvider(provider);

// 获取文本
string text = LocalizationKit.Get(1001);
string formatted = LocalizationKit.GetFormat(1002, playerName, score);

// 切换语言
LocalizationKit.SetLanguage(LanguageId.English);
```

### Buff 系统 (BuffKit)

```csharp
// 创建容器
var container = BuffKit.CreateContainer();

// 注册 Buff 数据
BuffKit.RegisterBuffData(BuffData.Create(1001, 10f, 5, StackMode.Stack)
    .WithTags(100));

// 添加 Buff
container.Add(1001);

// 更新（在游戏循环中调用）
container.Update(Time.deltaTime);

// 释放
container.Dispose();
```

### 空间查询 (SpatialKit)

```csharp
// 初始化空间分区
var spatial = new SpatialHash<Entity>(cellSize: 10f);

// 添加实体
spatial.Add(entity, entity.Position);

// 范围查询
var nearbyEntities = spatial.QueryRadius(center, radius);

// 最近邻查询
var nearest = spatial.FindNearest(position, maxDistance);

// 更新实体位置
spatial.Update(entity, newPosition);

// 移除实体
spatial.Remove(entity);
```

---

## 🛠️ 编辑器工具

### 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+E` | 打开 YokiFrame 工具面板 |
| `Alt+B` | 添加 UI 组件绑定 |

### 工具面板功能

按 `Ctrl+E` 打开工具面板，包含以下功能：

| 标签页 | 功能 | 说明 |
|--------|------|------|
| **文档** | API 文档和示例 | 完整的使用文档和代码示例 |
| **EventKit** | 事件查看器 | 实时监控事件注册和发送 |
| **PoolKit** | 对象池监控 | 查看池状态和性能统计 |
| **FsmKit** | 状态机查看器 | 监控运行时状态转换 |
| **ActionKit** | 动作监控器 | 追踪动作序列执行状态 |
| **UIKit** | UI 面板管理 | 面板创建和代码生成 |
| **AudioKit** | 音频监控 | 运行时音频监控和代码生成 |
| **TableKit** | 配置表管理 | Luban 配置表生成和管理 |
| **BuffKit** | Buff 监控器 | 实时查看活跃容器和 Buff 状态 |
| **LocalizationKit** | 本地化预览 | 文本预览和缺失翻译检测 |
| **SceneKit** | 场景管理器 | 查看已加载场景和状态 |

---

## 📚 文档索引

- **快速开始**：本文档 [README.md](README.md)
- **编辑器工具**：按 `Ctrl+E` 打开工具面板查看
- **源码位置**：[Core/Runtime](Core/Runtime) 和 [Tools](Tools)

### 目录结构

```
Assets/YokiFrame/
├── README.md                      ← 你在这里（功能导航）
├── Core/                          ← 核心模块
│   ├── Runtime/                   ← 运行时代码
│   └── Editor/                    ← 编辑器工具
└── Tools/                         ← 工具模块
    ├── ActionKit/
    ├── AudioKit/
    ├── UIKit/
    └── ...
```

---

## 📄 License

MIT License - 详见 [LICENSE](LICENSE) 文件
