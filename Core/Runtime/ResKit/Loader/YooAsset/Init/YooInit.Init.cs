#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_3_0_OR_NEWER
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
    /// YooInit 初始化逻辑 — 3.x 版本（适配 3.0.2-beta+）
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

                    // 3.0.2+: TryGetPackage 检查已存在包，避免重复创建
                    if (!YooAssets.TryGetPackage(packageName, out var package))
                        package = YooAssets.CreatePackage(packageName);

                    packages[packageName] = package;

                    // 第一个包设为默认包
                    if (i == 0)
                    {
                        SetDefaultPackage(package);
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
            InitializePackageOperation operation = CreateInitOperation(package, config);

            await operation.ToUniTask(cancellationToken: ct);

            if (operation.Status != EOperationStatus.Succeeded)
            {
                throw new Exception($"[YooInit] 包初始化失败: {package.PackageName}, 错误: {operation.Error}");
            }

            // 3.0.2+: RequestPackageVersionAsync 返回 RequestPackageVersionOperation
            var versionOp = package.RequestPackageVersionAsync();
            await versionOp.ToUniTask(cancellationToken: ct);

            if (versionOp.Status != EOperationStatus.Succeeded)
            {
                KitLogger.Warning($"[YooInit] 请求版本失败: {versionOp.Error}");
                return;
            }

            // 3.0.2+: LoadPackageManifestAsync(options) 替代 UpdatePackageManifestAsync
            var manifestOptions = new LoadPackageManifestOptions(versionOp.PackageVersion, 60);
            var manifestOp = package.LoadPackageManifestAsync(manifestOptions);
            await manifestOp.ToUniTask(cancellationToken: ct);

            if (manifestOp.Status != EOperationStatus.Succeeded)
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

                    if (!YooAssets.TryGetPackage(packageName, out var package))
                        package = YooAssets.CreatePackage(packageName);

                    packages[packageName] = package;

                    // 第一个包设为默认包
                    if (i == 0)
                    {
                        SetDefaultPackage(package);
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
            InitializePackageOperation operation = CreateInitOperation(package, config);

            yield return operation;

            if (operation.Status != EOperationStatus.Succeeded)
            {
                throw new Exception($"[YooInit] 包初始化失败: {package.PackageName}, 错误: {operation.Error}");
            }

            // 请求版本
            var versionOp = package.RequestPackageVersionAsync();
            yield return versionOp;

            if (versionOp.Status != EOperationStatus.Succeeded)
            {
                KitLogger.Warning($"[YooInit] 请求版本失败: {versionOp.Error}");
                yield break;
            }

            // 更新清单
            var manifestOptions = new LoadPackageManifestOptions(versionOp.PackageVersion, 60);
            var manifestOp = package.LoadPackageManifestAsync(manifestOptions);
            yield return manifestOp;

            if (manifestOp.Status != EOperationStatus.Succeeded)
            {
                KitLogger.Warning($"[YooInit] 更新清单失败: {manifestOp.Error}");
            }
        }

        #endregion
#endif

        #region 初始化模式（3.x API）

        private static InitializePackageOperation InitEditorSimulateMode(ResourcePackage package)
        {
            // 3.0.2+: EditorSimulateBuildInvoker.Build() 替代 EditorSimulateModeHelper.SimulateBuild()
            var buildResult = EditorSimulateBuildInvoker.Build(package.PackageName, (int)EBundleType.VirtualAssetBundle);
            var fileSystemParams = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory);
            var options = new EditorSimulateModeOptions
            {
                EditorFileSystemParameters = fileSystemParams
            };
            return package.InitializePackageAsync(options);
        }

        private static InitializePackageOperation InitOfflineMode(ResourcePackage package, YooInitConfig config)
        {
            var fileSystemParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
            ApplyDecryptor(fileSystemParams, config);
            var options = new OfflinePlayModeOptions
            {
                BuiltinFileSystemParameters = fileSystemParams
            };
            return package.InitializePackageAsync(options);
        }

        private static InitializePackageOperation InitHostMode(ResourcePackage package, YooInitConfig config)
        {
            if (HostModeHandler is not null)
                return HostModeHandler(package, config);

            throw new InvalidOperationException(
                "[YooInit] HostPlayMode 需要配置远程服务。请在调用 InitAsync 前设置 YooInit.HostModeHandler 委托。\n" +
                "3.x 示例:\n" +
                "YooInit.HostModeHandler = (pkg, cfg) => {\n" +
                "    // 实现 IRemoteService 接口\n" +
                "    IRemoteService remoteService = new MyRemoteService(\"http://cdn.example.com\", \"http://fallback.example.com\");\n" +
                "    var builtinParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();\n" +
                "    var cacheParams = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService);\n" +
                "    var options = new HostPlayModeOptions {\n" +
                "        BuiltinFileSystemParameters = builtinParams,\n" +
                "        CacheFileSystemParameters = cacheParams\n" +
                "    };\n" +
                "    return pkg.InitializePackageAsync(options);\n" +
                "};");
        }

        private static InitializePackageOperation InitWebMode(ResourcePackage package, YooInitConfig config)
        {
            if (WebModeHandler is not null)
                return WebModeHandler(package, config);

            throw new InvalidOperationException(
                "[YooInit] WebPlayMode 需要配置 WebGL 远程服务。请在调用 InitAsync 前设置 YooInit.WebModeHandler 委托。");
        }

        private static InitializePackageOperation CreateInitOperation(ResourcePackage package, YooInitConfig config)
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

        private static InitializePackageOperation InitCustomMode(ResourcePackage package, YooInitConfig config)
        {
            if (CustomHandler is null)
            {
                throw new InvalidOperationException(
                    "[YooInit] CustomPlayMode 需要配置自定义初始化逻辑。请在调用 InitAsync 前设置 YooInit.CustomHandler 委托。\n" +
                    "3.x 示例:\n" +
                    "YooInit.CustomHandler = (pkg, cfg) => {\n" +
                    "    var builtinParams = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();\n" +
                    "    var options = new OfflinePlayModeOptions { BuiltinFileSystemParameters = builtinParams };\n" +
                    "    return pkg.InitializePackageAsync(options);\n" +
                    "};");
            }
            return CustomHandler(package, config);
        }

        /// <summary>
        /// 将配置中的解密器应用到 FileSystemParameters
        /// </summary>
        private static void ApplyDecryptor(FileSystemParameters parameters, YooInitConfig config)
        {
            var decryptor = config.CreateBundleDecryptor();
            if (decryptor is not null)
            {
                parameters.AddParameter(EFileSystemParameter.AssetBundleDecryptor, decryptor);
            }
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
