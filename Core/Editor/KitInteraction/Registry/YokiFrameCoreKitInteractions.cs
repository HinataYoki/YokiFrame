#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>
    /// 作为 Core Kit 的唯一交互组合根；新增 Kit 只有完成完整交互切片后才能在此注册。
    /// </summary>
    public static class YokiFrameCoreKitInteractions
    {
        /// <summary>
        /// 创建当前版本已经真实可交互的 Core Kit Registry。
        /// </summary>
        /// <returns>包含已完成 Provider 的新 Registry。</returns>
        public static YokiFrameKitInteractionRegistry CreateDefault()
        {
            return CreateDefault(out _);
        }

        /// <summary>
        /// 创建当前 Registry，并返回与其中 Tool Provider 快照严格对应的 catalog 版本。
        /// </summary>
        /// <param name="toolProviderRevision">本次 Registry 捕获的 Tool Provider 版本。</param>
        /// <returns>包含已完成 Core 与 Tool Provider 的新 Registry。</returns>
        public static YokiFrameKitInteractionRegistry CreateDefault(out long toolProviderRevision)
        {
            YokiFrameKitInteractionRegistry registry = new();
            registry.Register(new ArchitectureInteractionProvider());
            registry.Register(new EventKitInteractionProvider());
            registry.Register(new FsmKitInteractionProvider());
            registry.Register(new LogKitInteractionProvider());
            registry.Register(new PoolKitInteractionProvider());
            registry.Register(new ResKitInteractionProvider());
            toolProviderRevision = YokiFrameToolKitInteractionCatalog.RegisterProviders(registry);
            return registry;
        }
    }
}
#endif
