#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit 异步加载文档
    /// </summary>
    internal static class LocalizationKitDocAsync
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "异步加载",
                Description = "使用 UniTask 异步加载语言数据。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "异步操作",
                        Code = @"// 异步加载语言
await LocalizationKitAsync.LoadLanguageAsync(
    LanguageId.Japanese,
    progress: new Progress<float>(p => Debug.Log($""加载进度: {p:P0}"")),
    cancellationToken: destroyCancellationToken
);

// 异步切换语言（包含加载）
bool success = await LocalizationKitAsync.SetLanguageAsync(
    LanguageId.Japanese,
    cancellationToken: destroyCancellationToken
);

// 异步获取文本
string text = await LocalizationKitAsync.GetAsync(TextId.TITLE);

// 异步卸载语言
await LocalizationKitAsync.UnloadLanguageAsync(LanguageId.Japanese);",
                        Explanation = "需要定义 YOKIFRAME_UNITASK_SUPPORT 宏。支持取消令牌和进度回调。"
                    }
                }
            };
        }
    }
}
#endif
