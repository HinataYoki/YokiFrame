namespace YokiFrame
{
    /// <summary>定义语言切换时需要刷新的绑定对象。</summary>
    public interface ILocalizationBinder
    {
        /// <summary>获取绑定使用的文本编号。</summary>
        int TextId { get; }
        /// <summary>判断绑定对象是否仍然有效。</summary>
        bool IsValid { get; }
        /// <summary>刷新绑定显示。</summary>
        void Refresh();
    }
}
