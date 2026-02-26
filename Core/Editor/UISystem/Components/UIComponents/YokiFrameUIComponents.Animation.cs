#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 动画效果
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        /// <summary>
        /// 滑入动画方向
        /// </summary>
        public enum SlideDirection
        {
            Left,
            Right,
            Up
        }

        /// <summary>
        /// 为元素添加淡入动画
        /// </summary>
        public static void AddFadeInAnimation(VisualElement element, int delayMs = 0)
        {
            element.AddToClassList("content-fade-in");
            element.schedule.Execute(() =>
            {
                element.AddToClassList("content-visible");
            }).ExecuteLater(delayMs + 16);
        }

        /// <summary>
        /// 为元素添加滑入动画
        /// </summary>
        public static void AddSlideInAnimation(VisualElement element, SlideDirection direction, int delayMs = 0)
        {
            string slideClass = direction switch
            {
                SlideDirection.Left => "slide-in-left",
                SlideDirection.Right => "slide-in-right",
                SlideDirection.Up => "slide-in-up",
                _ => "slide-in-up"
            };
            
            element.AddToClassList(slideClass);
            element.schedule.Execute(() =>
            {
                element.AddToClassList("slide-visible");
            }).ExecuteLater(delayMs + 16);
        }
    }
}
#endif
