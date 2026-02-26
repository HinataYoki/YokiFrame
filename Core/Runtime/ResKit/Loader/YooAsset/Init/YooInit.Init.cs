#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using YooAsset;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#else
using System.Collections;
#endif

namespace YokiFrame
{
    /// <summary>
    /// YooInit 初始化逻辑
    /// </summary>
    public static partial class YooInit
    {
#if YOKIFRAME_UNITASK_SUPPORT
        #region 初始化 - UniTask 版本

        /// <summary>
        /// 使用默认配置初始化 YooAsset
        /// </summary>
        public static UniTask InitAsync(CancellationToken ct = default)
            => InitAsync(new YooInitConfig(), ct);

        /// <summary>
        /// 初始化 YooAsset 并自动配置 ResKit
        /// </summary>
        public static async UniTask InitAsync(YooInitConfig config, CancellationToken ct = default)
        {
            if (Initialized) return;
            if (config is null) throw new ArgumentNullException(nameof(config));

            KitLogger.Log($"[YooInit] 开始初始化，模式: {config.PlayMode}");

            YooAssets.Initialize();

            var packages = GetPackagesInternal();

            // 初始化所有资源包
            if (config.PackageNames is { Count: > 0 })
            {
                for (int i = 0; i < config.PackageNames.Count; i++)
                {
                    var packageName = config.PackageNames[i];
                    if (string.IsNullOrEmpty(packageName)) continue;

                    var package = YooAssets.CreatePackage(packageName);
                    packages[packageName] = package;

                    // 第一个包设为默认包
                    if (i == 0)
                    {
                        SetDefaultPackage(package);
                        YooAssets.SetDefaultPackage(package);
                    }

                    await InitPackageAsync(package, config, ct);
                    KitLogger.Log($"[YooInit] 资源包初始化完成: {packageName}");
                }
            }

            // 配置 ResKit 加载器
            ConfigureResKit();

            SetInitialized(true);
            KitLogger.Log($"[YooInit] 初始化完成，资源包数量: {packages.Count}");
        }

        private static async UniTask InitPackageAsync(ResourcePackage package, YooInitConfig config, CancellationToken ct)
        {
            InitializationOperation operation = CreateInitOperation(package, config);

            await operation.ToUniTask(cancellationToken: ct);

            if (operation.Status != EOperationStatus.Succeed)
            {
                throw new Exception($"[YooInit] 包初始化失败: {package.PackageName}, 错误: {operation.Error}");
            }

            // 请求版本
            var versionOp = package.RequestPackageVersionAsync();
            await versionOp.ToUniTask(cancellationToken: ct);

            if (versionOp.Status != EOperationStatus.Succeed)
            {
                KitLogger.Warning($"[YooInit] 请求版本失败: {versionOp.Error}");
                return;
            }

            // 更新清单
            var manifestOp = package.UpdatePackageManifestAsync(versionOp.PackageVersion);
            await manifestOp.ToUniTask(cancellationToken: ct);

            if (manifestOp.Status != EOperationStatus.Succeed)
            {
                KitLogger.Warning($"[YooInit] 更新清单失败: {manifestOp.Error}");
            }
        }

        #endregion
#else
        #region 初始化 - 协程版本

        /// <summary>
        /// 使用默认配置初始化 YooAsset
        /// </summary>
        public static IEnumerator InitAsync(Action onComplete = null)
            => InitAsync(new YooInitConfig(), onComplete);

        /// <summary>
        /// 初始化 YooAsset 并自动配置 ResKit
        /// </summary>
        public static IEnumerator InitAsync(YooInitConfig config, Action onComplete = null)
        {
            if (Initialized)
            {
                onComplete?.Invoke();
                yield break;
            }

            if (config is null) throw new ArgumentNullException(nameof(config));

            KitLogger.Log($"[YooInit] 开始初始化，模式: {config.PlayMode}");

            YooAssets.Initialize();

            var packages = GetPackagesInternal();

            // 初始化所有资源包
            if (config.PackageNames is { Count: > 0 })
            {
                for (int i = 0; i < config.PackageNames.Count; i++)
                {
                    var packageName = config.PackageNames[i];
                    if (string.IsNullOrEmpty(packageName)) continue;

                    var package = YooAssets.CreatePackage(packageName);
                    packages[packageName] = package;

                    // 第一个包设为默认包
                    if (i == 0)
                    {
                        SetDefaultPackage(package);
                        YooAssets.SetDefaultPackage(package);
                    }

                    yield return InitPackageCoroutine(package, config);
                    KitLogger.Log($"[YooInit] 资源包初始化完成: {packageName}");
                }
            }

            // 配置 ResKit 加载器
            ConfigureResKit();

            SetInitialized(true);
            KitLogger.Log($"[YooInit] 初始化完成，资源包数量: {packages.Count}");
            onComplete?.Invoke();
        }

