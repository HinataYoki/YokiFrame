#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// BuffKit 事件监听文档
    /// </summary>
    internal static class BuffKitDocEvent
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "事件监听",
                Description = "监听 Buff 生命周期事件。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "注册事件",
                        Code = @"// 监听 Buff 添加
EventKit.Type.Register<BuffAddedEvent>(e =>
{
    Debug.Log(e.Instance.BuffId);
}).UnRegisterWhenGameObjectDestroyed(gameObject);

// 监听 Buff 移除
EventKit.Type.Register<BuffRemovedEvent>(e =>
{
    Debug.Log(e.Reason);
});

// 监听堆叠变化
EventKit.Type.Register<BuffStackChangedEvent>(e =>
{
    Debug.Log(e.OldStack + "" -> "" + e.NewStack);
});",
                        Explanation = "事件通过 EventKit 发送，支持自动注销。"
                    }
                }
            };
        }
    }
}
#endif
