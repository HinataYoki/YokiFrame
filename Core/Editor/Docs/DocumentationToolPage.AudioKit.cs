#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    public partial class DocumentationToolPage
    {
        private DocModule CreateAudioKitDoc()
        {
            return new DocModule
            {
                Name = "AudioKit",
                Icon = "🔊",
                Category = "TOOLS",
                Description = "音频管理工具，提供多通道音频播放、音量控制、3D 音效、预加载等功能。支持自定义后端和路径解析。",
                Sections = new List<DocSection>
                {
                    new()
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
AudioKit.Play(""Audio/BGM"", config);"
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
var handle = await AudioKit.PlayUniTaskAsync(""Audio/LargeFile"", config);"
                            }
                        }
                    },
                    new()
                    {
                        Title = "3D 音效",
                        Description = "支持位置音效和跟随目标音效。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "3D 音效播放",
                                Code = @"// 在指定位置播放
AudioKit.Play3D(""Audio/Explosion"", explosionPosition);

// 跟随目标播放
AudioKit.Play3D(""Audio/Engine"", vehicleTransform);

// 带配置的 3D 音效
var config = AudioPlayConfig.Create3D(position)
    .WithVolume(0.9f)
    .WithMinDistance(1f)
    .WithMaxDistance(50f);
AudioKit.Play(""Audio/Ambient"", config);"
                            }
                        }
                    },
                    new()
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
}"
                            }
                        }
                    },
                    new()
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
AudioKit.StopChannel(AudioChannel.Sfx);"
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
AudioKit.StopAll();"
                            }
                        }
                    },
                    new()
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
AudioKit.UnloadAll();"
                            }
                        }
                    },
                    new()
                    {
                        Title = "配置与初始化",
                        Description = "自定义后端、配置和路径解析。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "初始化配置",
                                Code = @"// 设置全局配置
var config = new AudioKitConfig
{
    MaxConcurrentSounds = 32,
    PoolSize = 16,
    GlobalVolume = 1f,
    BgmVolume = 0.8f,
    SfxVolume = 1f
};
AudioKit.SetConfig(config);

// 设置路径解析器（用于 int ID 方式）
AudioKit.SetPathResolver(audioId =>
{
    return AudioConfig.GetPath(audioId);
});

// 使用 int ID 播放
AudioKit.Play(1001, AudioChannel.Sfx);",
                                Explanation = "推荐使用 int ID + PathResolver 方式，避免魔法字符串。"
                            },
                            new()
                            {
                                Title = "自定义后端",
                                Code = @"// 实现自定义后端
public class FMODAudioBackend : IAudioBackend
{
    // 实现接口方法...
}

// 设置后端
AudioKit.SetBackend(new FMODAudioBackend());"
                            }
                        }
                    },
                    new()
                    {
                        Title = "编辑器工具",
                        Description = "AudioKit 提供音频 ID 代码生成器和运行时音频监控工具。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "使用编辑器工具",
                                Code = @"// 快捷键：Ctrl+E 打开 YokiFrame Tools 面板
// 选择 AudioKit 标签页

// 功能：
// - 音频 ID 生成器：扫描音频资源自动生成 ID 常量
// - 运行时监控：查看所有播放中的音频
// - 通道状态：查看各通道的音量和音频数量
// - 性能分析：查看音频池使用情况",
                                Explanation = "编辑器工具帮助管理音频资源，避免魔法字符串。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "FMOD 集成",
                        Description = "AudioKit 支持 FMOD Studio 作为音频后端，提供专业级音频功能。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "启用 FMOD 支持",
                                Code = @"// 1. 安装 FMOD Unity 插件
// 2. 在 Project Settings > Player > Scripting Define Symbols 添加：
//    YOKIFRAME_FMOD_SUPPORT

// 3. 初始化时设置 FMOD 后端
AudioKit.SetBackend(new FmodAudioBackend());

// 4. 使用 FMOD 事件路径播放
AudioKit.Play(""event:/Music/BGM_Main"", AudioChannel.Bgm);
AudioKit.Play(""event:/SFX/Explosion"", AudioChannel.Sfx);",
                                Explanation = "FMOD 使用事件路径（event:/...）而非文件路径。"
                            },
                            new()
                            {
                                Title = "FMOD 事件路径",
                                Code = @"// FMOD 事件路径格式
// event:/文件夹/事件名

// 示例
AudioKit.Play(""event:/Music/BGM_Battle"");
AudioKit.Play(""event:/SFX/UI/Click"");
AudioKit.Play(""event:/Voice/NPC/Greeting"");
AudioKit.Play(""event:/Ambient/Forest"");

// 推荐：使用 PathResolver 映射 ID 到事件路径
AudioKit.SetPathResolver(audioId =>
{
    // 从配置表获取 FMOD 事件路径
    return AudioConfig.GetFmodEventPath(audioId);
});

// 使用 int ID 播放
AudioKit.Play(1001, AudioChannel.Sfx);"
                            },
                            new()
                            {
                                Title = "FMOD 3D 音效",
                                Code = @"// FMOD 3D 音效自动使用 FMOD 的空间化系统
AudioKit.Play3D(""event:/SFX/Footstep"", playerPosition);

// 跟随目标
AudioKit.Play3D(""event:/SFX/Engine"", vehicleTransform);

// FMOD 的 3D 衰减由 FMOD Studio 中的事件设置控制
// AudioPlayConfig 的 MinDistance/MaxDistance 不影响 FMOD 事件"
                            },
                            new()
                            {
                                Title = "FMOD 预加载",
                                Code = @"// 预加载 FMOD 事件的采样数据
AudioKit.Preload(""event:/Music/BGM_Boss"");

// 异步预加载
await AudioKit.PreloadUniTaskAsync(""event:/Music/BGM_Boss"");

// 卸载采样数据
AudioKit.Unload(""event:/Music/BGM_Boss"");",
                                Explanation = "FMOD 预加载会调用 EventDescription.loadSampleData()。"
                            },
                            new()
                            {
                                Title = "FMOD Bank 管理",
                                Code = @"// FMOD Bank 由 FMODUnity.RuntimeManager 自动管理
// 确保在 FMOD Settings 中正确配置 Bank 加载

// 手动加载 Bank（如果需要）
FMODUnity.RuntimeManager.LoadBank(""Master"");
FMODUnity.RuntimeManager.LoadBank(""Music"");

// 检查 Bank 是否已加载
FMODUnity.RuntimeManager.HasBankLoaded(""Master"");",
                                Explanation = "通常不需要手动管理 Bank，FMOD 会自动处理。"
                            },
                            new()
                            {
                                Title = "FMOD 与 Unity 后端切换",
                                Code = @"// 根据条件选择后端
#if YOKIFRAME_FMOD_SUPPORT
    AudioKit.SetBackend(new FmodAudioBackend());
#else
    AudioKit.SetBackend(new UnityAudioBackend());
#endif

// 或者运行时动态切换
public void SwitchToFmod()
{
    AudioKit.StopAll();
    AudioKit.UnloadAll();
    AudioKit.SetBackend(new FmodAudioBackend());
}",
                                Explanation = "切换后端前应停止并卸载所有音频。"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
