#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// 保存 Tool 程序集在运行时注册的交互 Provider，使 Core 宿主无需反向引用具体 Tool。
    /// </summary>
    public static class YokiFrameToolKitInteractionCatalog
    {
        private static readonly object sSyncRoot = new();
        private static readonly List<IYokiFrameKitInteractionProvider> sProviders = new(4);
        private static IYokiFrameKitInteractionProvider[] sSnapshot =
            Array.Empty<IYokiFrameKitInteractionProvider>();
        private static long sRevision;

        /// <summary>
        /// 获取 Provider 集合的单调版本；宿主仅在版本变化时重建 Registry 和命令策略。
        /// </summary>
        public static long Revision => Interlocked.Read(ref sRevision);

        /// <summary>
        /// 注册一个 Tool Provider；同一实例重复注册保持幂等，同一 Kit 的不同 owner 会被拒绝。
        /// </summary>
        /// <param name="provider">由具体 Tool 程序集持有的 Runtime Provider。</param>
        public static void Register(IYokiFrameKitInteractionProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            lock (sSyncRoot)
            {
                for (var index = 0; index < sProviders.Count; index++)
                {
                    IYokiFrameKitInteractionProvider registered = sProviders[index];
                    if (ReferenceEquals(registered, provider))
                    {
                        return;
                    }

                    if (string.Equals(registered.Kit, provider.Kit, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Tool Kit interaction provider is already registered: " + provider.Kit);
                    }
                }

                sProviders.Add(provider);
                sSnapshot = sProviders.ToArray();
                Interlocked.Increment(ref sRevision);
            }
        }

        /// <summary>
        /// 把当前稳定快照加入新 Registry；调用方应先创建 Core Provider，再追加 Tool Provider。
        /// </summary>
        /// <param name="registry">待填充的宿主 Registry。</param>
        /// <returns>与本次 Provider 快照严格对应的 catalog 版本。</returns>
        internal static long RegisterProviders(YokiFrameKitInteractionRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            IYokiFrameKitInteractionProvider[] snapshot;
            long revision;
            lock (sSyncRoot)
            {
                snapshot = sSnapshot;
                revision = sRevision;
            }

            for (var index = 0; index < snapshot.Length; index++)
            {
                registry.Register(snapshot[index]);
            }

            return revision;
        }
    }
}
#endif
