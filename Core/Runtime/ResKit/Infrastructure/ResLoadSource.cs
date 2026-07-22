#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>保存一次资源 lease 获取发生的位置；仅在显式跟踪开启时携带真实调用栈。</summary>
    internal readonly struct ResLoadSource
    {
        /// <summary>创建不可变加载来源。</summary>
        internal ResLoadSource(bool tracked, string display, string filePath, int line)
        {
            Tracked = tracked;
            Display = display ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            Line = line;
        }

        internal bool Tracked { get; }
        internal string Display { get; }
        internal string FilePath { get; }
        internal int Line { get; }
    }
}
#endif
