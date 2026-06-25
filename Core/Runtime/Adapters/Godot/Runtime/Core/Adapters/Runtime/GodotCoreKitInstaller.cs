#if GODOT
using Godot;

namespace YokiFrame.Godot
{
    /// <summary>
    /// Godot 宿主 Core 默认后端安装器。
    /// </summary>
    [YokiFrameKitDiscoverableInstaller(YokiFrameEngine.Godot)]
    public sealed class GodotCoreKitInstaller : IYokiFrameKitInstaller
    {
        private readonly GodotEngineTime mTimeProvider;

        public GodotCoreKitInstaller()
            : this(new GodotEngineTime())
        {
        }

        public GodotCoreKitInstaller(GodotEngineTime timeProvider)
        {
            mTimeProvider = timeProvider;
        }

        public string KitName
        {
            get { return "Godot.Core"; }
        }

        public void Install(YokiFrameEngineContext context)
        {
            if (context.Engine != YokiFrameEngine.Godot)
                return;

            var logger = new GodotEngineLogger();
            var resourceProvider = new GodotResourceProvider();
            var serializationProvider = new GodotEngineSerializationProvider();

            LogKit.SetLogger(logger);
            ResKit.SetProvider(resourceProvider);

            context.SetService<IEngineLogger>(logger);
            context.SetService<IEngineTime>(mTimeProvider);
            context.SetService<IResourceProvider>(resourceProvider);
            context.SetService<ISerializationProvider>(serializationProvider);

            if (GodotDependencyDefineService.RefreshDefines())
                LogKit.Warning("[YokiFrame] Godot dependency defines changed. Rebuild C# to apply optional Kit compile symbols.");
        }

        public bool Tick(float deltaSeconds)
        {
            return true;
        }

        public void Shutdown()
        {
            LogKit.ClearLogger();
        }
    }
}
#endif
