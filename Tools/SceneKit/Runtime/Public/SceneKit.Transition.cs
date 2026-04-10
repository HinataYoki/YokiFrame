using System;

namespace YokiFrame
{
    /// <summary>
    /// SceneKit - 场景切换过渡方法
    /// </summary>
    public static partial class SceneKit
    {
        #region 场景切换（带过渡效果）

        /// <summary>
        /// 异步切换场景（带过渡效果）
        /// </summary>
        /// <param name="sceneName">目标场景名称</param>
        /// <param name="transition">过渡效果，null 则直接切换</param>
        /// <param name="data">场景数据</param>
        /// <param name="onComplete">切换完成回调</param>
        public static void SwitchSceneAsync(string sceneName,
            ISceneTransition transition = null,
            ISceneData data = null,
            Action<SceneHandler> onComplete = null)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                KitLogger.Error("[SceneKit] 场景名称不能为空");
                onComplete?.Invoke(null);
                return;
            }

            // 检查是否正在过渡中
            if (sIsTransitioning)
            {
                KitLogger.Warning("[SceneKit] 场景切换正在进行中，请等待完成");
                onComplete?.Invoke(null);
                return;
            }

            sIsTransitioning = true;

            // 无过渡效果，直接加载
            if (transition == null)
            {
                LoadSceneAsync(sceneName, SceneLoadMode.Single, handler =>
                {
                    sIsTransitioning = false;
                    onComplete?.Invoke(handler);
                }, data: data);
                return;
            }

            // 有过渡效果，执行淡出 -> 加载 -> 淡入
            transition.FadeOutAsync(() =>
            {
                LoadSceneAsync(sceneName, SceneLoadMode.Single, handler =>
                {
                    transition.FadeInAsync(() =>
                    {
                        sIsTransitioning = false;
                        onComplete?.Invoke(handler);
                    });
                }, data: data);
            });
        }

        #endregion
    }
}
