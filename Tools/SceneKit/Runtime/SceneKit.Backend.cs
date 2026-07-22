using System;

namespace YokiFrame
{
    public static partial class SceneKit
    {
        /// <summary>获取显式后端；否则让首次真实场景调用创建 ResKit 默认 Provider。</summary>
        private static ISceneBackend EnsureBackend()
        {
            if (sExplicitBackend != null)
            {
                return sExplicitBackend;
            }

            return ResolveDefaultBackend(ResKit.GetSceneProvider());
        }

        /// <summary>复用绑定当前 ResKit 场景 Provider 的适配器，Provider 变化时重新创建。</summary>
        private static ISceneBackend ResolveDefaultBackend(IResSceneProvider provider)
        {
            if (!ReferenceEquals(provider, sDefaultProvider))
            {
                sDefaultProvider = provider;
                sDefaultBackend = new ResKitSceneBackendAdapter(provider);
            }

            return sDefaultBackend;
        }
    }
}
