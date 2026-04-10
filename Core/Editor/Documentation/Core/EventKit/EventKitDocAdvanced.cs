#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Advanced EventKit documentation.
    /// </summary>
    internal static class EventKitDocAdvanced
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "Advanced Usage",
                Description = "Variadic events, bulk unregister flows, and the editor-side communication layer.",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "Variadic Event",
                        Code = @"EventKit.Enum.Send(GameEvent.Custom, ""arg1"", 100, true);

EventKit.Enum.Register<GameEvent>(GameEvent.Custom, args =>
{
    string str = (string)args[0];
    int num = (int)args[1];
    bool flag = (bool)args[2];
});",
                        Explanation = "Variadic events pass payloads through object arrays and require manual casting."
                    },
                    new()
                    {
                        Title = "Bulk Unregister",
                        Code = @"EventKit.Enum.UnRegister(GameEvent.PlayerDied);
EventKit.Enum.Clear();
EventKit.Type.Clear();",
                        Explanation = "Clear-style APIs remove all registered handlers and should be used carefully."
                    },
                    new()
                    {
                        Title = "LinkUnRegister",
                        Code = @"var unregister = EventKit.Type.Register<PlayerData>(OnPlayerDataChanged);
unregister.UnRegister();

EventKit.Type.Register<PlayerData>(OnPlayerDataChanged)
    .UnRegisterWhenGameObjectDestroyed(gameObject);",
                        Explanation = "LinkUnRegister helps tie event subscriptions to object lifetime."
                    },
                    new()
                    {
                        Title = "EditorEventCenter",
                        Code = @"#if UNITY_EDITOR
using YokiFrame.EditorTools;

var subscription = EditorEventCenter.Register<MyEditorEvent>(OnMyEvent);

EditorEventCenter.Register<EditorEventType, string>(
    EditorEventType.PoolListChanged,
    OnPoolListChanged);

EditorEventCenter.Send(new MyEditorEvent { Data = ""test"" });
EditorEventCenter.Send(EditorEventType.PoolListChanged, ""poolName"");

subscription.Dispose();
EditorEventCenter.UnregisterAll(this);
#endif",
                        Explanation = "EditorEventCenter is the editor-only typed event hub and stays independent from runtime EventKit."
                    },
                    new()
                    {
                        Title = "EditorDataBridge",
                        Code = @"#if UNITY_EDITOR
using YokiFrame.EditorTools;

var subscription = EditorDataBridge.Subscribe<PoolDebugInfo>(
    DataChannels.POOL_LIST_CHANGED,
    OnPoolListChanged);

EditorDataBridge.NotifyDataChanged(DataChannels.POOL_LIST_CHANGED, poolInfo);

// Example shared channels:
// DataChannels.POOL_LIST_CHANGED
// DataChannels.POOL_ACTIVE_CHANGED
// DataChannels.POOL_EVENT_LOGGED
// DataChannels.FSM_STATE_CHANGED
// DataChannels.RES_LIST_CHANGED
#endif",
                        Explanation = "EditorDataBridge is the shared editor data bus used by runtime debug publishers and tool pages."
                    }
                }
            };
        }
    }
}
#endif
