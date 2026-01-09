#if YOKIFRAME_DOTWEEN_SUPPORT
using DG.Tweening;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// DOTween 预初始化器
    /// 在游戏启动时预初始化 DOTween，避免首次使用时的卡顿
    /// </summary>
    public static class DOTweenInitializer
    {
        /// <summary>
        /// 默认 Tweener 容量
        /// </summary>
        private const int DEFAULT_TWEENERS_CAPACITY = 200;
        
        /// <summary>
        /// 默认 Sequence 容量
        /// </summary>
        private const int DEFAULT_SEQUENCES_CAPACITY = 50;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // 预初始化 DOTween，设置合理的容量避免运行时扩容
            DOTween.Init(recycleAllByDefault: true, useSafeMode: false, logBehaviour: LogBehaviour.ErrorsOnly);
            DOTween.SetTweensCapacity(DEFAULT_TWEENERS_CAPACITY, DEFAULT_SEQUENCES_CAPACITY);
        }
    }
}
#endif
