#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 高亮指示器组件 - 提供平滑移动的选中高亮效果
    /// </summary>
    public class HighlightIndicator
    {
        private readonly VisualElement mIndicator;
        private readonly VisualElement mContainer;
        private VisualElement mSelectedItem;
        
        /// <summary>
        /// 当前选中的项
        /// </summary>
        public VisualElement SelectedItem => mSelectedItem;
        
        /// <summary>
        /// 高亮指示器元素
        /// </summary>
        public VisualElement Element => mIndicator;
        
        /// <summary>
        /// 创建高亮指示器
        /// </summary>
        /// <param name="container">指示器所在的容器（Position.Relative）</param>
        /// <param name="backgroundColor">高亮背景色</param>
        /// <param name="borderRadius">圆角半径</param>
        public HighlightIndicator(VisualElement container, Color? backgroundColor = null, float borderRadius = 6f)
        {
            mContainer = container;
            
            mIndicator = new VisualElement();
            mIndicator.style.position = Position.Absolute;
            mIndicator.style.backgroundColor = new StyleColor(backgroundColor ?? new Color(0.22f, 0.22f, 0.25f));
            mIndicator.style.borderTopLeftRadius = borderRadius;
            mIndicator.style.borderTopRightRadius = borderRadius;
            mIndicator.style.borderBottomLeftRadius = borderRadius;
            mIndicator.style.borderBottomRightRadius = borderRadius;
            mIndicator.style.opacity = 0;
            mIndicator.pickingMode = PickingMode.Ignore;
            
            // 过渡动画
            mIndicator.style.transitionProperty = new List<StylePropertyName>
            {
                new("top"),
                new("left"),
                new("width"),
                new("height"),
                new("opacity"),
                new("background-color")
            };
            mIndicator.style.transitionDuration = new List<TimeValue>
            {
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond),
                new(150, TimeUnit.Millisecond),
                new(200, TimeUnit.Millisecond)
            };
            mIndicator.style.transitionTimingFunction = new List<EasingFunction>
            {
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut),
                new(EasingMode.EaseOut)
            };
            
            container.Add(mIndicator);
        }
        
        /// <summary>
        /// 移动高亮到目标项
        /// </summary>
        public void MoveTo(VisualElement targetItem)
        {
            if (targetItem == null || mContainer == null) return;
            
            mSelectedItem = targetItem;
            
            var targetRect = targetItem.worldBound;
            var containerRect = mContainer.worldBound;
            
            float relativeTop = targetRect.y - containerRect.y;
            float relativeLeft = targetRect.x - containerRect.x;
            
            mIndicator.style.top = relativeTop;
            mIndicator.style.left = relativeLeft;
            mIndicator.style.width = targetRect.width;
            mIndicator.style.height = targetRect.height;
            mIndicator.style.opacity = 1;
        }
        
        /// <summary>
        /// 延迟移动高亮到目标项（等待布局更新）
        /// </summary>
        public void MoveToDelayed(VisualElement targetItem, int delayMs = 1)
        {
            if (targetItem == null) return;
            targetItem.schedule.Execute(() => MoveTo(targetItem)).ExecuteLater(delayMs);
        }
        
        /// <summary>
        /// 刷新当前选中项的位置（用于滚动或窗口大小改变后）
        /// </summary>
        public void Refresh()
        {
            if (mSelectedItem != null)
            {
                MoveTo(mSelectedItem);
            }
        }
        
        /// <summary>
        /// 延迟刷新当前选中项的位置
        /// </summary>
        public void RefreshDelayed(int delayMs = 50)
        {
            if (mSelectedItem != null)
            {
                mSelectedItem.schedule.Execute(Refresh).ExecuteLater(delayMs);
            }
        }
        
        /// <summary>
        /// 隐藏高亮
        /// </summary>
        public void Hide()
        {
            mIndicator.style.opacity = 0;
        }
        
        /// <summary>
        /// 设置背景色
        /// </summary>
        public void SetBackgroundColor(Color color)
        {
            mIndicator.style.backgroundColor = new StyleColor(color);
        }
        
        /// <summary>
        /// 清除选中状态
        /// </summary>
        public void Clear()
        {
            mSelectedItem = null;
            Hide();
        }
    }
}
#endif
