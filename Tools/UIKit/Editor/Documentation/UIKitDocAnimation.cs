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
                Description = "UIKit 动画系统支持内置动画和 DOTween，所有动画对象池化复用。面板动画通过 Inspector 配置或代码设置。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "Inspector 配置动画（推荐）",
                        Code = @"// 在 UIPanel Inspector 中配置：
// - Show Animation Config: 显示动画配置
// - Hide Animation Config: 隐藏动画配置

// 支持的动画类型：
// - FadeAnimationConfig: 淡入淡出
// - ScaleAnimationConfig: 缩放
// - SlideAnimationConfig: 滑动

// 配置后自动在 Awake 中创建动画实例",
                        Explanation = "Inspector 配置是最简单的方式，支持可视化预览。"
                    },
                    new()
                    {
                        Title = "工厂方法创建动画",
                        Code = @"// 淡入淡出
var fadeIn = UIAnimationFactory.CreateFadeIn(0.3f);
var fadeOut = UIAnimationFactory.CreateFadeOut(0.3f);

// 缩放弹出
var popIn = UIAnimationFactory.CreatePopIn(0.3f);
var popOut = UIAnimationFactory.CreatePopOut(0.3f);

// 滑入
var slideIn = UIAnimationFactory.CreateSlideInFromBottom(0.3f);
var slideTop = UIAnimationFactory.CreateSlideInFromTop(0.3f);

// 播放动画
fadeIn.Play(rectTransform, () => Debug.Log(""完成""));

// 归还到池（面板销毁时自动处理）
fadeIn.Recycle();",
                        Explanation = "工厂方法自动检测 DOTween，有则用 DOTween 实现，否则用协程。"
                    },
                    new()
                    {
                        Title = "组合动画",
                        Code = @"// 并行（同时播放）
var parallel = UIAnimationFactory.CreateParallel()
    .Add(UIAnimationFactory.CreateFadeIn(0.3f))
    .Add(UIAnimationFactory.CreatePopIn(0.3f));

// 顺序（依次播放）
var sequence = UIAnimationFactory.CreateSequential()
    .Add(UIAnimationFactory.CreateFadeIn(0.2f))
    .Add(UIAnimationFactory.CreatePopIn(0.2f));

// 归还时自动归还子动画
parallel.Recycle();",
                        Explanation = "组合动画创建复杂入场/退场效果。"
                    },
                    new()
                    {
                        Title = "DOTween 自定义缓动",
                        Code = @"#if YOKIFRAME_DOTWEEN_SUPPORT
// 自定义缓动函数
var fade = SafePoolKit<DOTweenFadeAnimation>.Instance.Allocate()
    .Setup(0.3f, 0f, 1f, Ease.OutCubic);

var scale = SafePoolKit<DOTweenScaleAnimation>.Instance.Allocate()
    .Setup(0.3f, Vector3.zero, Vector3.one, Ease.OutBack);

// 组合
var anim = UIAnimationFactory.CreateParallel()
    .Add(fade)
    .Add(scale);
#endif",
                        Explanation = "直接使用 DOTween 动画类可自定义 Ease 缓动。"
                    },
                    new()
                    {
                        Title = "UniTask 异步等待",
                        Code = @"#if YOKIFRAME_UNITASK_SUPPORT
// 等待显示动画完成
await panel.ShowUniTaskAsync(destroyCancellationToken);

// 等待隐藏动画完成
await panel.HideUniTaskAsync(destroyCancellationToken);

// 直接使用动画异步方法
var anim = UIAnimationFactory.CreateFadeIn();
if (anim is IUIAnimationUniTask uniTaskAnim)
{
    await uniTaskAnim.PlayUniTaskAsync(rectTransform, ct);
}
anim.Recycle();
#endif",
                        Explanation = "UniTask 版本适合需要等待动画完成的场景。"
                    },
                    new()
                    {
                        Title = "零 GC 回调",
                        Code = @"// 使用静态 Lambda + state 避免闭包 GC
var ctx = new AnimContext { Panel = panel };

fadeIn.Play(rectTransform, static state =>
{
    var c = (AnimContext)state;
    c.Panel.OnAnimComplete();
}, ctx);",
                        Explanation = "IUIAnimation.Play 提供 Action<object> 重载，配合静态 Lambda 零 GC。"
                    }
                }
            };
        }
    }
}
#endif
