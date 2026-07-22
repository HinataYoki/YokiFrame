using System;

namespace YokiFrame
{
    /// <summary>声明具体后端可以真实兑现的可选播放语义。</summary>
    [Flags]
    public enum AudioBackendCapabilities
    {
        /// <summary>不声明任何可选语义。</summary>
        None = 0,
        /// <summary>支持真实异步资源加载。</summary>
        AsyncLoading = 1 << 0,
        /// <summary>支持逐次播放覆盖循环语义。</summary>
        LoopOverride = 1 << 1,
        /// <summary>支持三维空间音频。</summary>
        SpatialAudio = 1 << 2,
        /// <summary>支持逐次播放覆盖距离衰减。</summary>
        RolloffOverride = 1 << 3,
        /// <summary>支持运行期间跟随位置目标。</summary>
        FollowTarget = 1 << 4,
        /// <summary>支持资源预加载与卸载。</summary>
        Preload = 1 << 5,
        /// <summary>声明全部标准可选语义。</summary>
        All = AsyncLoading | LoopOverride | SpatialAudio | RolloffOverride | FollowTarget | Preload
    }
}
