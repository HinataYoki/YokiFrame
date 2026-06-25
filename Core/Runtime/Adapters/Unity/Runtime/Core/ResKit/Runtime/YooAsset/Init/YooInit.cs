#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections.Generic;
using System.Threading;
using YokiFrame;
using YooAsset;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame.Unity
{
#if YOOASSET_3_0_OR_NEWER
    /// <summary>
    /// YooAsset 3.x 资源包初始化委托。
    /// </summary>
    /// <param name="package">要初始化的资源包。</param>
    /// <param name="config">初始化配置。</param>
    /// <returns>YooAsset 初始化操作。</returns>
    public delegate InitializePackageOperation YooAssetInitHandler(ResourcePackage package, YooInitConfig config);
#else
    /// <summary>
    /// YooAsset 2.x 资源包初始化委托。
    /// </summary>
    /// <param name="package">要初始化的资源包。</param>
    /// <param name="config">初始化配置。</param>
    /// <returns>YooAsset 初始化操作。</returns>
    public delegate InitializationOperation YooAssetInitHandler(ResourcePackage package, YooInitConfig config);
#endif

    /// <summary>
    /// YooAsset 初始化入口，初始化完成后直接把 ResKit 后端切换为 YooAsset。
    /// </summary>
    public static class YooInit
    {
        private static readonly Dictionary<string, ResourcePackage> sPackages = new();

        /// <summary>
        /// 获取 YooAsset 是否已经完成初始化。
        /// </summary>
        public static bool Initialized { get; private set; }

        /// <summary>
        /// 获取当前默认资源包。
        /// </summary>
        public static ResourcePackage DefaultPackage { get; private set; }

        /// <summary>
        /// 获取当前默认资源包名称。
        /// </summary>
        public static string DefaultPackageName { get; private set; }

        /// <summary>
        /// 获取已注册的资源包集合。
        /// </summary>
        public static IReadOnlyDictionary<string, ResourcePackage> Packages => sPackages;

        /// <summary>
        /// 获取或设置 CustomPlayMode 初始化处理器。
        /// </summary>
        public static YooAssetInitHandler CustomHandler { get; set; }

        /// <summary>
        /// 获取或设置 HostPlayMode 初始化处理器。
        /// </summary>
        public static YooAssetInitHandler HostModeHandler { get; set; }

        /// <summary>
        /// 获取或设置 WebPlayMode 初始化处理器。
        /// </summary>
        public static YooAssetInitHandler WebModeHandler { get; set; }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 使用默认配置初始化 YooAsset。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        public static UniTask InitAsync(CancellationToken token = default)
#else
        /// <summary>
        /// 使用默认配置初始化 YooAsset。
        /// </summary>
        /// <param name="token">取消令牌。</param>
        public static Task InitAsync(CancellationToken token = default)
#endif
        {
            return InitAsync(new YooInitConfig(), token);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 使用指定配置初始化 YooAsset。
        /// </summary>
        /// <param name="config">初始化配置。</param>
        /// <param name="token">取消令牌。</param>
        public static async UniTask InitAsync(YooInitConfig config, CancellationToken token = default)
#else
        /// <summary>
        /// 使用指定配置初始化 YooAsset。
        /// </summary>
        /// <param name="config">初始化配置。</param>
        /// <param name="token">取消令牌。</param>
        public static async Task InitAsync(YooInitConfig config, CancellationToken token = default)
#endif
        {
            if (Initialized)
                return;

            if (config == null)
                throw new ArgumentNullException(nameof(config));
#if YOOASSET_3_0_OR_NEWER
            if (!YooAssets.IsInitialized)
#else
            if (!YooAssets.Initialized)
#endif
                YooAssets.Initialize();

            sPackages.Clear();
            DefaultPackage = null;
            DefaultPackageName = null;

            var packageNames = config.PackageNames;
            if (packageNames == null || packageNames.Count == 0)
                packageNames = new() { YooInitConfig.DEFAULT_PACKAGE_NAME };

            for (var i = 0; i < packageNames.Count; i++)
            {
                var packageName = packageNames[i];
                if (string.IsNullOrEmpty(packageName))
                    continue;

                var package = GetOrCreatePackage(packageName);
                sPackages[packageName] = package;

                if (DefaultPackage == null)
                {
                    DefaultPackage = package;
                    DefaultPackageName = packageName;
#if !YOOASSET_3_0_OR_NEWER
                    YooAssets.SetDefaultPackage(package);
#endif
                }

                var initOperation = CreateInitOperation(package, config);
#if YOKIFRAME_UNITASK_SUPPORT
                await YooInitOperationAwaiter.WaitAsync(initOperation, token);
#else
                await YooInitOperationAwaiter.WaitAsync(initOperation, token).ConfigureAwait(false);
#endif

                if (config.AutoLoadManifest)
                {
#if YOKIFRAME_UNITASK_SUPPORT
                    await LoadPackageManifestAsync(package, config, token);
#else
                    await LoadPackageManifestAsync(package, config, token).ConfigureAwait(false);
#endif
                }
            }

            if (DefaultPackage == null)
                throw new InvalidOperationException("[YooInit] 没有可用资源包，请检查 YooInitConfig.PackageNames。");

            InstallProvider(DefaultPackage);
            Initialized = true;
        }

        /// <summary>
        /// 将 ResKit 资源提供者切换到当前默认 YooAsset 资源包。
        /// </summary>
        public static void InstallProvider()
        {
            if (DefaultPackage != null)
            {
                InstallProvider(DefaultPackage);
                return;
            }

#if YOOASSET_3_0_OR_NEWER
            ResKit.SetProvider(new YooAssetResourceProvider(YooInitConfig.DEFAULT_PACKAGE_NAME));
#else
            ResKit.SetProvider(new YooAssetResourceProvider());
#endif
        }

        /// <summary>
        /// 将 ResKit 资源提供者切换到指定 YooAsset 资源包。
        /// </summary>
        /// <param name="package">YooAsset 资源包实例。</param>
        public static void InstallProvider(ResourcePackage package)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));

            DefaultPackage = package;
            DefaultPackageName = package.PackageName;
            sPackages[package.PackageName] = package;

#if YOOASSET_3_0_OR_NEWER
            ResKit.SetProvider(new YooAssetResourceProvider(package));
#else
            YooAssets.SetDefaultPackage(package);
            ResKit.SetProvider(new YooAssetResourceProvider());
#endif
        }

        /// <summary>
        /// 尝试获取指定名称的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <param name="package">找到的资源包。</param>
        /// <returns>找到资源包时返回 true。</returns>
        public static bool TryGetPackage(string packageName, out ResourcePackage package)
        {
            return sPackages.TryGetValue(packageName, out package);
        }

        /// <summary>
        /// 获取指定名称的资源包。
        /// </summary>
        /// <param name="packageName">资源包名称。</param>
        /// <returns>找到的资源包；不存在时返回 null。</returns>
        public static ResourcePackage GetPackage(string packageName)
        {
            ResourcePackage package;
            return sPackages.TryGetValue(packageName, out package) ? package : null;
        }

        /// <summary>
        /// 重置 YooAsset 初始化状态并清空 ResKit 缓存。
        /// </summary>
        public static void Reset()
        {
            ResKit.ClearAll();
            Initialized = false;
            DefaultPackage = null;
            DefaultPackageName = null;
            sPackages.Clear();
            CustomHandler = null;
            HostModeHandler = null;
            WebModeHandler = null;
        }

        private static ResourcePackage GetOrCreatePackage(string packageName)
        {
#if YOOASSET_3_0_OR_NEWER
            ResourcePackage package;
            if (YooAssets.TryGetPackage(packageName, out package))
                return package;

            return YooAssets.CreatePackage(packageName);
#else
            var package = YooAssets.TryGetPackage(packageName);
            if (package != null)
                return package;

            return YooAssets.CreatePackage(packageName);
#endif
        }

