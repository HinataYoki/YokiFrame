# YokiFrame 响应式编辑器 API

## 概述

YokiFrame 响应式编辑器系统提供了一套轻量级的数据绑定和事件通知机制，用于实现编辑器 UI 的自动更新，避免低效的轮询刷新。

## 核心组件

### EditorDataBridge

数据桥接中心，负责运行时与编辑器之间的数据通知。

```csharp
// 运行时发送通知（使用 [Conditional] 确保运行时零开销）
EditorDataBridge.NotifyDataChanged(DataChannels.ACTION_STARTED, action);

// 编辑器订阅通知
var subscription = EditorDataBridge.Subscribe<IAction>(
    DataChannels.ACTION_STARTED,
    action => RefreshUI());

// 带节流的订阅（限制刷新频率）
var subscription = EditorDataBridge.SubscribeThrottled<IAction>(
    DataChannels.ACTION_STARTED,
    action => RefreshUI(),
    intervalSeconds: 0.1f);

// 取消订阅
subscription.Dispose();
```

### DataChannels

预定义的通道常量，避免魔法字符串：

```csharp
// ActionKit 通道
DataChannels.ACTION_STARTED    // Action 开始
DataChannels.ACTION_FINISHED   // Action 结束

// UIKit 通道
DataChannels.PANEL_OPENED      // 面板打开
DataChannels.PANEL_CLOSED      // 面板关闭
DataChannels.FOCUS_CHANGED     // 焦点变化

// AudioKit 通道
DataChannels.AUDIO_PLAY_STARTED  // 音频播放
DataChannels.AUDIO_PLAY_STOPPED  // 音频停止

// BuffKit 通道
DataChannels.BUFF_ADDED        // Buff 添加
DataChannels.BUFF_REMOVED      // Buff 移除
```

### Throttle（节流器）

限制高频操作的执行频率：

```csharp
private Throttle mRefreshThrottle = new(0.1f);

// 在事件回调中使用
void OnDataChanged()
{
    mRefreshThrottle.Execute(RefreshUI);
}
```

### Debounce（防抖器）

延迟执行，重复调用会重置计时器：

```csharp
private Debounce mSearchDebounce;

void BuildUI(VisualElement root)
{
    mSearchDebounce = new Debounce(0.3f, root);
}

void OnSearchTextChanged(string text)
{
    mSearchDebounce.Execute(() => PerformSearch(text));
}
```

### ReactiveProperty<T>

响应式属性，值变化时自动通知：

```csharp
private ReactiveProperty<int> mCount = new(0);

void BuildUI()
{
    mCount.Subscribe(count => label.text = $"数量: {count}");
}

void OnButtonClick()
{
    mCount.Value++;  // 自动触发 UI 更新
}
```

### ReactiveCollection<T>

响应式集合，增删改时自动通知：

```csharp
private ReactiveCollection<string> mItems = new();

void BuildUI()
{
    mItems.ObserveAdd().Subscribe(item => AddItemToUI(item));
    mItems.ObserveRemove().Subscribe(item => RemoveItemFromUI(item));
}
```

## 编辑器桥接模式

对于需要从运行时通知编辑器的场景，推荐使用桥接模式：

### 1. 运行时定义钩子（委托方式）

```csharp
// 在运行时代码中
public static class MyKitEditorHooks
{
    public static Action<MyData> OnDataChanged;
    
    public static void Clear()
    {
        OnDataChanged = null;
    }
}
```

### 2. 编辑器注册钩子

```csharp
[InitializeOnLoad]
public static class MyKitEditorBridge
{
    static MyKitEditorBridge()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            MyKitEditorHooks.OnDataChanged = OnDataChanged;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            MyKitEditorHooks.Clear();
        }
    }

    private static void OnDataChanged(MyData data)
    {
        EditorDataBridge.NotifyDataChanged("MyKit.DataChanged", data);
    }
}
```

### 3. 使用 EventKit 事件（推荐）

如果运行时已有事件系统，直接在编辑器桥接中订阅：

```csharp
[InitializeOnLoad]
public static class MyKitEditorBridge
{
    private static void Subscribe()
    {
        EventKit.Type.Register<MyDataChangedEvent>(OnDataChanged);
    }

    private static void OnDataChanged(MyDataChangedEvent evt)
    {
        EditorDataBridge.NotifyDataChanged("MyKit.DataChanged", evt);
    }
}
```

## 在 ToolPage 中使用

继承 `YokiFrameToolPageBase` 的页面可以使用内置的订阅管理：

```csharp
public class MyToolPage : YokiFrameToolPageBase
{
    protected override void BuildUI(VisualElement root)
    {
        // 订阅会在 OnDeactivate 时自动清理
        Subscriptions.Add(EditorDataBridge.Subscribe<MyData>(
            "MyKit.DataChanged",
            data => RefreshUI()));
    }
}
```

## 最佳实践

1. **使用常量通道名**：在 `DataChannels` 中定义常量，避免魔法字符串
2. **合理使用节流**：高频事件（如进度更新）使用 `Throttle` 或 `SubscribeThrottled`
3. **及时清理订阅**：使用 `CompositeDisposable` 或 `Subscriptions` 管理订阅生命周期
4. **运行时零侵入**：使用 `[Conditional("UNITY_EDITOR")]` 或委托钩子模式
5. **跨线程安全**：`FileSystemWatcher` 等回调在非主线程，使用标志位在 `OnUpdate` 中处理
