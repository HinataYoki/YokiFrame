#if !GODOT
namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity 宿主 Core 默认后端安装器。
    /// </summary>
    [YokiFrameKitDiscoverableInstaller(YokiFrameEngine.Unity)]
    public sealed class UnityCoreKitInstaller : IYokiFrameKitInstaller
    {
        private readonly UnityLogKitOptions mLogKitOptions;
        private readonly IEngineLogger mLogger;

        public UnityCoreKitInstaller()
            : this(UnityLogKitOptions.CreateDefault(), null)
        {
        }

        public UnityCoreKitInstaller(UnityLogKitOptions logKitOptions, IEngineLogger logger)
        {
            mLogKitOptions = logKitOptions;
            mLogger = logger;
        }

        public string KitName
        {
            get { return "Unity.Core"; }
        }

        public void Install(YokiFrameEngineContext context)
        {
            if (context.Engine != YokiFrameEngine.Unity)
                return;

            var logger = mLogger ?? new UnityEngineLogger();
            UnityRuntimeSettingsBridge.EnsureInstalled();
            UnityLogKitRuntimeInstaller.Install(UnityRuntimeSettingsBridge.GetLogKitOptions(mLogKitOptions), logger);

            var timeProvider = new UnityEngineTime();
            var resourceProvider = new UnityResourceProvider();
            var serializationProvider = new UnityEngineSerializationProvider();

            context.SetService<IEngineLogger>(logger);
            context.SetService<IEngineTime>(timeProvider);
            context.SetService<IResourceProvider>(resourceProvider);
            context.SetService<ISerializationProvider>(serializationProvider);

            ResKit.SetProvider(resourceProvider);
        }

        public bool Tick(float deltaSeconds)
        {
            return true;
        }

        public void Shutdown()
        {
            UnityLogKitRuntimeInstaller.Shutdown();
        }
    }
}
#endif
