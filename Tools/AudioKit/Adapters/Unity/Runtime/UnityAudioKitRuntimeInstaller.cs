#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>在 Unity 子系统代际开始时只注册 AudioSource 默认后端工厂。</summary>
    internal static class UnityAudioKitRuntimeInstaller
    {
        /// <summary>清理上一代静态状态并注册惰性 Unity 后端工厂。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAndRegisterDefaultBackendFactory()
        {
            AudioKit.ResetRuntimeDefaults();
            AudioKit.RegisterDefaultBackendFactory(static () => new UnityAudioKitBackend());
        }
    }
}
#endif
