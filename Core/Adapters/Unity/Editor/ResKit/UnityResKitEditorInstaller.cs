#if UNITY_EDITOR && UNITY_2022_3_OR_NEWER

using UnityEditor;

namespace YokiFrame.Unity
{
    /// <summary>在主 Unity Editor 域加载后注册 Resources 默认 Provider 工厂。</summary>
    internal static class UnityResKitEditorInstaller
    {
        /// <summary>跳过 AssetImportWorker；注册动作不会创建或覆盖当前 Provider。</summary>
        [InitializeOnLoadMethod]
        private static void RegisterDefaultProviderFactory()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            UnityResKitRuntimeInstaller.RegisterDefaultProviderFactory();
        }
    }
}

#endif
