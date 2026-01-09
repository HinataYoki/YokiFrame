#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// EventKit EasyEvent 底层 API 文档
    /// </summary>
    internal static class EventKitDocEasyEvent
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "EasyEvent 底层 API",
                Description = "EventKit 内部使用 EasyEvent 实现，也可以直接使用 EasyEvent 创建独立的事件实例。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "直接使用 EasyEvent",
                        Code = @"// 创建独立的事件实例
private readonly EasyEvent mOnDeath = new();
private readonly EasyEvent<int> mOnHealthChanged = new();
private readonly EasyEvent<int, string> mOnItemAdded = new();

// 注册
mOnDeath.Register(OnDeath);
mOnHealthChanged.Register(OnHealthChanged);

// 触发
mOnDeath.Trigger();
mOnHealthChanged.Trigger(currentHealth);
mOnItemAdded.Trigger(itemId, itemName);

// 注销
mOnDeath.UnRegister(OnDeath);
mOnDeath.UnRegisterAll(); // 注销所有监听者",
                        Explanation = "EasyEvent 适合在类内部使用，不需要全局事件总线的场景。"
                    }
                }
            };
        }
    }
}
#endif
