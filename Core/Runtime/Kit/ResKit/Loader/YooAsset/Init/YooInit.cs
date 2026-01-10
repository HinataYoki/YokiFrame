#if YOKIFRAME_YOOASSET_SUPPORT
using System.Collections.Generic;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// 自定义初始化模式委托
    /// </summary>
    /// <param name="package">资源包</param>
    /// <param name="config">初始化配置</param>
    /// <returns>初始化操作</returns>
    public delegate InitializationOperation CustomInitModeHandler(ResourcePackage package, YooInitConfig config);

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

        /// <summary>
        /// 默认资源包（第一个包，用于 ResKit）
        /// </summary>
        public static ResourcePackage DefaultPackage { get; private set; }

        /// <summary>
        /// 所有已初始化的资源包
        /// </summary>
        public static IReadOnlyDictionary<string, ResourcePackage> Packages => sPackages;
        private static readonly Dictionary<string, ResourcePackage> sPackages = new();

        #endregion

        #region 自定义初始化模式

        /// <summary>
        /// HostPlayMode 自定义初始化处理器
        /// 用户需要在调用 InitAsync 前设置此委托
        /// </summary>
        /// <example>
        /// YooInit.HostModeHandler = (package, config) =>
        /// {
        ///     var remoteServices = new RemoteServices("http://cdn.example.com", "http://cdn-fallback.example.com");
        ///     var cacheParams = FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices, config.CreateDecryptionServices());
        ///     var buildinParams = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(config.CreateDecryptionServices());
        ///     return package.InitializeAsync(new HostPlayModeParameters
        ///     {
        ///         BuildinFileSystemParameters = buildinParams,
        ///         CacheFileSystemParameters = cacheParams
        ///     });
        /// };
        /// </example>
        public static CustomInitModeHandler HostModeHandler { get; set; }

        /// <summary>
        /// WebPlayMode 自定义初始化处理器
        /// </summary>
        public static CustomInitModeHandler WebModeHandler { get; set; }

        /// <summary>
        /// 通用自定义初始化处理器（优先级最高）
        /// 设置后将覆盖所有模式的默认实现
        /// </summary>
        public static CustomInitModeHandler CustomHandler { get; set; }

        #endregion

        #region 生命周期

        /// <summary>
        /// 销毁并重置状态
        /// </summary>
        public static void Dispose()
        {
            if (!Initialized) return;

            ResKit.ClearAll();
            sPackages.Clear();
            DefaultPackage = null;
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
            HostModeHandler = null;
            WebModeHandler = null;
            CustomHandler = null;
        }

        /// <summary>
        /// 设置初始化完成状态（内部使用）
        /// </summary>
        internal static void SetInitialized(bool value) => Initialized = value;

        /// <summary>
        /// 设置默认包（内部使用）
        /// </summary>
        internal static void SetDefaultPackage(ResourcePackage package) => DefaultPackage = package;

        /// <summary>
        /// 获取内部包字典（内部使用）
        /// </summary>
        internal static Dictionary<string, ResourcePackage> GetPackagesInternal() => sPackages;

        #endregion
    }
}
#endif
