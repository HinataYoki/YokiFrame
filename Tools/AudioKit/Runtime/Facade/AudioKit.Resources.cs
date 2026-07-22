using System;

namespace YokiFrame
{
    public static partial class AudioKit
    {
        /// <summary>获取当前资源加载器名称；读取不会创建音频后端或 ResKit Provider。</summary>
        public static string ResourceLoaderName => GetResourceLoader().LoaderName;

        /// <summary>设置原生音频后端使用的显式资源加载器。</summary>
        public static void SetResourceLoader(IAudioResourceLoader loader)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            lock (sLock) sResourceLoader = loader;
#if UNITY_EDITOR || (GODOT && TOOLS)
            BumpDiagnosticVersion();
#endif
        }

        /// <summary>清除显式加载器并恢复 ResKit 默认加载器。</summary>
        public static void ClearResourceLoader()
        {
            lock (sLock) sResourceLoader = null;
#if UNITY_EDITOR || (GODOT && TOOLS)
            BumpDiagnosticVersion();
#endif
        }

        /// <summary>获取显式加载器或共享 ResKit 默认加载器。</summary>
        public static IAudioResourceLoader GetResourceLoader()
        {
            lock (sLock) return sResourceLoader ?? ResKitAudioResourceLoader.Shared;
        }
    }
}
