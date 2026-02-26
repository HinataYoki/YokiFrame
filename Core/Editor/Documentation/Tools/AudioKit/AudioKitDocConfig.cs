#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// AudioKit 配置与初始化文档
    /// </summary>
    internal static class AudioKitDocConfig
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "配置与初始化",
                Description = "自定义后端、配置、加载池和路径解析。",
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
                        Title = "自定义加载池",
                        Code = @"// 默认使用 ResKit 加载，无需配置
AudioKit.Play(""Audio/Click"");

// 自定义加载池（如 YooAsset）
public class YooAssetAudioLoaderPool : AbstractAudioLoaderPool
{
    protected override IAudioLoader CreateAudioLoader() 
        => new YooAssetAudioLoader(this);
}

public class YooAssetAudioLoader : IAudioLoader
{
    private readonly IAudioLoaderPool mPool;
    private AssetHandle mHandle;

    public YooAssetAudioLoader(IAudioLoaderPool pool) => mPool = pool;

    public AudioClip Load(string path)
    {
        mHandle = YooAssets.LoadAssetSync<AudioClip>(path);
        return mHandle.AssetObject as AudioClip;
    }

    public void LoadAsync(string path, Action<AudioClip> onComplete)
    {
        mHandle = YooAssets.LoadAssetAsync<AudioClip>(path);
        mHandle.Completed += h => onComplete?.Invoke(h.AssetObject as AudioClip);
    }

    public void UnloadAndRecycle()
    {
        mHandle?.Release();
        mHandle = null;
        mPool.RecycleLoader(this);
    }
}

// 设置自定义加载池
AudioKit.SetLoaderPool(new YooAssetAudioLoaderPool());",
                        Explanation = "通过实现 IAudioLoaderPool 接口扩展加载方式。"
                    }
                }
            };
        }
    }
}
#endif
