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
                        Title = "通道并发控制",
                        Code = @"// 设置通道最大并发数（0 表示无限制）
AudioKit.SetChannelMaxConcurrent(AudioChannel.Bgm, 1);    // BGM 单曲模式
AudioKit.SetChannelMaxConcurrent(AudioChannel.Voice, 1);  // Voice 单曲模式
AudioKit.SetChannelMaxConcurrent(AudioChannel.Sfx, 0);    // SFX 无限制

// 自定义通道（channelId >= 5）
AudioKit.SetChannelMaxConcurrent(10, 3);  // 自定义通道 10 最多 3 个并发

// 查询通道并发限制
int bgmMax = AudioKit.GetChannelMaxConcurrent(AudioChannel.Bgm);
int customMax = AudioKit.GetChannelMaxConcurrent(10);",
                        Explanation = "达到并发上限时，新音频会自动停止最早播放的音频。"
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
