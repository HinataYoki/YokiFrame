#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// AudioKit 资源管理文档
    /// </summary>
    internal static class AudioKitDocResource
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "资源管理",
                Description = "预加载和卸载音频资源。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "预加载",
                        Code = @"// 同步预加载
AudioKit.Preload(""Audio/BGM_Battle"");

// 异步预加载
AudioKit.PreloadAsync(""Audio/LargeFile"", () =>
{
    Debug.Log(""预加载完成"");
});

// UniTask 预加载
await AudioKit.PreloadUniTaskAsync(""Audio/LargeFile"");

// 卸载
AudioKit.Unload(""Audio/BGM_Battle"");

// 卸载所有
AudioKit.UnloadAll();",
                        Explanation = "预加载适合在 Loading 界面提前加载后续需要的音频。"
                    }
                }
            };
        }
    }
}
#endif
