#if UNITY_5_3_OR_NEWER
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 在 Unity 完成全部 SubsystemRegistration 之后换代一次运行会话。
    /// 各个 Kit 只能在更早的阶段登记重置，不能再各自直接清理。
    /// </summary>
    internal static class UnityYokiFrameSessionHost
    {
        /// <summary>
        /// 进入 Play Mode 或程序集加载后的统一会话边界。
        /// 关闭 Domain Reload 时静态字段会留下来，这一次调用负责换掉上一会话。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void BeginSession()
        {
            YokiFrameSession.Begin();
        }
    }
}
#endif
