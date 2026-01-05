#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 工具页面接口
    /// </summary>
    public interface IYokiFrameToolPage
    {
        /// <summary>
        /// 页面名称
        /// </summary>
        string PageName { get; }
        
        /// <summary>
        /// 排序优先级（越小越靠前）
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// 创建页面 UI
        /// </summary>
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
        /// 定时更新（用于刷新运行时数据）
        /// </summary>
        void OnUpdate();
    }
}
#endif
