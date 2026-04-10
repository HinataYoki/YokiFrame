namespace YokiFrame
{
    /// <summary>
    /// 本地化绑定器接口
    /// 用于 UI 组件自动响应语言切换
    /// </summary>
    public interface ILocalizationBinder
    {
        /// <summary>
        /// 绑定的文本ID
        /// </summary>
        int TextId { get; }

        /// <summary>
        /// 绑定器是否有效
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// 刷新显示文本
        /// </summary>
        void Refresh();
    }
}
