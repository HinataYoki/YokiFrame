#if YOKIFRAME_YOOASSET_SUPPORT
using System.Collections.Generic;
using YooAsset;

namespace YokiFrame
{
#if YOOASSET_3_0_OR_NEWER
    /// <summary>
    /// 自定义初始化模式委托（3.x 版本）
    /// </summary>
    /// <param name="package">资源包</param>
    /// <param name="config">初始化配置</param>
    /// <returns>初始化操作</returns>
    public delegate InitializePackageOperation CustomInitModeHandler(ResourcePackage package, YooInitConfig config);
#else
    /// <summary>
    /// 自定义初始化模式委托（2.x 版本 — 无 ResourcePackage）
    /// </summary>
    /// <param name="packageName">资源包名称</param>
    /// <param name="config">初始化配置</param>
    /// <returns>初始化操作</returns>
    public delegate InitializationOperation CustomInitModeHandler(string packageName, YooInitConfig config);
#endif

    /// <summary>
    /// YooAsset 初始化器
    /// 负责 YooAsset 初始化及 ResKit 加载器配置
    /// </summary>
    public static partial class YooInit
    {
        #region 状态

        /// <summary>
        /// 是否已完成初始化
        /// </summary>
        public static bool Initialized { get; private set; }

#if YOOASSET_3_0_OR_NEWER
        /// <summary>
        /// 默认资源包（第一个包，用于 ResKit）
        /// </summary>
        public static ResourcePackage DefaultPackage { get; private set; }

        /// <summary>
        /// 所有已初始化的资源包
        /// </summary>
        public static IReadOnlyDictionary<string, ResourcePackage> Packages => sPackages;
        private static readonly Dictionary<string, ResourcePackage> sPackages = new();
#else
        /// <summary>
        /// 默认资源包名称（2.x 无 ResourcePackage 类型）
        /// </summary>
        public static string DefaultPackageName { get; private set; }

        /// <summary>
        /// 所有已初始化的资源包名称
        /// </summary>
        public static IReadOnlyList<string> PackageNames => sPackageNames;
        internal static readonly List<string> sPackageNames = new();
#endif

        #endregion

        #region 自定义初始化模式

#if YOOASSET_3_0_OR_NEWER
        /// <summary>
        /// HostPlayMode 自定义初始化处理器（3.x 版本）
        /// </summary>
        /// <example>
        /// YooInit.HostModeHandler = (package, config) =>
        /// {
        ///     IRemoteService remoteService = new MyRemoteService("http://cdn.example.com", "http://fallback.example.com");
        ///     var builtinParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
        ///     var cacheParams = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService);
        ///     var options = new HostPlayModeOptions
        ///     {
        ///         BuiltinFileSystemParameters = builtinParams,
        ///         CacheFileSystemParameters = cacheParams
        ///     };
        ///     return package.InitializePackageAsync(options);
        /// };
        /// </example>
        public static CustomInitModeHandler HostModeHandler { get; set; }

        /// <summary>
        /// WebPlayMode 自定义初始化处理器
        /// </summary>
        public static CustomInitModeHandler WebModeHandler { get; set; }

        /// <summary>
        /// 通用自定义初始化处理器（优先级最高）
        /// </summary>
        public static CustomInitModeHandler CustomHandler { get; set; }
#else
        /// <summary>
        /// HostPlayMode 自定义初始化处理器（2.x 版本）
        /// </summary>
        /// <example>
        /// YooInit.HostModeHandler = (packageName, config) =>
        /// {
        ///     var hostParams = new HostPlayModeParameters
        ///     {
        ///         DefaultHostServer = "http://cdn.example.com",
        ///         FallbackHostServer = "http://fallback.example.com"
        ///     };
        ///     return YooAssets.Initialize(hostParams);
        /// };
        /// </example>
        public static CustomInitModeHandler HostModeHandler { get; set; }

        /// <summary>
        /// WebPlayMode 自定义初始化处理器
        /// </summary>
        public static CustomInitModeHandler WebModeHandler { get; set; }

        /// <summary>
        /// 通用自定义初始化处理器（优先级最高）
        /// </summary>
        public static CustomInitModeHandler CustomHandler { get; set; }
#endif

        #endregion

        #region 生命周期

        /// <summary>
        /// 销毁并重置状态
        /// </summary>
        public static void Dispose()
        {
            if (!Initialized) return;

            ResKit.ClearAll();
#if YOOASSET_3_0_OR_NEWER
            sPackages.Clear();
            DefaultPackage = null;
#else
            sPackageNames.Clear();
            DefaultPackageName = null;
#endif
            Initialized = false;

            // 不重置委托，允许复用

            KitLogger.Log("[YooInit] 已销毁");
        }

        /// <summary>
        /// 完全重置（包括委托）
        /// </summary>
        public static void Reset()
        {
            Dispose();
#if YOOASSET_3_0_OR_NEWER || !YOOASSET_3_0_OR_NEWER
            HostModeHandler = null;
            WebModeHandler = null;
            CustomHandler = null;
#endif
        }

        /// <summary>
        /// 设置初始化完成状态（内部使用）
        /// </summary>
        internal static void SetInitialized(bool value) => Initialized = value;

#if YOOASSET_3_0_OR_NEWER
        /// <summary>
        /// 设置默认包（内部使用）
        /// </summary>
        internal static void SetDefaultPackage(ResourcePackage package) => DefaultPackage = package;

        /// <summary>
        /// 获取内部包字典（内部使用）
        /// </summary>
        internal static Dictionary<string, ResourcePackage> GetPackagesInternal() => sPackages;
#else
        /// <summary>
        /// 设置默认包名称（内部使用）
        /// </summary>
        internal static void SetDefaultPackageName(string name) => DefaultPackageName = name;

        /// <summary>
        /// 获取内部包名称列表（内部使用）
        /// </summary>
        internal static List<string> GetPackageNamesInternal() => sPackageNames;
#endif

        #endregion
    }
}
#endif
