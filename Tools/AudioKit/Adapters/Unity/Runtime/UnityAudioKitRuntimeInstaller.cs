#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>在 Unity 子系统代际开始时只注册 AudioSource 默认后端工厂。</summary>
    internal static class UnityAudioKitRuntimeInstaller
    {
        /// <summary>登记会话重置，并在工厂被清掉后恢复 Unity 默认后端工厂。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterSessionParticipants()
        {
            YokiFrameSession.Register(YokiFrameSession.RELEASE_HOSTS_ORDER, "audiokit", AudioKit.Reset);
            YokiFrameSession.Register(
                YokiFrameSession.RESTORE_FACTORIES_ORDER,
                "audiokit-unity-factory",
                RegisterUnityFactory);
            RegisterUnityFactory();
        }

        /// <summary>登记不会立即创建后端的 Unity AudioSource 工厂。</summary>
        private static void RegisterUnityFactory()
        {
            AudioKit.RegisterDefaultBackendFactory(static () => new UnityAudioKitBackend());
        }
    }
}
#endif
