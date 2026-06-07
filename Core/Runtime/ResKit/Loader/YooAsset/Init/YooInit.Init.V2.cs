#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_2_3_OR_NEWER
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
    /// YooInit 初始化逻辑 — 2.x 版本
    /// 2.x 使用 YooAssets 静态 API，无 ResourcePackage 概念
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

            var packageNames = GetPackageNamesInternal();

            // 2.x 不支持多包，仅使用第一个包名作为标识
            if (config.PackageNames is { Count: > 0 })
            {
                for (int i = 0; i < config.PackageNames.Count; i++)
                {
                    var packageName = config.PackageNames[i];
                    if (string.IsNullOrEmpty(packageName)) continue;

                    packageNames.Add(packageName);

                    // 第一个包设为默认包
                    if (i == 0)
                    {
                        SetDefaultPackageName(packageName);
                    }
                }
            }

            // 2.x: 直接使用 YooAssets.Initialize(parameters) 一步完成初始化
            InitializationOperation operation = CreateInitOperation(config);
            await operation.ToUniTask(cancellationToken: ct);

            if (operation.Status != EOperationStatus.Succeed)
            {
                throw new Exception($"[YooInit] 初始化失败: {operation.Error}");
            }

            // 请求版本号（2.x 通过 YooAssets 静态方法）
            var versionOp = YooAssets.UpdatePackageVersionAsync();
            await versionOp.ToUniTask(cancellationToken: ct);

            if (versionOp.Status != EOperationStatus.Succeed)
            {
                KitLogger.Warning($"[YooInit] 请求版本失败: {versionOp.Error}");
            }
            else
            {
                // 更新资源清单
                var manifestOp = YooAssets.UpdatePackageManifestAsync(versionOp.PackageVersion);
                await manifestOp.ToUniTask(cancellationToken: ct);

                if (manifestOp.Status != EOperationStatus.Succeed)
                {
                    KitLogger.Warning($"[YooInit] 更新清单失败: {manifestOp.Error}");
                }
            }

            // 配置 ResKit 加载器
            ConfigureResKit();

            SetInitialized(true);
            KitLogger.Log($"[YooInit] 初始化完成，资源包数量: {packageNames.Count}");
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

            var packageNames = GetPackageNamesInternal();

            if (config.PackageNames is { Count: > 0 })
            {
                for (int i = 0; i < config.PackageNames.Count; i++)
                {
                    var packageName = config.PackageNames[i];
                    if (string.IsNullOrEmpty(packageName)) continue;

                    packageNames.Add(packageName);

                    if (i == 0)
                    {
                        SetDefaultPackageName(packageName);
                    }
                }
            }

            // 2.x: 直接使用 YooAssets.Initialize
            InitializationOperation operation = CreateInitOperation(config);
            yield return operation;

            if (operation.Status != EOperationStatus.Succeed)
            {
                throw new Exception($"[YooInit] 初始化失败: {operation.Error}");
            }

            // 请求版本号
            var versionOp = YooAssets.UpdatePackageVersionAsync();
            yield return versionOp;

            if (versionOp.Status != EOperationStatus.Succeed)
            {
                KitLogger.Warning($"[YooInit] 请求版本失败: {versionOp.Error}");
            }
            else
            {
                var manifestOp = YooAssets.UpdatePackageManifestAsync(versionOp.PackageVersion);
                yield return manifestOp;

                if (manifestOp.Status != EOperationStatus.Succeed)
                {
                    KitLogger.Warning($"[YooInit] 更新清单失败: {manifestOp.Error}");
                }
            }

            // 配置 ResKit 加载器
            ConfigureResKit();

            SetInitialized(true);
            KitLogger.Log($"[YooInit] 初始化完成，资源包数量: {packageNames.Count}");
            onComplete?.Invoke();
        }

        #endregion
#endif

        #region 初始化模式

        private static InitializationOperation InitEditorSimulateMode()
        {
            var simulateParams = new EditorSimulateModeParameters();
            // 2.x: SimulateBuild() 无参数，返回 manifest 文件路径
            simulateParams.SimulateManifestFilePath = EditorSimulateModeHelper.SimulateBuild();
            return YooAssets.Initialize(simulateParams);
        }

        private static InitializationOperation InitOfflineMode()
        {
            var offlineParams = new OfflinePlayModeParameters();
            return YooAssets.Initialize(offlineParams);
        }

        private static InitializationOperation InitHostMode(YooInitConfig config)
        {
            if (HostModeHandler is null)
            {
                throw new InvalidOperationException(
                    "[YooInit] HostPlayMode 需要配置远程服务。请在调用 InitAsync 前设置 YooInit.HostModeHandler 委托。\n" +
                    "2.x 示例:\n" +
                    "YooInit.HostModeHandler = (pkgName, cfg) => {\n" +
                    "    var hostParams = new HostPlayModeParameters {\n" +
                    "        DefaultHostServer = \"http://cdn.example.com\",\n" +
                    "        FallbackHostServer = \"http://fallback.example.com\"\n" +
                    "    };\n" +
                    "    return YooAssets.Initialize(hostParams);\n" +
                    "};");
            }
            return HostModeHandler(DefaultPackageName, config);
        }

        private static InitializationOperation InitWebMode(YooInitConfig config)
        {
            if (WebModeHandler is null)
            {
                throw new InvalidOperationException(
                    "[YooInit] WebPlayMode 需要配置 WebGL 远程服务。请在调用 InitAsync 前设置 YooInit.WebModeHandler 委托。");
            }
            return WebModeHandler(DefaultPackageName, config);
        }

        private static InitializationOperation CreateInitOperation(YooInitConfig config)
        {
            // 优先使用通用自定义处理器
            if (CustomHandler is not null)
                return CustomHandler(DefaultPackageName, config);

            return config.PlayMode switch
            {
                EPlayMode.EditorSimulateMode => InitEditorSimulateMode(),
                EPlayMode.OfflinePlayMode => InitOfflineMode(),
                EPlayMode.HostPlayMode => InitHostMode(config),
                EPlayMode.WebPlayMode => InitWebMode(config),
                EPlayMode.CustomPlayMode => InitCustomMode(config),
                _ => throw new NotSupportedException($"[YooInit] 不支持的播放模式: {config.PlayMode}，请设置 YooInit.CustomHandler 委托处理。")
            };
        }

        private static InitializationOperation InitCustomMode(YooInitConfig config)
        {
            if (CustomHandler is null)
            {
                throw new InvalidOperationException(
                    "[YooInit] CustomPlayMode 需要配置自定义初始化逻辑。请在调用 InitAsync 前设置 YooInit.CustomHandler 委托。");
            }
            return CustomHandler(DefaultPackageName, config);
        }

        private static void ConfigureResKit()
        {
#if YOKIFRAME_UNITASK_SUPPORT
            ResKit.SetLoaderPool(new YooAssetResLoaderUniTaskPool());
            ResKit.SetRawFileLoaderPool(new YooAssetRawFileLoaderUniTaskPool());
            ResKit.SetSceneLoaderPool(new YooAssetSceneLoaderUniTaskPool());
#else
            ResKit.SetLoaderPool(new YooAssetResLoaderPool());
            ResKit.SetRawFileLoaderPool(new YooAssetRawFileLoaderPool());
            ResKit.SetSceneLoaderPool(new YooAssetSceneLoaderPool());
#endif
        }

        #endregion
    }
}
#endif
