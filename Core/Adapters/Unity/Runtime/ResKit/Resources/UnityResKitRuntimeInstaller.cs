#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 注册 Unity Resources 默认 Provider 工厂，并在新子系统会话开始时清理旧静态资源状态。
    /// </summary>
    public static class UnityResKitRuntimeInstaller
    {
        /// <summary>
        /// 获取 ResKit 当前是否已经按需创建 Unity Resources 默认 Provider。
        /// </summary>
        public static bool IsInstalled
        {
            get { return ResKit.GetProvider() is UnityResourceProvider; }
        }

        /// <summary>
        /// 注册 Unity Resources 默认 Provider 工厂；不会创建或覆盖当前 Provider。
        /// </summary>
        public static void RegisterDefaultProviderFactory()
        {
            ResKit.RegisterDefaultProviderFactory(CreateDefaultProvider);
        }

        /// <summary>
        /// 在进入新 Player 子系统时清理上一会话静态状态，并重新注册默认 Provider 工厂。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAndRegisterDefaultProviderFactory()
        {
            ResKit.ResetRuntimeDefaults();
            RegisterDefaultProviderFactory();
        }

        /// <summary>仅在 ResKit 首次真实资源调用时构造 Unity Resources Provider。</summary>
        /// <returns>新的 Unity Resources Provider。</returns>
        private static IResourceProvider CreateDefaultProvider()
        {
            return new UnityResourceProvider();
        }
    }
}
#endif
