#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// AudioKit 3D 音效文档
    /// </summary>
    internal static class AudioKitDoc3D
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
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
AudioKit.Play(""Audio/Ambient"", config);",
                        Explanation = "3D 音效会根据听者位置自动计算音量和声像。"
                    }
                }
            };
        }
    }
}
#endif
