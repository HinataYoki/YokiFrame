#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// AudioKit 基本播放文档
    /// </summary>
    internal static class AudioKitDocBasic
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "基本播放",
                Description = "AudioKit 提供简洁的音频播放 API。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "播放音频",
                        Code = @"// 播放音效（默认 Sfx 通道）
AudioKit.Play(""Audio/Click"");

// 指定通道
AudioKit.Play(""Audio/BGM_Main"", AudioChannel.Bgm);
AudioKit.Play(""Audio/Voice_01"", AudioChannel.Voice);

// 使用自定义通道 ID（5+ 为用户自定义）
AudioKit.Play(""Audio/Custom"", channelId: 10);",
                        Explanation = "内置通道：Bgm(0), Sfx(1), Voice(2), Ambient(3), UI(4)。"
                    },
                    new()
                    {
                        Title = "播放配置",
                        Code = @"// 使用完整配置
var config = new AudioPlayConfig
{
    Channel = AudioChannel.Sfx,
    Volume = 0.8f,
    Pitch = 1.2f,
    Loop = false,
    FadeInDuration = 0.5f
};
AudioKit.Play(""Audio/Effect"", config);

// 链式配置
var config = AudioPlayConfig.Default
    .WithChannel(AudioChannel.Bgm)
    .WithVolume(0.7f)
    .WithLoop(true)
    .WithFadeIn(1f);
AudioKit.Play(""Audio/BGM"", config);",
                        Explanation = "AudioPlayConfig 支持链式配置，代码更简洁。"
                    },
                    new()
                    {
                        Title = "异步播放",
                        Code = @"// 回调方式
AudioKit.PlayAsync(""Audio/LargeFile"", config, handle =>
{
    if (handle != null)
    {
        Debug.Log(""播放开始"");
    }
});

// UniTask 方式
var handle = await AudioKit.PlayUniTaskAsync(""Audio/LargeFile"", config);",
                        Explanation = "异步播放适合大型音频文件，避免阻塞主线程。"
                    }
                }
            };
        }
    }
}
#endif
