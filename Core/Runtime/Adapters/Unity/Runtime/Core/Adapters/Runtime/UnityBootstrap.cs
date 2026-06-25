#if !GODOT
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// YokiFrame 的 Unity 启动器。
    /// 挂载到场景中的根 GameObject 上，负责注册所有引擎适配器到框架服务
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class UnityBootstrap : MonoSingleton<UnityBootstrap>
    {
        [Header("Logging")]
        [SerializeField] private bool mEnableDebugLog = true;
        [SerializeField] private UnityLogKitOptions mLogKitOptions = UnityLogKitOptions.CreateDefault();
        private readonly List<Func<float, bool>> mOptionalKitTicks = new();

        /// <summary>
        /// 获取当前引擎日志实例。
        /// </summary>
        public IEngineLogger Logger { get; private set; }

        /// <summary>
        /// 获取当前引擎时间实例。
        /// </summary>
        public IEngineTime TimeProvider { get; private set; }

        /// <summary>
        /// 获取当前资源提供者实例。
        /// </summary>
        public IResourceProvider ResourceProvider { get; private set; }

        /// <summary>
        /// 获取当前序列化提供者实例。
        /// </summary>
        public ISerializationProvider SerializationProvider { get; private set; }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            InitializeAdapters();
        }

        private void InitializeAdapters()
        {
            Logger = new UnityEngineLogger();
            UnityRuntimeSettingsBridge.EnsureInstalled();
            UnityLogKitRuntimeInstaller.Install(UnityRuntimeSettingsBridge.GetLogKitOptions(mLogKitOptions), Logger);
            TimeProvider = new UnityEngineTime();
            ResourceProvider = new UnityResourceProvider();
            SerializationProvider = new UnityEngineSerializationProvider();
            ResKit.SetProvider(ResourceProvider);
            RegisterOptionalKitTick(InstallOptionalKitAdapter("YokiFrame.Unity.UnityActionKitInstaller, YokiFrame.Unity.ActionKit", ResourceProvider));
            RegisterOptionalKitTick(InstallOptionalKitAdapter("YokiFrame.Unity.UnitySceneKitInstaller, YokiFrame.Unity.SceneKit", ResourceProvider));
            RegisterOptionalKitTick(InstallOptionalKitAdapter("YokiFrame.Unity.UnityInputKitInstaller, YokiFrame.Unity.InputKit", ResourceProvider));
            RegisterOptionalKitTick(InstallOptionalKitAdapter("YokiFrame.Unity.UnitySaveKitInstaller, YokiFrame.Unity.SaveKit", ResourceProvider));
            RegisterOptionalKitTick(InstallOptionalKitAdapter("YokiFrame.Unity.UnityUIKitInstaller, YokiFrame.Unity.UIKit", ResourceProvider));
            AudioKit.SetBackend(new UnityAudioKitBackend());

            EventKitErrorHandler.OnError = message =>
            {
                if (mEnableDebugLog)
                    LogKit.Error(message);
            };
        }

        private void Update()
        {
            TickOptionalKits(TimeProvider != null ? TimeProvider.DeltaTime : Time.deltaTime);
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

            var installMethod = installerType.GetMethod(
                "Install",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(IResourceProvider) },
                null);
            if (installMethod != null)
                installMethod.Invoke(null, new object[] { resourceProvider });

            var tickMethod = installerType.GetMethod(
                "TickAutoSave",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(float) },
                null);
            if (tickMethod == null)
            {
                tickMethod = installerType.GetMethod(
                    "TickActionKit",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(float) },
                    null);
            }

            if (tickMethod == null)
            {
                tickMethod = installerType.GetMethod(
                    "Tick",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(float) },
                    null);
            }

            if (tickMethod == null || tickMethod.ReturnType != typeof(bool))
                return null;

            return (Func<float, bool>)Delegate.CreateDelegate(typeof(Func<float, bool>), tickMethod);
        }

        protected override void OnDestroy()
        {
            EventKitErrorHandler.OnError = null;
            mOptionalKitTicks.Clear();
            AudioKit.ClearBackend();
            UnityLogKitRuntimeInstaller.Shutdown();
            base.OnDestroy();
        }
    }
}
#endif
