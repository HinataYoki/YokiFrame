#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// EventKit 高级用法文档
    /// </summary>
    internal static class EventKitDocAdvanced
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "高级用法",
                Description = "可变参数事件、批量注销、事件清理等高级功能。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "可变参数事件",
                        Code = @"// 发送可变参数事件
EventKit.Enum.Send(GameEvent.Custom, ""arg1"", 100, true);

// 注册可变参数事件
EventKit.Enum.Register<GameEvent>(GameEvent.Custom, args =>
{
    string str = (string)args[0];
    int num = (int)args[1];
    bool flag = (bool)args[2];
    Debug.Log($""{str}, {num}, {flag}"");
}).UnRegisterWhenGameObjectDestroyed(gameObject);",
                        Explanation = "可变参数使用 object[] 传递，需要手动类型转换。"
                    },
                    new()
                    {
                        Title = "批量注销",
                        Code = @"// 注销指定枚举的所有事件
EventKit.Enum.UnRegister(GameEvent.PlayerDied);

// 清空所有枚举事件
EventKit.Enum.Clear();

// 清空所有类型事件
EventKit.Type.Clear();

// 场景切换时清理
private void OnDestroy()
{
    // 自动注销（推荐）
    // 使用 UnRegisterWhenGameObjectDestroyed
}",
                        Explanation = "Clear 方法会移除所有已注册的事件，谨慎使用。"
                    },
                    new()
                    {
                        Title = "LinkUnRegister 链式注销",
                        Code = @"// 注册返回 LinkUnRegister 令牌
var unregister = EventKit.Type.Register<PlayerData>(OnPlayerDataChanged);

// 手动注销
unregister.UnRegister();

// 绑定到 GameObject 生命周期（推荐）
EventKit.Type.Register<PlayerData>(OnPlayerDataChanged)
    .UnRegisterWhenGameObjectDestroyed(gameObject);

// 绑定到 MonoBehaviour 禁用
EventKit.Enum.Register(GameEvent.Update, OnUpdate)
    .UnRegisterWhenDisabled(this);

// 多个事件统一管理
private readonly List<IUnRegister> mUnregisters = new();

private void OnEnable()
{
    mUnregisters.Add(EventKit.Type.Register<A>(OnA));
    mUnregisters.Add(EventKit.Type.Register<B>(OnB));
}

private void OnDisable()
{
    foreach (var u in mUnregisters) u.UnRegister();
    mUnregisters.Clear();
}",
                        Explanation = "LinkUnRegister 支持链式调用，自动管理事件生命周期。"
                    },
                    new()
                    {
                        Title = "编辑器事件系统（EditorEventCenter）",
                        Code = @"// 编辑器专用事件中心 - 不依赖运行时 EventKit
// 位于 YokiFrame.EditorTools 命名空间

#if UNITY_EDITOR
using YokiFrame.EditorTools;

// 类型事件注册
var subscription = EditorEventCenter.Register<MyEditorEvent>(OnMyEvent);

// 枚举键事件注册
EditorEventCenter.Register<EditorEventType, string>(
    EditorEventType.PoolListChanged, 
    OnPoolListChanged);

// 发送事件
EditorEventCenter.Send(new MyEditorEvent { Data = ""test"" });
EditorEventCenter.Send(EditorEventType.PoolListChanged, ""poolName"");

// 取消订阅
subscription.Dispose();

// 批量清理（EditorWindow 关闭时）
EditorEventCenter.UnregisterAll(this);
#endif",
                        Explanation = "EditorEventCenter 是编辑器专用的轻量级事件系统，不依赖运行时 EventKit，避免编辑器代码污染运行时程序集。"
                    },
                    new()
                    {
                        Title = "EditorDataBridge 数据通道",
                        Code = @"// EditorDataBridge 提供编辑器数据通道订阅机制
// 用于运行时 Debugger 与编辑器 ToolPage 之间的通信

#if UNITY_EDITOR
using YokiFrame.EditorTools;

// 订阅数据通道
var subscription = EditorDataBridge.Subscribe<List<PoolDebugInfo>>(
    DataChannels.CHANNEL_POOL_LIST_CHANGED,
    OnPoolListChanged);

// 发布数据变化通知
EditorDataBridge.NotifyDataChanged(DataChannels.CHANNEL_POOL_LIST_CHANGED);

// 预定义通道（DataChannels.cs）：
// CHANNEL_POOL_LIST_CHANGED   - 池列表变化
// CHANNEL_POOL_ACTIVE_CHANGED - 活跃对象变化
// CHANNEL_POOL_EVENT_LOGGED   - 事件日志追加
// CHANNEL_FSM_STATE_CHANGED   - 状态机状态变化
// CHANNEL_RES_LOADED          - 资源加载完成
#endif",
                        Explanation = "EditorDataBridge 是响应式架构的核心，替代了传统的 OnUpdate 轮询模式。"
                    }
                }
            };
        }
    }
}
#endif
