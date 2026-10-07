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
        private static readonly object sProviderCreateLock = new();
        private static int sProviderCreateDepth;

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

        /// <summary>
        /// 返回当前 Provider。缺失时在状态锁外调用宿主工厂，提交前再次确认没有显式 Provider 或会话重置。
        /// 工厂不得重入 ResKit，否则抛出 InvalidOperationException。
        /// </summary>
        /// <returns>已安装的资源 Provider。</returns>
        private static IResourceProvider EnsureProvider()
        {
            lock (sProviderCreateLock)
            {
                while (true)
                {
                    IResourceProvider current = ReadInstalledProvider();
                    if (current != null)
                    {
                        return current;
                    }

                    if (!TryBeginProviderCreate(out Func<IResourceProvider> factory, out long generation))
                    {
                        continue;
                    }

                    try
                    {
                        IResourceProvider created = factory();
                        if (created == null)
                        {
                            throw new InvalidOperationException("The ResKit default provider factory returned null.");
                        }

                        current = CommitCreatedProvider(created, generation);
                    }
                    finally
                    {
                        EndProviderCreate();
                    }

                    if (current != null)
                    {
                        return current;
                    }
                }
            }
        }

        /// <summary>读取已经安装的 Provider；尚未创建时返回 null，不调用工厂。</summary>
        /// <returns>当前 Provider 或 null。</returns>
        private static IResourceProvider ReadInstalledProvider()
        {
            lock (sLock)
            {
                return sProvider;
            }
        }

        /// <summary>
        /// 在状态锁内占住一次默认工厂调用。已有 Provider 时返回 false，由调用方重新读取。
        /// </summary>
        /// <param name="factory">本次要在锁外调用的工厂。</param>
        /// <param name="generation">占位时的 Provider 代次，提交时用来识别会话重置。</param>
        /// <returns>需要调用工厂时返回 true。</returns>
        private static bool TryBeginProviderCreate(out Func<IResourceProvider> factory, out long generation)
        {
            factory = null;
            generation = 0;
            lock (sLock)
            {
                if (sProvider != null)
                {
                    return false;
                }

                if (sDefaultProviderFactory == null)
                {
                    throw new InvalidOperationException(
                        "ResKit provider is not configured. Install an engine adapter or call ResKit.SetProvider first.");
                }

                if (sProviderCreateDepth > 0)
                {
                    throw new InvalidOperationException(
                        "ResKit default provider factory must not reenter ResKit.");
                }

                sProviderCreateDepth = 1;
                factory = sDefaultProviderFactory;
                generation = sProviderGeneration;
                return true;
            }
        }

        /// <summary>结束一次工厂占位。必须在工厂返回、失败或重入拒绝后调用。</summary>
        private static void EndProviderCreate()
        {
            lock (sLock)
            {
                sProviderCreateDepth = 0;
            }
        }

        /// <summary>
        /// 提交锁外创建的 Provider。显式注册优先；代次变化或工厂被清空时放弃候选并让调用方重试。
        /// </summary>
        /// <param name="created">工厂返回的候选 Provider。</param>
        /// <param name="generation">开始创建时观察到的 Provider 代次。</param>
        /// <returns>最终安装的 Provider；本次结果已过期时返回 null。</returns>
        private static IResourceProvider CommitCreatedProvider(IResourceProvider created, long generation)
        {
#if UNITY_EDITOR || (GODOT && TOOLS)
            string providerName = ResolveProviderName(created);
#endif
            ResolveProviderCapabilities(created, out bool rawBytes, out bool rawText);
            lock (sLock)
            {
                if (sProvider != null)
                {
                    return sProvider;
                }

                if (sProviderGeneration != generation || sDefaultProviderFactory == null)
                {
                    return null;
                }

                InstallProviderLocked(created, rawBytes, rawText
#if UNITY_EDITOR || (GODOT && TOOLS)
                    , providerName
#endif
                    );
#if UNITY_EDITOR || (GODOT && TOOLS)
                BumpDiagnosticVersionLocked();
#endif
                return created;
            }
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
