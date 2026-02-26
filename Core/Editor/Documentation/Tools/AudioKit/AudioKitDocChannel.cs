#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// AudioKit 通道控制文档
    /// </summary>
    internal static class AudioKitDocChannel
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "通道控制",
                Description = "按通道管理音量和静音状态。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "通道音量",
                        Code = @"// 设置通道音量
AudioKit.SetChannelVolume(AudioChannel.Bgm, 0.5f);
AudioKit.SetChannelVolume(AudioChannel.Sfx, 0.8f);

// 获取通道音量
float bgmVolume = AudioKit.GetChannelVolume(AudioChannel.Bgm);

// 静音通道
AudioKit.MuteChannel(AudioChannel.Voice, true);

// 停止通道所有音频
AudioKit.StopChannel(AudioChannel.Sfx);",
                        Explanation = "通道音量会影响该通道下所有音频。"
                    },
                    new()
                    {
                        Title = "全局控制",
                        Code = @"// 全局音量
AudioKit.SetGlobalVolume(0.7f);
float volume = AudioKit.GetGlobalVolume();

// 全局静音
AudioKit.MuteAll(true);
bool isMuted = AudioKit.IsMuted();

// 暂停/恢复所有
AudioKit.PauseAll();
AudioKit.ResumeAll();

// 停止所有
AudioKit.StopAll();",
                        Explanation = "全局控制会影响所有通道的音频。"
                    }
                }
            };
        }
    }
}
#endif
