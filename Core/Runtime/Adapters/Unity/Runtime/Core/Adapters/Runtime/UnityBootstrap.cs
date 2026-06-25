#if !GODOT
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
            YokiFrameKit.RegisterInstaller(new UnityCoreKitInstaller(mLogKitOptions, null));
            var runtime = YokiFrameKit.Initialize(YokiFrameEngine.Unity);
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

        private void Update()
        {
            YokiFrameKit.Tick(TimeProvider != null ? TimeProvider.DeltaTime : Time.deltaTime);
        }

        protected override void OnDestroy()
        {
            EventKitErrorHandler.OnError = null;
            YokiFrameKit.Shutdown();
            base.OnDestroy();
        }
    }
}
#endif
