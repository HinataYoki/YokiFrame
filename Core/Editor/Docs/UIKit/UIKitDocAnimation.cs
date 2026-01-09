#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 动画系统文档
    /// </summary>
    internal static class UIKitDocAnimation
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "动画系统",
                Description = "UIKit 提供灵活的动画系统，支持内置动画、DOTween 动画和自定义动画。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "使用内置动画",
                        Code = @"// 创建淡入淡出动画
var fadeIn = UIAnimationFactory.CreateFadeIn(0.3f);
var fadeOut = UIAnimationFactory.CreateFadeOut(0.3f);

// 创建缩放动画
var scaleIn = UIAnimationFactory.CreateScaleIn(0.3f);
var scaleOut = UIAnimationFactory.CreateScaleOut(0.3f);

// 设置面板动画
panel.SetShowAnimation(fadeIn);
panel.SetHideAnimation(fadeOut);",
                        Explanation = "内置动画使用协程实现，无需额外依赖。"
                    },
                    new()
                    {
                        Title = "使用 DOTween 动画",
                        Code = @"// 需要定义 YOKIFRAME_DOTWEEN_SUPPORT 宏
var fadeIn = UIAnimationFactory.CreateDOTweenFadeIn(0.3f, Ease.OutQuad);
var scaleIn = UIAnimationFactory.CreateDOTweenScaleIn(0.3f, Ease.OutBack);

panel.SetShowAnimation(fadeIn);",
                        Explanation = "DOTween 动画支持更多缓动函数，性能更优。"
                    },
                    new()
                    {
                        Title = "组合动画",
                        Code = @"// 并行组合（同时播放）
var parallelAnim = UIAnimationFactory.CreateParallel(
    UIAnimationFactory.CreateFadeIn(0.3f),
    UIAnimationFactory.CreateScaleIn(0.3f)
);

// 顺序组合（依次播放）
var sequenceAnim = UIAnimationFactory.CreateSequence(
    UIAnimationFactory.CreateFadeIn(0.2f),
    UIAnimationFactory.CreateScaleIn(0.2f)
);

panel.SetShowAnimation(parallelAnim);",
                        Explanation = "组合动画可以创建复杂的入场/退场效果。"
                    },
                    new()
                    {
                        Title = "异步动画（UniTask）",
                        Code = @"// 使用 UniTask 等待动画完成
await panel.ShowUniTaskAsync();
Debug.Log(""显示动画完成"");

await panel.HideUniTaskAsync();
Debug.Log(""隐藏动画完成"");",
                        Explanation = "UniTask 版本适合需要等待动画完成的场景。"
                    }
                }
            };
        }
    }
}
#endif
