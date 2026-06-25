#if GODOT
using Godot;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// Godot 启动器：注册 Godot 适配器并驱动基础服务。
    /// </summary>
    [Tool]
    [GlobalClass]
    public partial class GodotBootstrap : Node
    {
        private readonly GodotEngineTime mTimeProvider = new GodotEngineTime();
        private readonly GodotCommandBridgeHost mCommandBridgeHost = new GodotCommandBridgeHost();
        private static GodotBootstrap sInstance;

        [Export]
        private bool mEnableDebugLog = true;

        public IEngineLogger Logger { get; private set; }
        public IEngineTime TimeProvider { get; private set; }
        public IResourceProvider ResourceProvider { get; private set; }
        public ISerializationProvider SerializationProvider { get; private set; }

        public override void _Ready()
        {
            if (sInstance != null && !ReferenceEquals(sInstance, this))
            {
                QueueFree();
                return;
            }

            sInstance = this;
            GodotAutoBootstrap.EnsureAutoloadRegistered();
            InitializeAdapters();
            mCommandBridgeHost.EnsureInitialized();
            SetProcess(true);
        }

        public override void _Process(double delta)
        {
            mTimeProvider.UpdateFrameTime(delta);
            mCommandBridgeHost.Tick(delta);
            YokiFrameKit.Tick((float)delta);
        }

        private void InitializeAdapters()
        {
            YokiFrameKit.RegisterInstaller(new GodotCoreKitInstaller(mTimeProvider));
            var runtime = YokiFrameKit.Initialize(YokiFrameEngine.Godot);
            var context = runtime.Context;
            Logger = context.GetService<IEngineLogger>();
            TimeProvider = context.GetService<IEngineTime>();
            ResourceProvider = context.GetService<IResourceProvider>();
            SerializationProvider = context.GetService<ISerializationProvider>();

            EventKitErrorHandler.OnError = message =>
            {
                if (mEnableDebugLog)
                    LogKit.Error(message);
            };
        }

        public override void _ExitTree()
        {
            if (ReferenceEquals(sInstance, this))
                sInstance = null;

            EventKitErrorHandler.OnError = null;
            YokiFrameKit.Shutdown();
        }
    }
}
#endif
