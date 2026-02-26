#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// EventKit 字符串事件文档
    /// </summary>
    internal static class EventKitDocString
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "字符串事件（已过时）",
                Description = "使用字符串作为事件键。已标记为 [Obsolete]，存在类型安全隐患且重构困难，建议迁移到枚举或类型事件。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "字符串事件用法（不推荐）",
                        Code = @"// ⚠️ 已过时，仅用于旧代码兼容
#pragma warning disable CS0618

// 注册
EventKit.String.Register(""PlayerDied"", OnPlayerDied);
EventKit.String.Register<int>(""ScoreChanged"", OnScoreChanged);

// 触发
EventKit.String.Send(""PlayerDied"");
EventKit.String.Send(""ScoreChanged"", 100);

// 注销
EventKit.String.UnRegister(""PlayerDied"", OnPlayerDied);

#pragma warning restore CS0618",
                        Explanation = "字符串事件容易拼写错误，重构时无法自动更新引用，建议尽快迁移。"
                    }
                }
            };
        }
    }
}
#endif
