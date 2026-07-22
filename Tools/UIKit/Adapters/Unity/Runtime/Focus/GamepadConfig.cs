#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame
{
    /// <summary>保存 UIKit 导航重复、输入模式和焦点高亮参数。</summary>
    [CreateAssetMenu(fileName = "GamepadConfig", menuName = "YokiFrame/UIKit/Gamepad Config")]
    public sealed class GamepadConfig : ScriptableObject
    {
        [SerializeField, Range(0.1f, 0.9f)] private float mNavigationDeadzone = 0.5f;
        [SerializeField] private float mNavigationRepeatDelay = 0.4f;
        [SerializeField] private float mNavigationRepeatRate = 0.1f;
        [SerializeField] private bool mAllowDiagonalNavigation;
        [SerializeField] private float mMouseMoveThreshold = 1f;
        [SerializeField] private bool mHideCursorOnGamepad = true;
        [SerializeField] private float mHighlightMoveDuration = 0.1f;
        [SerializeField] private float mHighlightScaleDuration = 0.08f;
        [SerializeField] private Vector2 mHighlightPadding = new(8f, 8f);
        [SerializeField] private Color mHighlightColor = new(1f, 0.8f, 0.2f, 1f);
        private static GamepadConfig sDefault;

        /// <summary>导航输入死区。</summary>
        public float NavigationDeadzone => mNavigationDeadzone;

        /// <summary>首次导航重复延迟。</summary>
        public float NavigationRepeatDelay => mNavigationRepeatDelay;

        /// <summary>持续导航重复间隔。</summary>
        public float NavigationRepeatRate => mNavigationRepeatRate;

        /// <summary>是否接受对角导航输入。</summary>
        public bool AllowDiagonalNavigation => mAllowDiagonalNavigation;

        /// <summary>判定鼠标移动的最小像素距离。</summary>
        public float MouseMoveThreshold => mMouseMoveThreshold;

        /// <summary>导航模式下是否隐藏鼠标。</summary>
        public bool HideCursorOnGamepad => mHideCursorOnGamepad;

        /// <summary>高亮移动时长。</summary>
        public float HighlightMoveDuration => mHighlightMoveDuration;

        /// <summary>高亮尺寸变化时长。</summary>
        public float HighlightScaleDuration => mHighlightScaleDuration;

        /// <summary>焦点高亮边距。</summary>
        public Vector2 HighlightPadding => mHighlightPadding;

        /// <summary>焦点高亮颜色。</summary>
        public Color HighlightColor => mHighlightColor;

        /// <summary>获取运行时创建的默认配置。</summary>
        public static GamepadConfig Default
        {
            get
            {
                if (sDefault != null) return sDefault;
                sDefault = CreateInstance<GamepadConfig>();
                sDefault.name = "DefaultGamepadConfig";
                return sDefault;
            }
        }
    }
}
#endif
