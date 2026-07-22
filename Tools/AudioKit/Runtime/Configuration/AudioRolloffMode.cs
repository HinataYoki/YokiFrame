namespace YokiFrame
{
    /// <summary>描述跨宿主 3D 音频的距离衰减语义。</summary>
    public enum AudioRolloffMode
    {
        /// <summary>使用对数距离衰减。</summary>
        Logarithmic = 0,
        /// <summary>使用线性距离衰减。</summary>
        Linear = 1,
        /// <summary>使用后端或项目定义的自定义衰减。</summary>
        Custom = 2
    }
}
