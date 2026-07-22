using System;

namespace YokiFrame
{
    public static partial class ResKit
    {
        private static Func<IResourceProvider> sDefaultProviderFactory;
        private static IResourceProvider sProvider;
#if UNITY_EDITOR || (GODOT && TOOLS)
        private static string sProviderName = NO_PROVIDER_NAME;
#endif
        private static bool sSupportsRawBytes;
        private static bool sSupportsRawText;
        private static long sProviderGeneration;

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>获取当前 Provider 的稳定展示名；未创建时返回 None，读取不会触发默认创建。</summary>
        public static string ProviderName
        {
            get
            {
                lock (sLock)
                {
                    return sProviderName;
                }
            }
        }
#endif

        /// <summary>显式替换当前 Provider，并清理旧代次的全部缓存和在途加载。</summary>
        /// <param name="provider">新的宿主资源 Provider。</param>
        /// <exception cref="ArgumentNullException">Provider 为空时抛出。</exception>
        /// <exception cref="AggregateException">旧资源释放或取消回调发生一个或多个异常时抛出。</exception>
        public static void SetProvider(IResourceProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            string providerName = ResolveProviderName(provider);
#endif
            ResolveProviderCapabilities(provider, out bool rawBytes, out bool rawText);
            DetachedState detached;
            lock (sLock)
            {
                InstallProviderLocked(provider, rawBytes, rawText
#if UNITY_EDITOR || (GODOT && TOOLS)
                    , providerName
#endif
                    );
                sCacheEpoch++;
                detached = DetachStateLocked("ResKit provider changed before the load completed.");
            }

            try
            {
                ExecuteDetachedCleanup(detached);
            }
            catch (AggregateException exception)
            {
                RecordBackgroundFailure(exception);
                throw;
            }
        }

        /// <summary>获取当前 Provider；尚未发生资源调用且未显式设置时返回 null。</summary>
        /// <returns>当前资源 Provider 或 null。</returns>
        public static IResourceProvider GetProvider()
        {
            lock (sLock)
            {
                return sProvider;
            }
        }

        /// <summary>注册宿主默认 Provider 工厂；注册本身不会创建或替换当前 Provider。</summary>
        /// <param name="factory">只负责构造宿主默认 Provider、且不得重入 ResKit 的工厂。</param>
        internal static void RegisterDefaultProviderFactory(Func<IResourceProvider> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            lock (sLock)
            {
                sDefaultProviderFactory = factory;
            }
        }

        /// <summary>返回当前 Provider；为空时在状态锁内调用宿主工厂并只安装一次。</summary>
        private static IResourceProvider EnsureProviderLocked()
        {
            if (sProvider != null)
            {
                return sProvider;
            }

            if (sDefaultProviderFactory == null)
            {
                throw new InvalidOperationException(
                    "ResKit provider is not configured. Install an engine adapter or call ResKit.SetProvider first.");
            }

            IResourceProvider provider = sDefaultProviderFactory();
            if (provider == null)
            {
                throw new InvalidOperationException("The ResKit default provider factory returned null.");
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            string providerName = ResolveProviderName(provider);
#endif
            ResolveProviderCapabilities(provider, out bool rawBytes, out bool rawText);
            InstallProviderLocked(provider, rawBytes, rawText
#if UNITY_EDITOR || (GODOT && TOOLS)
                , providerName
#endif
                );
#if UNITY_EDITOR || (GODOT && TOOLS)
            BumpDiagnosticVersionLocked();
#endif
            return provider;
        }

        /// <summary>在状态锁内发布已经验证的 Provider 元数据并推进 Provider 代次。</summary>
        private static void InstallProviderLocked(
            IResourceProvider provider,
            bool rawBytes,
            bool rawText
#if UNITY_EDITOR || (GODOT && TOOLS)
            , string providerName
#endif
            )
        {
            sProvider = provider;
#if UNITY_EDITOR || (GODOT && TOOLS)
            sProviderName = providerName;
#endif
            sSupportsRawBytes = rawBytes;
            sSupportsRawText = rawText;
            sProviderGeneration++;
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>安全解析 Provider 名称，诊断属性异常时回退到完整类型名。</summary>
        private static string ResolveProviderName(IResourceProvider provider)
        {
            Type providerType = provider.GetType();
            string fallbackName = providerType.FullName ?? providerType.Name;
            try
            {
                string name = provider.ProviderName;
                return string.IsNullOrEmpty(name) ? fallbackName : name;
            }
            catch (Exception)
            {
                return fallbackName;
            }
        }
#endif

        /// <summary>读取可选能力；能力查询异常时退回 raw 接口的保守声明。</summary>
        private static void ResolveProviderCapabilities(
            IResourceProvider provider,
            out bool rawBytes,
            out bool rawText)
        {
            bool hasRaw = provider is IRawResourceProvider;
            rawBytes = hasRaw;
            rawText = hasRaw;
            if (!(provider is IResourceProviderCapabilities capabilities)) return;
            try
            {
                rawBytes = capabilities.SupportsRawBytes;
                rawText = capabilities.SupportsRawText;
            }
            catch (Exception)
            {
                rawBytes = hasRaw;
                rawText = hasRaw;
            }
        }
    }
}
