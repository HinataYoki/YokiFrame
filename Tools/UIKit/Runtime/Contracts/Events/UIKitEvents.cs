using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    #region Panel Lifecycle Events

    /// <summary>
    /// 面板即将显示事件
    /// </summary>
    public struct PanelWillShowEvent
    {
        public IPanel Panel;
    }

    /// <summary>
    /// 面板已显示事件
    /// </summary>
    public struct PanelDidShowEvent
    {
        public IPanel Panel;
    }

    /// <summary>
    /// 面板即将隐藏事件
    /// </summary>
    public struct PanelWillHideEvent
    {
        public IPanel Panel;
    }

    /// <summary>
    /// 面板已隐藏事件
    /// </summary>
    public struct PanelDidHideEvent
    {
        public IPanel Panel;
    }

    /// <summary>
    /// 面板获得焦点事件
    /// </summary>
    public struct PanelFocusEvent
    {
        public IPanel Panel;
    }

    /// <summary>
    /// 面板失去焦点事件
    /// </summary>
    public struct PanelBlurEvent
    {
        public IPanel Panel;
    }

    /// <summary>
    /// 面板恢复事件
    /// </summary>
    public struct PanelResumeEvent
    {
        public IPanel Panel;
    }

    #endregion

    #region Focus Events

    /// <summary>
    /// UI 焦点变更事件
    /// </summary>
    public struct UIFocusChangedEvent
    {
        /// <summary>
        /// 之前的焦点元素
        /// </summary>
        public Selectable Previous;
        
        /// <summary>
        /// 当前的焦点元素
        /// </summary>
        public Selectable Current;
    }

    /// <summary>
    /// 输入模式
    /// </summary>
    public enum InputMode
    {
        Mouse,
        Keyboard,
        Gamepad
    }

    /// <summary>
    /// 输入模式变更事件
    /// </summary>
    public struct UIInputModeChangedEvent
    {
        /// <summary>
        /// 当前输入模式
        /// </summary>
        public InputMode Mode;
    }

    #endregion

    #region Screen Events

    /// <summary>
    /// 屏幕尺寸变更事件
    /// </summary>
    public struct ScreenSizeChangedEvent
    {
        /// <summary>
        /// 之前的屏幕尺寸
        /// </summary>
        public Vector2 PreviousSize;
        
        /// <summary>
        /// 新的屏幕尺寸
        /// </summary>
        public Vector2 NewSize;
    }

    /// <summary>
    /// 屏幕方向变更事件
    /// </summary>
    public struct ScreenAspectChangedEvent
    {
        /// <summary>
        /// 新的屏幕方向
        /// </summary>
        public ScreenAspect NewAspect;
    }

    #endregion
}
