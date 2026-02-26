#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// AudioKit FMOD 集成文档
    /// </summary>
    internal static class AudioKitDocFmod
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
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
AudioKit.Play(1001, AudioChannel.Sfx);",
                        Explanation = "推荐使用 int ID + PathResolver 方式管理 FMOD 事件。"
                    },
                    new()
                    {
                        Title = "FMOD 3D 音效",
                        Code = @"// FMOD 3D 音效自动使用 FMOD 的空间化系统
AudioKit.Play3D(""event:/SFX/Footstep"", playerPosition);

// 跟随目标
AudioKit.Play3D(""event:/SFX/Engine"", vehicleTransform);

// FMOD 的 3D 衰减由 FMOD Studio 中的事件设置控制
// AudioPlayConfig 的 MinDistance/MaxDistance 不影响 FMOD 事件",
                        Explanation = "FMOD 3D 音效的衰减参数在 FMOD Studio 中配置。"
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
                    }
                }
            };
        }
    }
}
#endif
