#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// EventKit 高级用法文档。
    /// </summary>
    internal static class EventKitDocAdvanced
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "高级用法",
                Description = "可变参数事件、批量注销以及生命周期绑定。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "可变参数事件",
                        Code = @"EventKit.Enum.Send(GameEvent.Custom, ""arg1"", 100, true);

EventKit.Enum.Register<GameEvent>(GameEvent.Custom, args =>
{
    string str = (string)args[0];
    int num = (int)args[1];
    bool flag = (bool)args[2];
});",
                        Explanation = "可变参数事件通过 object[] 传递参数，接收方需手动转型。"
                    },
                    new()
                    {
                        Title = "批量注销",
                        Code = @"// 注销指定事件的所有监听者
EventKit.Enum.UnRegister(GameEvent.PlayerDied);

// 清空全部枚举事件
EventKit.Enum.Clear();

// 清空全部类型事件
EventKit.Type.Clear();",
                        Explanation = "Clear 类 API 会移除所有已注册的处理者，应谨慎使用。"
                    },
                    new()
                    {
                        Title = "生命周期绑定",
                        Code = @"// 注册事件并绑定到 GameObject 生命周期
EventKit.Type.Register<PlayerData>(OnPlayerDataChanged)
    .UnRegisterWhenGameObjectDestroyed(gameObject);

// 手动注销
var unregister = EventKit.Type.Register<PlayerData>(OnPlayerDataChanged);
unregister.UnRegister();",
                        Explanation = "UnRegisterWhenGameObjectDestroyed 在 GameObject 销毁时自动注销，避免空引用。"
                    }
                }
            };
        }
    }
}
#endif
