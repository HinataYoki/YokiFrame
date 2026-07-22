using System;

namespace YokiFrame
{
    public static partial class ResKit
    {
        /// <summary>
        /// 获取当前 Provider 的场景能力；首次调用会按宿主默认工厂惰性创建 Provider。
        /// </summary>
        /// <returns>当前 Provider 的场景能力。</returns>
        /// <exception cref="NotSupportedException">当前 Provider 不支持场景加载时抛出。</exception>
        public static IResSceneProvider GetSceneProvider()
        {
            IResourceProvider provider;
            lock (sLock)
            {
                provider = EnsureProviderLocked();
            }

            IResSceneProvider sceneProvider = provider as IResSceneProvider;
            if (sceneProvider != null)
            {
                return sceneProvider;
            }

            string providerName = provider.GetType().FullName ?? provider.GetType().Name;
            throw new NotSupportedException(
                "ResKit provider '" + providerName + "' does not support scene loading.");
        }

        /// <summary>
        /// 尝试读取当前 Provider 的场景能力，不触发默认 Provider 创建。
        /// </summary>
        /// <returns>当前场景能力；Provider 尚未创建或不支持时返回空。</returns>
        public static IResSceneProvider TryGetSceneProvider()
        {
            lock (sLock)
            {
                return sProvider as IResSceneProvider;
            }
        }
    }
}
