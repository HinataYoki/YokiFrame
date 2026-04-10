#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SceneKit 场景切换（带过渡效果）文档
    /// </summary>
    internal static class SceneKitDocTransition
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "场景切换（带过渡效果）",
                Description = "支持淡入淡出等过渡效果的场景切换。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "淡入淡出切换",
                        Code = @"// 使用默认淡入淡出效果
var transition = new FadeTransition();
SceneKit.SwitchSceneAsync(""GameScene"", transition);

// 自定义淡入淡出参数
var transition = new FadeTransition(
    fadeDuration: 0.5f,      // 淡入淡出时长
    fadeColor: Color.black   // 淡入淡出颜色
);
SceneKit.SwitchSceneAsync(""GameScene"", transition);

// 带回调的切换
SceneKit.SwitchSceneAsync(""GameScene"", transition,
    onComplete: handler => Debug.Log(""切换完成""));"
                    },
                    new()
                    {
                        Title = "自定义过渡效果",
                        Code = @"// 实现自定义过渡效果
public class SlideTransition : ISceneTransition
{
    public float Progress { get; private set; }
    public bool IsTransitioning { get; private set; }

    public void FadeOutAsync(Action onComplete)
    {
        IsTransitioning = true;
        // 实现滑出动画...
        onComplete?.Invoke();
    }

    public void FadeInAsync(Action onComplete)
    {
        // 实现滑入动画...
        IsTransitioning = false;
        onComplete?.Invoke();
    }

    public void Dispose() { }
}

// 使用自定义过渡
SceneKit.SwitchSceneAsync(""GameScene"", new SlideTransition());"
                    }
                }
            };
        }
    }
}
#endif