        private static IEnumerator InitPackageCoroutine(ResourcePackage package, YooInitConfig config)
        {
            InitializationOperation operation = CreateInitOperation(package, config);

            yield return operation;

            if (operation.Status != EOperationStatus.Succeed)
            {
                throw new Exception($"[YooInit] 包初始化失败: {package.PackageName}, 错误: {operation.Error}");
            }

            // 请求版本
            var versionOp = package.RequestPackageVersionAsync();
            yield return versionOp;

            if (versionOp.Status != EOperationStatus.Succeed)
            {
                KitLogger.Warning($"[YooInit] 请求版本失败: {versionOp.Error}");
                yield break;
            }

            // 更新清单
            var manifestOp = package.UpdatePackageManifestAsync(versionOp.PackageVersion);
            yield return manifestOp;

            if (manifestOp.Status != EOperationStatus.Succeed)
            {
                KitLogger.Warning($"[YooInit] 更新清单失败: {manifestOp.Error}");
            }
        }

        #endregion
#endif

        #region 初始化模式

        private static InitializationOperation InitEditorSimulateMode(ResourcePackage package)
        {
            var buildResult = EditorSimulateModeHelper.SimulateBuild(package.PackageName);
            var fileSystemParams = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory);
            return package.InitializeAsync(new EditorSimulateModeParameters
            {
                EditorFileSystemParameters = fileSystemParams
            });
        }

        private static InitializationOperation InitOfflineMode(ResourcePackage package, YooInitConfig config)
        {
            var decryption = config.CreateDecryptionServices();
            var fileSystemParams = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(decryption);
            return package.InitializeAsync(new OfflinePlayModeParameters
            {
                BuildinFileSystemParameters = fileSystemParams
            });
        }

        private static InitializationOperation InitHostMode(ResourcePackage package, YooInitConfig config)
        {
            if (HostModeHandler is null)
            {
                throw new InvalidOperationException(
                    "[YooInit] HostPlayMode 需要配置远程服务。请在调用 InitAsync 前设置 YooInit.HostModeHandler 委托。\n" +
                    "示例:\n" +
                    "YooInit.HostModeHandler = (pkg, cfg) => {\n" +
                    "    var remoteServices = new RemoteServices(\"http://cdn.example.com\", \"http://fallback.example.com\");\n" +
                    "    var cacheParams = FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices, cfg.CreateDecryptionServices());\n" +
                    "    var buildinParams = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(cfg.CreateDecryptionServices());\n" +
                    "    return pkg.InitializeAsync(new HostPlayModeParameters {\n" +
                    "        BuildinFileSystemParameters = buildinParams,\n" +
                    "        CacheFileSystemParameters = cacheParams\n" +
                    "    });\n" +
                    "};");
            }
            return HostModeHandler(package, config);
        }

        private static InitializationOperation InitWebMode(ResourcePackage package, YooInitConfig config)
        {
            if (WebModeHandler is null)
            {
                throw new InvalidOperationException(
                    "[YooInit] WebPlayMode 需要配置 WebGL 远程服务。请在调用 InitAsync 前设置 YooInit.WebModeHandler 委托。");
            }
            return WebModeHandler(package, config);
        }

        private static InitializationOperation CreateInitOperation(ResourcePackage package, YooInitConfig config)
        {
            // 优先使用通用自定义处理器
            if (CustomHandler is not null)
                return CustomHandler(package, config);

            return config.PlayMode switch
            {
                EPlayMode.EditorSimulateMode => InitEditorSimulateMode(package),
                EPlayMode.OfflinePlayMode => InitOfflineMode(package, config),
                EPlayMode.HostPlayMode => InitHostMode(package, config),
                EPlayMode.WebPlayMode => InitWebMode(package, config),
                EPlayMode.CustomPlayMode => InitCustomMode(package, config),
                _ => throw new NotSupportedException($"[YooInit] 不支持的播放模式: {config.PlayMode}，请设置 YooInit.CustomHandler 委托处理。")
            };
        }

        private static InitializationOperation InitCustomMode(ResourcePackage package, YooInitConfig config)
        {
            if (CustomHandler is null)
            {
                throw new InvalidOperationException(
                    "[YooInit] CustomPlayMode 需要配置自定义初始化逻辑。请在调用 InitAsync 前设置 YooInit.CustomHandler 委托。\n" +
                    "示例:\n" +
                    "YooInit.CustomHandler = (pkg, cfg) => {\n" +
                    "    // 自定义初始化逻辑\n" +
                    "    var buildinParams = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(cfg.CreateDecryptionServices());\n" +
                    "    return pkg.InitializeAsync(new OfflinePlayModeParameters {\n" +
                    "        BuildinFileSystemParameters = buildinParams\n" +
                    "    });\n" +
                    "};");
            }
            return CustomHandler(package, config);
        }

        private static void ConfigureResKit()
        {
#if YOKIFRAME_UNITASK_SUPPORT
            ResKit.SetLoaderPool(new YooAssetResLoaderUniTaskPool(DefaultPackage));
            ResKit.SetRawFileLoaderPool(new YooAssetRawFileLoaderUniTaskPool());
            ResKit.SetSceneLoaderPool(new YooAssetSceneLoaderUniTaskPool(DefaultPackage));
#else
            ResKit.SetLoaderPool(new YooAssetResLoaderPool(DefaultPackage));
            ResKit.SetRawFileLoaderPool(new YooAssetRawFileLoaderPool());
            ResKit.SetSceneLoaderPool(new YooAssetSceneLoaderPool(DefaultPackage));
#endif
        }

        #endregion
    }
}
#endif
