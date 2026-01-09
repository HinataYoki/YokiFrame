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

// 创建缩放动画（弹出/收缩效果）
var popIn = UIAnimationFactory.CreatePopIn(0.3f);
var popOut = UIAnimationFactory.CreatePopOut(0.3f);

// 创建滑入动画
var slideIn = UIAnimationFactory.CreateSlideInFromBottom(0.3f);

// 设置面板动画
panel.SetShowAnimation(fadeIn);
panel.SetHideAnimation(fadeOut);",
                        Explanation = "内置动画使用协程实现，无需额外依赖。安装 DOTween 后自动切换为 DOTween 实现。"
                    },
                    new()
                    {
                        Title = "DOTween 动画（自动检测）",
                        Code = @"// YokiFrame 会自动检测项目中是否安装了 DOTween
// 检测到后自动添加 YOKIFRAME_DOTWEEN_SUPPORT 宏定义

// 使用工厂方法，内部自动切换为 DOTween 实现
var fadeIn = UIAnimationFactory.CreateFadeIn(0.3f);
var popIn = UIAnimationFactory.CreatePopIn(0.3f);

panel.SetShowAnimation(fadeIn);",
                        Explanation = "安装 DOTween 后，UIAnimationFactory 会自动使用 DOTween 实现，无需修改调用代码。"
                    },
                    new()
                    {
                        Title = "直接使用 DOTween 动画类",
                        Code = @"// 直接创建 DOTween 动画实例，可自定义缓动函数
var showAnim = UIAnimationFactory.CreateParallel(
    new DOTweenFadeAnimation(duration: 0.3f, fromAlpha: 0, toAlpha: 1, Ease.OutCubic),
    new DOTweenScaleAnimation(duration: 0.3f, fromScale: Vector3.zero, toScale: Vector3.one, Ease.OutCubic)
);

var hideAnim = UIAnimationFactory.CreateParallel(
    new DOTweenFadeAnimation(duration: 0.3f, fromAlpha: 1, toAlpha: 0, Ease.OutCubic),
    new DOTweenScaleAnimation(duration: 0.3f, fromScale: Vector3.one, toScale: Vector3.zero, Ease.OutCubic)
);

SetShowAnimation(showAnim);
SetHideAnimation(hideAnim);",
                        Explanation = "直接使用 DOTweenFadeAnimation、DOTweenScaleAnimation、DOTweenSlideAnimation 类可自定义缓动函数（Ease）。"
                    },
                    new()
                    {
                        Title = "组合动画",
                        Code = @"// 并行组合（同时播放）
var parallelAnim = UIAnimationFactory.CreateParallel(
    UIAnimationFactory.CreateFadeIn(0.3f),
    UIAnimationFactory.CreatePopIn(0.3f)
);

// 顺序组合（依次播放）
var sequenceAnim = UIAnimationFactory.CreateSequential(
    UIAnimationFactory.CreateFadeIn(0.2f),
    UIAnimationFactory.CreatePopIn(0.2f)
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
