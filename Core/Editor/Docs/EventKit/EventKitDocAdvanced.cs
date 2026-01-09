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
                    }
                }
            };
        }
    }
}
#endif
