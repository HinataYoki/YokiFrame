#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// EventKit 参数类型通道隔离文档
    /// </summary>
    internal static class EventKitDocChannel
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "参数类型通道隔离",
                Description = "不同参数类型的事件通道是完全隔离的，互不影响。这是编译期类型安全的核心设计。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "通道隔离示例",
                        Code = @"// 以下是 5 个完全独立的事件通道，互不干扰：

// 通道1：无参数
EventKit.Enum.Register(GameEvent.Test, OnTestNoParam);
EventKit.Enum.Send(GameEvent.Test);

// 通道2：int 参数
EventKit.Enum.Register<GameEvent, int>(GameEvent.Test, OnTestInt);
EventKit.Enum.Send(GameEvent.Test, 100);

// 通道3：string 参数
EventKit.Enum.Register<GameEvent, string>(GameEvent.Test, OnTestString);
EventKit.Enum.Send(GameEvent.Test, ""hello"");

// 通道4：元组参数
EventKit.Enum.Register<GameEvent, (int, string)>(GameEvent.Test, OnTestTuple);
EventKit.Enum.Send(GameEvent.Test, (1, ""world""));

// 通道5：object[] 参数
EventKit.Enum.Register(GameEvent.Test, OnTestParams);
EventKit.Enum.Send(GameEvent.Test, 1, ""a"", 3.14f);

// 触发 Send<int> 只会通知 Register<int> 的监听者
// 触发 Send<string> 只会通知 Register<string> 的监听者",
                        Explanation = "这种设计确保了编译期类型检查，避免运行时类型转换错误。"
                    },
                    new()
                    {
                        Title = "常见误区",
                        Code = @"// ❌ 错误：以为 Send<int> 会触发无参监听者
EventKit.Enum.Register(GameEvent.Test, OnTest);  // 无参监听
EventKit.Enum.Send(GameEvent.Test, 100);         // 发送 int 参数
// OnTest 不会被调用！因为它们在不同通道

// ✅ 正确：参数类型必须匹配
EventKit.Enum.Register<GameEvent, int>(GameEvent.Test, OnTestInt);
EventKit.Enum.Send(GameEvent.Test, 100);  // OnTestInt 会被调用

// ✅ 正确：无参事件使用无参 Send
EventKit.Enum.Register(GameEvent.Test, OnTest);
EventKit.Enum.Send(GameEvent.Test);  // OnTest 会被调用",
                        Explanation = "Register 和 Send 的参数类型必须完全一致才能正确通信。"
                    }
                }
            };
        }
    }
}
#endif
