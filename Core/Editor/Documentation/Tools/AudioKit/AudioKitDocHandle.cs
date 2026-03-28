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
handle.StopWithFade(1f);

// 调整音量和音调
handle.Volume = 0.5f;
handle.Pitch = 1.2f;

// 检查状态
if (handle.IsPlaying)
{
    Debug.Log($""当前播放进度: {handle.Time}/{handle.Duration}"");
}",
                        Explanation = "句柄提供对单个音频实例的完整控制。"
                    },
                    new()
                    {
                        Title = "自动回收机制",
                        Code = @"// 非循环音频播放完成后自动回收
var sfx = AudioKit.Play(""Audio/Click"", AudioChannel.Sfx);
// 播放完成后自动释放，无需手动 Stop

// 循环音频需要手动停止
var bgm = AudioKit.Play(""Audio/BGM"", new AudioPlayConfig
{
    Channel = AudioChannel.Bgm,
    Loop = true
});
// 必须手动停止，否则会一直播放
bgm.Stop();

// 通道并发控制会自动停止旧音频
AudioKit.SetChannelMaxConcurrent(AudioChannel.Bgm, 1);
AudioKit.Play(""Audio/BGM1"", AudioChannel.Bgm);  // 播放 BGM1
AudioKit.Play(""Audio/BGM2"", AudioChannel.Bgm);  // 自动停止 BGM1，播放 BGM2",
                        Explanation = "非循环音频播放完成后自动回收，循环音频需要手动停止或被通道并发控制挤掉。"
                    },
                    new()
                    {
                        Title = "手动生命周期管理",
                        Code = @"// 方式1：配置时声明（推荐）
var config = AudioPlayConfig.Default
    .WithManualLifecycle(true);  // 禁用自动回收
var handle = AudioKit.Play(""Audio/BGM"", config);

// 播放完成后不会自动回收，需要手动释放
if (!handle.IsPlaying && handle.IsValid)
{
    handle.Release();  // 手动释放
}

// 方式2：运行时切换
var handle2 = AudioKit.Play(""Audio/Voice"");
handle2.SetManualLifecycle(true);  // 切换为手动模式

// 检查句柄状态
if (handle.IsValid)
{
    Debug.Log($""音频有效，手动模式: {handle.IsManualLifecycle}"");
}

// 使用场景：需要在音频播放完成后保留句柄进行状态查询
var narrator = AudioKit.Play(""Audio/Narrator"", new AudioPlayConfig
{
    Channel = AudioChannel.Voice,
    ManualLifecycle = true
});

// 等待播放完成
while (narrator.IsPlaying) 
{
    await UniTask.Yield();
}

// 播放完成后仍可查询状态
Debug.Log($""旁白时长: {narrator.Duration}秒"");
narrator.Release();  // 完成后手动释放",
                        Explanation = "手动生命周期模式下，音频播放完成不会自动回收，需要调用 Release() 手动释放。适用于需要在播放完成后保留句柄进行状态查询的场景。"
                    }
                }
            };
        }
    }
}
#endif
