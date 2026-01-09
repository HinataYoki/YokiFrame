#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// AudioKit 音频句柄文档
    /// </summary>
    internal static class AudioKitDocHandle
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "音频句柄",
                Description = "播放返回的句柄可用于控制音频。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "句柄控制",
                        Code = @"// 获取句柄
var handle = AudioKit.Play(""Audio/BGM"", AudioChannel.Bgm);

// 暂停/恢复
handle.Pause();
handle.Resume();

// 停止
handle.Stop();

// 淡出停止
handle.FadeOut(1f);

// 调整音量
handle.SetVolume(0.5f);

// 检查状态
if (handle.IsPlaying)
{
    Debug.Log($""当前播放进度: {handle.Time}"");
}",
                        Explanation = "句柄提供对单个音频实例的完整控制。"
                    }
                }
            };
        }
    }
}
#endif
