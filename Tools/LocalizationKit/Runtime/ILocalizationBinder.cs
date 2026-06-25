namespace YokiFrame
{
    /// <summary>
    /// 定义语言切换时可刷新的本地化绑定对象。
    /// </summary>
    public interface ILocalizationBinder
    {
        /// <summary>
        /// 获取绑定文本编号。
        /// </summary>
        int TextId { get; }

        /// <summary>
        /// 获取绑定对象当前是否有效。
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// 刷新绑定显示内容。
        /// </summary>
        void Refresh();
    }
}
