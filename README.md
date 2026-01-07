# YokiFrame

<p align="center">
  <img src="Core/Editor/Resources/yoki.png" alt="YokiFrame Logo" width="128" height="128">
</p>

<p align="center">
  <b>一个轻量级的 Unity 开发框架</b><br>
  提供架构设计、事件系统、动作序列、状态机、UI管理、音频管理、存档系统等常用功能模块。
</p>

## 📦 安装

通过 Unity Package Manager 安装：
1. 打开 `Window > Package Manager`
2. 点击 `+` > `Add package from git URL`
3. 输入：`https://github.com/HinataYoki/YokiFrame.git`

## 📖 文档

在 Unity 编辑器中按 `Ctrl+E` 打开 YokiFrame 工具面板，选择「文档」标签页查看完整 API 文档和使用示例。

## 🧩 模块概览

### 核心模块 (Core)

| 模块 | 说明 |
|------|------|
| **Architecture** | 轻量级服务架构，支持依赖注入和模块化管理 |
| **EventKit** | 类型安全的事件系统（TypeEvent / EnumEvent） |
| **SingletonKit** | 普通类和 MonoBehaviour 单例支持 |
| **PoolKit** | 高效对象池管理 |
| **ResKit** | 统一资源加载接口，支持扩展 YooAsset |
| **FsmKit** | 简洁的有限状态机 |
| **KitLogger** | 支持加密、文件写入和 IMGUI 运行时显示的日志系统 |
| **Bindable** | 响应式数据绑定 |
| **FluentApi** | 便捷的链式扩展方法 |

### 工具模块 (Tools)

| 模块 | 说明 |
|------|------|
| **ActionKit** | 链式动作序列系统（延时、回调、并行、循环、Lerp） |
| **UIKit** | 带热度管理的 UI 面板系统，支持编辑器快速创建和代码生成 |
| **AudioKit** | 高扩展性音频管理，支持 Unity 原生和 FMOD 后端 |
| **SaveKit** | 完整存档方案，支持多槽位、加密、版本迁移 |
| **TableKit** | Luban 配置表集成工具，支持编辑器配置和代码生成 |
| **BuffKit** | 通用 Buff 系统，支持堆叠、时间管理、属性修改、免疫、序列化 |
| **LocalizationKit** | 多语言本地化系统，支持参数化文本、复数形式、UI 绑定、异步加载 |

## ⚡ 快速开始

```csharp
// 事件系统
EventKit.Type.Register<PlayerDiedEvent>(e => Debug.Log($"{e.PlayerName} 死亡"))
    .UnRegisterWhenGameObjectDestroyed(gameObject);
EventKit.Type.Send(new PlayerDiedEvent { PlayerName = "Player1" });

// 动作序列
ActionKit.Sequence()
    .Delay(1f, () => Debug.Log("1秒后"))
    .Callback(() => Debug.Log("立即执行"))
    .Start(this);

// 音频播放
AudioKit.Play("Audio/BGM/MainTheme", AudioChannel.Bgm);
AudioKit.Play("Audio/SFX/Click");

// UI 管理
UIKit.OpenPanel<MainMenuPanel>();
UIKit.ClosePanel<MainMenuPanel>();

// 存档系统
var saveData = SaveKit.CreateSaveData();
saveData.SetModule(new PlayerData { Level = 10 });
SaveKit.Save(0, saveData);

// 配置表（需先通过 TableKit 工具生成代码）
TableKit.Init();
var item = TableKit.Tables.TbItem.Get(1001);
Debug.Log($"物品名称: {item.Name}");

// Buff 系统
var container = BuffKit.CreateContainer();
BuffKit.RegisterBuffData(BuffData.Create(1001, 10f, 5, StackMode.Stack).WithTags(100));
container.Add(1001);
container.Update(Time.deltaTime); // 在游戏循环中调用
container.Dispose(); // 使用完毕后释放

// 本地化系统
var provider = new JsonLocalizationProvider();
provider.LoadFromResources();
LocalizationKit.SetProvider(provider);
string text = LocalizationKit.Get(1001); // 获取文本
LocalizationKit.SetLanguage(LanguageId.English); // 切换语言

// KitLogger IMGUI 日志显示（打包后调试）
KitLogger.EnableIMGUI(); // 启用 IMGUI 日志窗口
// PC: 按 ` 键切换显示 | 移动端: 三指触摸切换
```

## 🛠️ 编辑器工具

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+E` | 打开 YokiFrame 工具面板 |
| `Alt+B` | 添加 UI 组件绑定 |

工具面板包含：
- **文档** - 完整 API 文档和使用示例
- **EventKit** - 事件查看器，实时监控事件注册和发送
- **FsmKit** - 状态机查看器，监控运行时状态
- **ActionKit** - Action 监控器，追踪动作序列执行状态
- **UIKit** - UI 面板创建和代码生成
- **AudioKit** - 运行时音频监控和代码生成
- **TableKit** - Luban 配置表生成和管理（需安装 Luban 包）
- **BuffKit** - Buff 监控器，实时查看活跃容器和 Buff 状态
- **Localization** - 本地化文本预览和缺失翻译检测

## 📄 License

MIT License - 详见 [LICENSE](LICENSE) 文件