#if YOKIFRAME_UNITASK_SUPPORT
        private static async UniTask LoadPackageManifestAsync(ResourcePackage package, YooInitConfig config, CancellationToken token)
#else
        private static async Task LoadPackageManifestAsync(ResourcePackage package, YooInitConfig config, CancellationToken token)
#endif
        {
            var timeout = config.GetManifestTimeoutSeconds();
#if YOOASSET_3_0_OR_NEWER
            var versionOptions = new RequestPackageVersionOptions(true, timeout);
            var versionOperation = package.RequestPackageVersionAsync(versionOptions);
#else
            var versionOperation = package.RequestPackageVersionAsync(true, timeout);
#endif
#if YOKIFRAME_UNITASK_SUPPORT
            await YooInitOperationAwaiter.WaitAsync(versionOperation, token);
#else
            await YooInitOperationAwaiter.WaitAsync(versionOperation, token).ConfigureAwait(false);
#endif

#if YOOASSET_3_0_OR_NEWER
            var manifestOptions = new LoadPackageManifestOptions(versionOperation.PackageVersion, timeout);
            var manifestOperation = package.LoadPackageManifestAsync(manifestOptions);
#else
            var manifestOperation = package.UpdatePackageManifestAsync(versionOperation.PackageVersion, timeout);
#endif

#if YOKIFRAME_UNITASK_SUPPORT
            await YooInitOperationAwaiter.WaitAsync(manifestOperation, token);
#else
            await YooInitOperationAwaiter.WaitAsync(manifestOperation, token).ConfigureAwait(false);
#endif
        }

