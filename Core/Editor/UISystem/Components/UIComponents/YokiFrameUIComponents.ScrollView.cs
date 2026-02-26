#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame UI 组件 - ScrollView 工具
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        /// <summary>
        /// 确保 ScrollView 滚动条可正常拖动
        /// </summary>
        public static void FixScrollViewDragger(ScrollView scrollView)
        {
            if (scrollView == null) return;
            
            // 多次尝试，确保在不同时机都能生效
            scrollView.schedule.Execute(() => ApplyScrollerFix(scrollView)).ExecuteLater(1);
            scrollView.schedule.Execute(() => ApplyScrollerFix(scrollView)).ExecuteLater(50);
            scrollView.schedule.Execute(() => ApplyScrollerFix(scrollView)).ExecuteLater(200);
            
            // 布局变化时再次应用
            scrollView.RegisterCallback<GeometryChangedEvent>(evt => ApplyScrollerFix(scrollView));
            
            // 附加到面板时应用
            scrollView.RegisterCallback<AttachToPanelEvent>(evt => 
            {
                scrollView.schedule.Execute(() => ApplyScrollerFix(scrollView)).ExecuteLater(1);
            });
        }
        
        /// <summary>
        /// 应用滚动条样式
        /// </summary>
        private static void ApplyScrollerFix(ScrollView scrollView)
        {
            FixDragContainer(scrollView.verticalScroller);
            FixDragContainer(scrollView.horizontalScroller);
        }
        
        /// <summary>
        /// 设置 Scroller 的 drag-container 尺寸
        /// </summary>
        private static void FixDragContainer(Scroller scroller)
        {
            if (scroller == null) return;
            
            var slider = scroller.slider;
            if (slider == null) return;
            
            var dragContainer = slider.Q(className: "unity-base-slider__drag-container");
            if (dragContainer == null) return;
            
            bool needsFix = false;
            
            // 垂直滚动条：设置宽度
            if (scroller.ClassListContains("unity-scroller--vertical"))
            {
                if (dragContainer.resolvedStyle.width <= 0)
                {
                    needsFix = true;
                    dragContainer.style.position = Position.Absolute;
                    dragContainer.style.left = 0;
                    dragContainer.style.right = 0;
                    dragContainer.style.top = 0;
                    dragContainer.style.bottom = 0;
                    dragContainer.style.width = Length.Percent(100);
                }
            }
            // 水平滚动条：设置高度
            else if (scroller.ClassListContains("unity-scroller--horizontal"))
            {
                if (dragContainer.resolvedStyle.height <= 0)
                {
                    needsFix = true;
                    dragContainer.style.position = Position.Absolute;
                    dragContainer.style.left = 0;
                    dragContainer.style.right = 0;
                    dragContainer.style.top = 0;
                    dragContainer.style.bottom = 0;
                    dragContainer.style.height = Length.Percent(100);
                }
            }
            
            if (needsFix)
            {
                var dragger = slider.Q(className: "unity-base-slider__dragger");
                if (dragger != null)
                {
                    if (scroller.ClassListContains("unity-scroller--vertical"))
                    {
                        dragger.style.minWidth = 8;
                        dragger.style.width = Length.Percent(100);
                    }
                    else
                    {
                        dragger.style.minHeight = 8;
                        dragger.style.height = Length.Percent(100);
                    }
                }
            }
        }
        
        /// <summary>
        /// 创建可正常拖动的 ScrollView
        /// </summary>
        public static ScrollView CreateScrollView(ScrollViewMode mode = ScrollViewMode.Vertical)
        {
            var scrollView = new ScrollView(mode);
            FixScrollViewDragger(scrollView);
            return scrollView;
        }
    }
}
#endif
