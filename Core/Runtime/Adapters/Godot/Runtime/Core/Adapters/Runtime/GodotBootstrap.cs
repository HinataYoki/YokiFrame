#if GODOT
using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
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
        private readonly List<Func<float, bool>> mOptionalKitTicks = new List<Func<float, bool>>();
        private GodotAudioKitBackend mAudioBackend;
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
            AudioKit.Update((float)delta);
            TickOptionalKits((float)delta);
        }

        private void InitializeAdapters()
        {
            Logger = new GodotEngineLogger();
            LogKit.SetLogger(Logger);
            TimeProvider = mTimeProvider;
            ResourceProvider = new GodotResourceProvider();
            SerializationProvider = new GodotEngineSerializationProvider();
            ResKit.SetProvider(ResourceProvider);
            AudioKit.SetBackend(new GodotAudioKitBackend());
            mAudioBackend = AudioKit.GetBackend() as GodotAudioKitBackend;
            AudioKit.SetResourceLoader(new ResourceProviderAudioResourceLoader(ResourceProvider));
            if (GodotDependencyDefineService.RefreshDefines() && mEnableDebugLog)
                LogKit.Warning("[YokiFrame] Godot dependency defines changed. Rebuild C# to apply optional Kit compile symbols.");

            RegisterOptionalKitTick(InstallOptionalKitAdapter("YokiFrame.Godot.GodotActionKitInstaller", ResourceProvider));
            RegisterOptionalKitTick(InstallOptionalKitAdapter("YokiFrame.Godot.GodotSceneKitInstaller", ResourceProvider));
            RegisterOptionalKitTick(InstallOptionalKitAdapter("YokiFrame.Godot.GodotInputKitInstaller", ResourceProvider));
            RegisterOptionalKitTick(InstallOptionalKitAdapter("YokiFrame.Godot.GodotSaveKitInstaller", ResourceProvider));

            EventKitErrorHandler.OnError = message =>
            {
                if (mEnableDebugLog)
                    LogKit.Error(message);
            };
        }

        private void RegisterOptionalKitTick(Func<float, bool> tick)
        {
            if (tick != null)
                mOptionalKitTicks.Add(tick);
        }

        private void TickOptionalKits(float deltaSeconds)
        {
            for (var i = 0; i < mOptionalKitTicks.Count; i++)
            {
                mOptionalKitTicks[i](deltaSeconds);
            }
        }

        private static Func<float, bool> InstallOptionalKitAdapter(string installerTypeName, IResourceProvider resourceProvider)
        {
            var installerType = Type.GetType(installerTypeName);
            if (installerType == null)
                return null;

            var installMethod = installerType.GetMethod("Install", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(IResourceProvider) }, null);
            if (installMethod != null)
                installMethod.Invoke(null, new object[] { resourceProvider });

            var tickMethod = FindOptionalKitTickMethod(installerType);
            if (tickMethod == null || tickMethod.ReturnType != typeof(bool))
                return null;

            return (Func<float, bool>)Delegate.CreateDelegate(typeof(Func<float, bool>), tickMethod);
        }

        private static MethodInfo FindOptionalKitTickMethod(Type installerType)
        {
            var parameters = new[] { typeof(float) };
            var tickMethod = installerType.GetMethod("TickAutoSave", BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            if (tickMethod != null)
                return tickMethod;

            tickMethod = installerType.GetMethod("TickActionKit", BindingFlags.Public | BindingFlags.Static, null, parameters, null);
            if (tickMethod != null)
                return tickMethod;

            return installerType.GetMethod("Tick", BindingFlags.Public | BindingFlags.Static, null, parameters, null);
        }

        public override void _ExitTree()
        {
            if (ReferenceEquals(sInstance, this))
                sInstance = null;

            mOptionalKitTicks.Clear();
            EventKitErrorHandler.OnError = null;
            if (mAudioBackend != null)
            {
                mAudioBackend.Dispose();
                mAudioBackend = null;
            }
            AudioKit.ClearBackend();
            if (ReferenceEquals(LogKit.GetLogger(), Logger))
                LogKit.ClearLogger();
        }
    }
}
#endif
