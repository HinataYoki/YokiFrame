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
                Description = "自动响应语言切换的 UI 文本绑定。支持文本组件（TextMeshProUGUI/Text），也可扩展支持图片、音频等组件，详见「自定义 Binder」章节。",
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
                    },
                    new()
                    {
                        Title = "扩展到其他组件类型",
                        Code = @"// 支持自定义文本组件、图片、音频等
// 详见「自定义 Binder」文档

// 示例：绑定图片组件（需用户实现扩展方法）
var imageBinder = myImage.BindLocalizedSprite(spriteId: 2001);

// 示例：绑定音频组件
var audioBinder = myAudioSource.BindLocalizedAudio(audioId: 3001);",
                        Explanation = "LocalizationKit 基于依赖倒置原则设计，用户可通过实现 ILocalizationBinder 或使用泛型 LocalizedBinder<T> 扩展任意组件类型。"
                    }
                }
            };
        }
    }
}
#endif
