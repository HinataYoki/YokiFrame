using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 手柄导航配置 - 定义手柄输入的各项参数
    /// </summary>
    [CreateAssetMenu(fileName = "GamepadConfig", menuName = "YokiFrame/UIKit/Gamepad Config")]
    public class GamepadConfig : ScriptableObject
    {
        #region 导航配置

        [Header("导航设置")]
        [Tooltip("导航输入死区阈值")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float mNavigationDeadzone = 0.5f;

        [Tooltip("导航重复延迟（首次输入后的等待时间）")]
        [SerializeField] private float mNavigationRepeatDelay = 0.4f;

        [Tooltip("导航重复间隔（持续输入时的间隔）")]
        [SerializeField] private float mNavigationRepeatRate = 0.1f;

        [Tooltip("是否启用对角线导航")]
        [SerializeField] private bool mAllowDiagonalNavigation;

        #endregion

        #region 输入模式检测

        [Header("输入模式检测")]
        [Tooltip("鼠标移动检测阈值")]
        [SerializeField] private float mMouseMoveThreshold = 1f;

        [Tooltip("切换到手柄模式后隐藏鼠标光标")]
        [SerializeField] private bool mHideCursorOnGamepad = true;

        #endregion

        #region 焦点高亮

        [Header("焦点高亮")]
        [Tooltip("焦点高亮移动动画时长")]
        [SerializeField] private float mHighlightMoveDuration = 0.1f;

        [Tooltip("焦点高亮缩放动画时长")]
        [SerializeField] private float mHighlightScaleDuration = 0.08f;

        [Tooltip("焦点高亮边距")]
        [SerializeField] private Vector2 mHighlightPadding = new(8f, 8f);

        [Tooltip("焦点高亮颜色")]
        [SerializeField] private Color mHighlightColor = new(1f, 0.8f, 0.2f, 1f);

        #endregion

        #region 属性访问器

        /// <summary>
        /// 导航输入死区阈值
        /// </summary>
        public float NavigationDeadzone => mNavigationDeadzone;

        /// <summary>
        /// 导航重复延迟
        /// </summary>
        public float NavigationRepeatDelay => mNavigationRepeatDelay;

        /// <summary>
        /// 导航重复间隔
        /// </summary>
        public float NavigationRepeatRate => mNavigationRepeatRate;

        /// <summary>
        /// 是否允许对角线导航
        /// </summary>
        public bool AllowDiagonalNavigation => mAllowDiagonalNavigation;

        /// <summary>
        /// 鼠标移动检测阈值
        /// </summary>
        public float MouseMoveThreshold => mMouseMoveThreshold;

        /// <summary>
        /// 切换到手柄模式时是否隐藏光标
        /// </summary>
        public bool HideCursorOnGamepad => mHideCursorOnGamepad;

        /// <summary>
        /// 焦点高亮移动动画时长
        /// </summary>
        public float HighlightMoveDuration => mHighlightMoveDuration;

        /// <summary>
        /// 焦点高亮缩放动画时长
        /// </summary>
        public float HighlightScaleDuration => mHighlightScaleDuration;

        /// <summary>
        /// 焦点高亮边距
        /// </summary>
        public Vector2 HighlightPadding => mHighlightPadding;

        /// <summary>
        /// 焦点高亮颜色
        /// </summary>
        public Color HighlightColor => mHighlightColor;

        #endregion

        #region 默认配置

        private static GamepadConfig sDefault;

        /// <summary>
        /// 获取默认配置（运行时创建）
        /// </summary>
        public static GamepadConfig Default
        {
            get
            {
                if (sDefault == null)
                {
                    sDefault = CreateInstance<GamepadConfig>();
                    sDefault.name = "DefaultGamepadConfig";
                }
                return sDefault;
            }
        }

        #endregion
    }
}
