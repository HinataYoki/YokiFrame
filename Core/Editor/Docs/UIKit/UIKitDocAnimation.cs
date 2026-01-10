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
                Description = "UIKit 提供灵活的动画系统，支持内置动画、DOTween 动画和自定义动画。所有动画对象均通过对象池管理，使用完毕后需调用 Recycle() 归还。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "使用内置动画",
                        Code = @"// 创建淡入淡出动画（从对象池分配）
var fadeIn = UIAnimationFactory.CreateFadeIn(0.3f);
var fadeOut = UIAnimationFactory.CreateFadeOut(0.3f);

// 创建缩放动画（弹出/收缩效果）
var popIn = UIAnimationFactory.CreatePopIn(0.3f);
var popOut = UIAnimationFactory.CreatePopOut(0.3f);

// 创建滑入动画
var slideIn = UIAnimationFactory.CreateSlideInFromBottom(0.3f);

// 设置面板动画
panel.SetShowAnimation(fadeIn);
panel.SetHideAnimation(fadeOut);

// 播放动画（带回调）
fadeIn.Play(rectTransform, () => Debug.Log(""动画完成""));

// 使用完毕后归还到池（面板销毁时自动处理）
fadeIn.Recycle();
// 或使用工厂方法
UIAnimationFactory.Return(fadeIn);",
                        Explanation = "内置动画使用协程实现，无需额外依赖。安装 DOTween 后自动切换为 DOTween 实现。所有动画对象均池化复用，避免 GC。"
                    },
                    new()
                    {
                        Title = "DOTween 动画（自动检测）",
                        Code = @"// YokiFrame 会自动检测项目中是否安装了 DOTween
// 检测到后自动添加 YOKIFRAME_DOTWEEN_SUPPORT 宏定义

// 使用工厂方法，内部自动切换为 DOTween 实现
var fadeIn = UIAnimationFactory.CreateFadeIn(0.3f);
var popIn = UIAnimationFactory.CreatePopIn(0.3f);

panel.SetShowAnimation(fadeIn);

// DOTween 版本自动使用 SetLink 绑定生命周期
// 目标销毁时自动停止动画",
                        Explanation = "安装 DOTween 后，UIAnimationFactory 会自动使用 DOTween 实现，无需修改调用代码。"
                    },
                    new()
                    {
                        Title = "直接使用 DOTween 动画类",
                        Code = @"#if YOKIFRAME_DOTWEEN_SUPPORT
// 直接创建 DOTween 动画实例，可自定义缓动函数
// 使用对象池分配
var fadeAnim = SafePoolKit<DOTweenFadeAnimation>.Instance.Allocate()
    .Setup(duration: 0.3f, fromAlpha: 0, toAlpha: 1, Ease.OutCubic);

var scaleAnim = SafePoolKit<DOTweenScaleAnimation>.Instance.Allocate()
    .Setup(duration: 0.3f, fromScale: Vector3.zero, toScale: Vector3.one, Ease.OutBack);

// 组合动画
var showAnim = UIAnimationFactory.CreateParallel()
    .Add(fadeAnim)
    .Add(scaleAnim);

SetShowAnimation(showAnim);
#endif",
                        Explanation = "直接使用 DOTweenFadeAnimation、DOTweenScaleAnimation、DOTweenSlideAnimation 类可自定义缓动函数（Ease）。"
                    },
                    new()
                    {
                        Title = "组合动画",
                        Code = @"// 并行组合（同时播放）
var parallelAnim = UIAnimationFactory.CreateParallel()
    .Add(UIAnimationFactory.CreateFadeIn(0.3f))
    .Add(UIAnimationFactory.CreatePopIn(0.3f));

// 顺序组合（依次播放）
var sequenceAnim = UIAnimationFactory.CreateSequential()
    .Add(UIAnimationFactory.CreateFadeIn(0.2f))
    .Add(UIAnimationFactory.CreatePopIn(0.2f));

// 批量添加
var animations = new List<IUIAnimation> { fadeIn, popIn };
var composite = UIAnimationFactory.CreateParallel(animations);

panel.SetShowAnimation(parallelAnim);

// 组合动画归还时会自动归还子动画
parallelAnim.Recycle();",
                        Explanation = "组合动画可以创建复杂的入场/退场效果。CompositeAnimation 内部复用上下文对象，避免 GC。"
                    },
                    new()
                    {
                        Title = "异步动画（UniTask）",
                        Code = @"#if YOKIFRAME_UNITASK_SUPPORT
// 使用 UniTask 等待动画完成
await panel.ShowUniTaskAsync(destroyCancellationToken);
Debug.Log(""显示动画完成"");

await panel.HideUniTaskAsync(destroyCancellationToken);
Debug.Log(""隐藏动画完成"");

// 直接使用动画的异步方法
var fadeIn = UIAnimationFactory.CreateFadeIn();
await ((IUIAnimationUniTask)fadeIn).PlayUniTaskAsync(rectTransform, ct);
fadeIn.Recycle();
#endif",
                        Explanation = "UniTask 版本适合需要等待动画完成的场景。DOTween 动画使用 ToUniTask 扩展，自动处理取消。"
                    },
                    new()
                    {
                        Title = "零 GC 回调",
                        Code = @"// 使用带状态的回调避免闭包 GC
var context = new MyContext { Panel = panel, Data = data };

fadeIn.Play(rectTransform, static state =>
{
    var ctx = (MyContext)state;
    ctx.Panel.OnAnimationComplete(ctx.Data);
}, context);

// 静态 Lambda + state 参数避免闭包捕获",
                        Explanation = "IUIAnimation.Play 提供 Action<object> 重载，配合静态 Lambda 实现零 GC 回调。"
                    }
                }
            };
        }
    }
}
#endif
