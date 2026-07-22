#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 组合当前宿主实际启用的 Kit Provider，并为 FileBridge 提供统一 Snapshot 与 Command 路由。
    /// </summary>
    public sealed class YokiFrameKitInteractionRegistry : IYokiFrameCommandHandler
    {
        private readonly List<IYokiFrameKitInteractionProvider> mProviders = new();
        private IYokiFrameKitInteractionProvider[] mProviderSnapshot = Array.Empty<IYokiFrameKitInteractionProvider>();
        private IReadOnlyList<IYokiFrameKitInteractionProvider> mProviderView =
            Array.AsReadOnly(Array.Empty<IYokiFrameKitInteractionProvider>());

        /// <summary>
        /// 获取当前已注册 Provider 的稳定只读视图；只在注册发生时重建，不在状态刷新热路径分配。
        /// </summary>
        public IReadOnlyList<IYokiFrameKitInteractionProvider> Providers => mProviderView;

        /// <summary>
        /// 注册一个 Kit Provider；同一个 Kit 只能有一个 Runtime 事实 owner。
        /// </summary>
        /// <param name="provider">待注册 Provider。</param>
        public void Register(IYokiFrameKitInteractionProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            ValidateProvider(provider);
            if (FindProvider(provider.Kit) != null)
            {
                throw new ArgumentException("Kit interaction provider is already registered: " + provider.Kit, nameof(provider));
            }

            mProviders.Add(provider);
            RebuildProviderView();
        }

        /// <summary>
        /// 聚合全部 Provider 当前声明的 Command，供宿主 CommandPolicy 和实时目录使用。
        /// </summary>
        /// <returns>独立 Command descriptor 数组。</returns>
        public YokiFrameCommandDescriptor[] GetCommandDescriptors()
        {
            var count = 0;
            for (var providerIndex = 0; providerIndex < mProviderSnapshot.Length; providerIndex++)
            {
                count += mProviderSnapshot[providerIndex].Commands.Count;
            }

            YokiFrameCommandDescriptor[] commands = new YokiFrameCommandDescriptor[count];
            var commandIndex = 0;
            for (var providerIndex = 0; providerIndex < mProviderSnapshot.Length; providerIndex++)
            {
                IReadOnlyList<YokiFrameCommandDescriptor> source = mProviderSnapshot[providerIndex].Commands;
                for (var sourceIndex = 0; sourceIndex < source.Count; sourceIndex++)
                {
                    commands[commandIndex++] = source[sourceIndex];
                }
            }

            return commands;
        }

        /// <summary>
        /// 尝试创建指定 Kit/Snapshot 的 payload；未注册或未声明时不伪造在线占位。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="snapshotName">Snapshot 名称。</param>
        /// <param name="payloadJson">成功时返回 Kit payload。</param>
        /// <returns>当前 Runtime 确实支持该 Snapshot 时返回 true。</returns>
        public bool TryCreateSnapshot(string kit, string snapshotName, out string payloadJson)
        {
            var provider = FindProvider(kit);
            if (provider == null || !Contains(provider.SnapshotNames, snapshotName))
            {
                payloadJson = null;
                return false;
            }

            payloadJson = provider.CreateSnapshot(snapshotName);
            return true;
        }

        /// <summary>判断任一已注册 Provider 是否处理指定命令。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>存在匹配 Provider 时返回 true。</returns>
        public bool CanHandle(YokiFrameCommandRequest request)
        {
            return FindCommandProvider(request) != null;
        }

        /// <summary>把命令交给唯一匹配的 Kit Provider。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>Provider 终态结果；缺失匹配时返回稳定错误。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            var provider = FindCommandProvider(request);
            return provider == null
                ? YokiFrameCommandResult.Error("HandlerMismatch", "No Kit interaction provider supports this command.")
                : provider.Handle(request);
        }

        /// <summary>校验 Provider 的 Kit、Snapshot 和 Command 声明保持同一所有权。</summary>
        /// <param name="provider">待校验 Provider。</param>
        private static void ValidateProvider(IYokiFrameKitInteractionProvider provider)
        {
            if (!YokiFrameSafeIdContract.IsSafeId(provider.Kit))
            {
                throw new ArgumentException("Kit interaction provider has an invalid Kit id.", nameof(provider));
            }

            ValidateSnapshotNames(provider);
            ValidateCommands(provider);
        }

        /// <summary>校验 Snapshot 名称安全且不重复。</summary>
        /// <param name="provider">待校验 Provider。</param>
        private static void ValidateSnapshotNames(IYokiFrameKitInteractionProvider provider)
        {
            IReadOnlyList<string> names = provider.SnapshotNames
                ?? throw new ArgumentException("Kit interaction provider SnapshotNames cannot be null.", nameof(provider));
            for (var index = 0; index < names.Count; index++)
            {
                if (!YokiFrameSafeIdContract.IsSafeId(names[index]) || ContainsBefore(names, names[index], index))
                {
                    throw new ArgumentException("Kit interaction provider has an invalid or duplicate Snapshot name.", nameof(provider));
                }
            }
        }

        /// <summary>校验 Command 均属于当前 Kit，避免跨 Kit 劫持命令。</summary>
        /// <param name="provider">待校验 Provider。</param>
        private static void ValidateCommands(IYokiFrameKitInteractionProvider provider)
        {
            IReadOnlyList<YokiFrameCommandDescriptor> commands = provider.Commands
                ?? throw new ArgumentException("Kit interaction provider Commands cannot be null.", nameof(provider));
            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index];
                if (command == null || !string.Equals(command.Kit, provider.Kit, StringComparison.Ordinal))
                {
                    throw new ArgumentException("Kit interaction provider Command belongs to another Kit.", nameof(provider));
                }
            }
        }

        /// <summary>重建只读 Provider 快照，避免读取路径遍历可变 List。</summary>
        private void RebuildProviderView()
        {
            mProviderSnapshot = mProviders.ToArray();
            mProviderView = Array.AsReadOnly(mProviderSnapshot);
        }

        /// <summary>按稳定 Kit 标识查找 Provider。</summary>
        /// <param name="kit">Kit 标识。</param>
        /// <returns>匹配 Provider；不存在时返回 null。</returns>
        private IYokiFrameKitInteractionProvider FindProvider(string kit)
        {
            for (var index = 0; index < mProviderSnapshot.Length; index++)
            {
                var provider = mProviderSnapshot[index];
                if (string.Equals(provider.Kit, kit, StringComparison.Ordinal))
                {
                    return provider;
                }
            }

            return null;
        }

        /// <summary>查找首个声明可处理当前命令的 Provider。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>匹配 Provider；请求为空或未匹配时返回 null。</returns>
        private IYokiFrameKitInteractionProvider FindCommandProvider(YokiFrameCommandRequest request)
        {
            if (request == null)
            {
                return null;
            }

            for (var index = 0; index < mProviderSnapshot.Length; index++)
            {
                var provider = mProviderSnapshot[index];
                if (provider.CanHandle(request))
                {
                    return provider;
                }
            }

            return null;
        }

        /// <summary>判断只读名称集合是否包含目标值。</summary>
        /// <param name="values">名称集合。</param>
        /// <param name="value">目标值。</param>
        /// <returns>存在完全匹配项时返回 true。</returns>
        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (var index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>判断目标值是否已经在当前位置之前出现。</summary>
        /// <param name="values">名称集合。</param>
        /// <param name="value">目标值。</param>
        /// <param name="exclusiveEnd">不包含的结束位置。</param>
        /// <returns>此前已经出现时返回 true。</returns>
        private static bool ContainsBefore(IReadOnlyList<string> values, string value, int exclusiveEnd)
        {
            for (var index = 0; index < exclusiveEnd; index++)
            {
                if (string.Equals(values[index], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
