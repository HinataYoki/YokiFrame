#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 工具页面接口
    /// 
    /// 所有工具页面必须实现此接口。
    /// 推荐继承 YokiToolPageBase 获得更多便捷功能。
    /// </summary>
    public interface IYokiToolPage
    {
        /// <summary>
        /// 页面名称（显示在侧边栏）
        /// </summary>
        string PageName { get; }
        
        /// <summary>
        /// 页面图标 ID（使用 KitIcons 常量）
        /// </summary>
        string PageIcon { get; }
        
        /// <summary>
        /// 排序优先级（越小越靠前）
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// 创建页面 UI
        /// </summary>
        /// <returns>页面根元素</returns>
        VisualElement CreateUI();
        
        /// <summary>
        /// 页面激活时调用
        /// </summary>
        void OnActivate();
        
        /// <summary>
        /// 页面停用时调用
        /// </summary>
        void OnDeactivate();
        
        /// <summary>
        /// 定时更新（已废弃，请使用响应式订阅）
        /// </summary>
        void OnUpdate();
    }
}
#endif