#if YOOASSET_3_0_OR_NEWER
        private static InitializePackageOperation CreateInitOperation(ResourcePackage package, YooInitConfig config)
#else
        private static InitializationOperation CreateInitOperation(ResourcePackage package, YooInitConfig config)
#endif
        {
            if (CustomHandler != null)
                return CustomHandler(package, config);

            switch (config.PlayMode)
            {
                case EPlayMode.EditorSimulateMode:
                    return CreateEditorSimulateOperation(package);
                case EPlayMode.OfflinePlayMode:
                    return CreateOfflineOperation(package);
                case EPlayMode.HostPlayMode:
                    return CreateHostOperation(package, config);
                case EPlayMode.WebPlayMode:
                    return CreateWebOperation(package, config);
                case EPlayMode.CustomPlayMode:
                    throw new InvalidOperationException("[YooInit] CustomPlayMode 需要设置 YooInit.CustomHandler。");
                default:
                    throw new NotSupportedException("[YooInit] 不支持的 YooAsset PlayMode: " + config.PlayMode);
            }
        }

#if YOOASSET_3_0_OR_NEWER
        private static InitializePackageOperation CreateEditorSimulateOperation(ResourcePackage package)
        {
#if UNITY_EDITOR
            var buildResult = EditorSimulateBuildInvoker.Build(package.PackageName, (int)EBundleType.VirtualAssetBundle);
            var fileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory);
            return package.InitializePackageAsync(new EditorSimulateModeOptions
            {
                EditorFileSystemParameters = fileSystemParameters
            });
#else
            throw new InvalidOperationException("[YooInit] EditorSimulateMode 只能在 Unity Editor 中使用。");
#endif
        }

        private static InitializePackageOperation CreateOfflineOperation(ResourcePackage package)
        {
            var fileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
            return package.InitializePackageAsync(new OfflinePlayModeOptions
            {
                BuiltinFileSystemParameters = fileSystemParameters
            });
        }

        private static InitializePackageOperation CreateHostOperation(ResourcePackage package, YooInitConfig config)
        {
            if (HostModeHandler != null)
                return HostModeHandler(package, config);

            throw new InvalidOperationException("[YooInit] YooAsset 3.x HostPlayMode 需要设置 YooInit.HostModeHandler。");
        }

        private static InitializePackageOperation CreateWebOperation(ResourcePackage package, YooInitConfig config)
        {
            if (WebModeHandler != null)
                return WebModeHandler(package, config);

            throw new InvalidOperationException("[YooInit] YooAsset 3.x WebPlayMode 需要设置 YooInit.WebModeHandler。");
        }
#else
        private static InitializationOperation CreateEditorSimulateOperation(ResourcePackage package)
        {
#if UNITY_EDITOR
            var buildResult = EditorSimulateModeHelper.SimulateBuild(package.PackageName);
            var parameters = new EditorSimulateModeParameters
            {
                EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory)
            };
            return package.InitializeAsync(parameters);
#else
            throw new InvalidOperationException("[YooInit] EditorSimulateMode 只能在 Unity Editor 中使用。");
#endif
        }

        private static InitializationOperation CreateOfflineOperation(ResourcePackage package)
        {
            var parameters = new OfflinePlayModeParameters
            {
                BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters()
            };
            return package.InitializeAsync(parameters);
        }

        private static InitializationOperation CreateHostOperation(ResourcePackage package, YooInitConfig config)
        {
            if (HostModeHandler != null)
                return HostModeHandler(package, config);

            var remoteServices = CreateRemoteServices(config);
            var parameters = new HostPlayModeParameters
            {
                BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(),
                CacheFileSystemParameters = FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices)
            };
            return package.InitializeAsync(parameters);
        }

        private static InitializationOperation CreateWebOperation(ResourcePackage package, YooInitConfig config)
        {
            if (WebModeHandler != null)
                return WebModeHandler(package, config);

            var remoteServices = CreateRemoteServices(config);
            var parameters = new WebPlayModeParameters
            {
                WebServerFileSystemParameters = FileSystemParameters.CreateDefaultWebServerFileSystemParameters(),
                WebRemoteFileSystemParameters = FileSystemParameters.CreateDefaultWebRemoteFileSystemParameters(remoteServices)
            };
            return package.InitializeAsync(parameters);
        }

        private static IRemoteServices CreateRemoteServices(YooInitConfig config)
        {
            if (string.IsNullOrEmpty(config.DefaultHostServer))
                throw new InvalidOperationException("[YooInit] Host/Web 模式需要配置 DefaultHostServer。");

            return new YooInitRemoteServices(config.DefaultHostServer, config.FallbackHostServer);
        }
#endif
    }
}
#endif
#endif
