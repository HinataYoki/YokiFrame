#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit UI 绑定文档
    /// </summary>
    internal static class LocalizationKitDocBind
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "UI 绑定",
                Description = "自动响应语言切换的 UI 文本绑定。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "绑定文本组件",
                        Code = @"// 绑定 TextMeshProUGUI
var binder = tmpText.BindLocalization(TextId.TITLE);

// 绑定带参数的文本
var binder2 = tmpText.BindLocalization(TextId.WELCOME, ""玩家名"");

// 绑定 Legacy Text
var binder3 = legacyText.BindLocalization(TextId.CONFIRM);

// 更新参数
binder2.UpdateArgs(""新玩家名"");

// 手动刷新
binder.Refresh();

// 释放绑定（重要！）
binder.Dispose();",
                        Explanation = "绑定器会自动注册到 LocalizationKit，语言切换时自动刷新。使用完毕后必须调用 Dispose() 释放。"
                    },
                    new()
                    {
                        Title = "手动管理绑定器",
                        Code = @"// 创建绑定器
var binder = new LocalizedTextBinder(TextId.TITLE, tmpText);

// 使用命名参数
var args = new Dictionary<string, object> { { ""name"", ""Test"" } };
var binder2 = new LocalizedTextBinder(TextId.MSG, tmpText, args);

// 获取绑定器数量
int count = LocalizationKit.GetBinderCount();"
                    }
                }
            };
        }
    }
}
#endif
