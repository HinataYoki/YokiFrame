#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_2_3_OR_NEWER && !YOOASSET_3_0_OR_NEWER
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
    /// YooInit 初始化逻辑 — 2.3.x 版本（string-based 包管理）
    /// </summary>
    public static partial class YooInit
    {
#if YOKIFRAME_UNITASK_SUPPORT
        #region 初始化 - UniTask 版本

        public static UniTask InitAsync(CancellationToken ct = default)
            => InitAsync(new YooInitConfig(), ct);

        public static async UniTask InitAsync(YooInitConfig config, CancellationToken ct = default)
        {
            if (Initialized) return;
            if (config is null) throw new ArgumentNullException(nameof(config));

            KitLogger.Log($"[YooInit] 开始初始化，模式: {config.PlayMode}");

            YooAssets.Initialize();

            var packageNames = GetPackageNamesInternal();

            if (config.PackageNames is { Count: > 0 })
            {
                for (int i = 0; i < config.PackageNames.Count; i++)
                {
                    var pn = config.PackageNames[i];
                    if (string.IsNullOrEmpty(pn)) continue;
                    packageNames.Add(pn);
                    if (i == 0) SetDefaultPackageName(pn);
                }
            }

            // 2.3.x: YooAssets.Initialize(parameters) 用静态 API 一步完成初始化
            InitializationOperation operation = CreateInitOperation(config);
            await operation.ToUniTask(cancellationToken: ct);

            if (operation.Status != EOperationStatus.Succeed)
            {
                throw new Exception($"[YooInit] 初始化失败: {operation.Error}");
            }

            // 2.3.x: 版本/清单在 Initialize 内部处理（不需要额外调用）

            ConfigureResKit();

            SetInitialized(true);
            KitLogger.Log($"[YooInit] 初始化完成，资源包数量: {packageNames.Count}");
        }

        #endregion
#else
        #region 初始化 - 协程版本

        public static IEnumerator InitAsync(Action onComplete = null)
            => InitAsync(new YooInitConfig(), onComplete);

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

            var packageNames = GetPackageNamesInternal();

            if (config.PackageNames is { Count: > 0 })
            {
                for (int i = 0; i < config.PackageNames.Count; i++)
                {
                    var pn = config.PackageNames[i];
                    if (string.IsNullOrEmpty(pn)) continue;
                    packageNames.Add(pn);
                    if (i == 0) SetDefaultPackageName(pn);
                }
            }

            InitializationOperation operation = CreateInitOperation(config);
            yield return operation;

            if (operation.Status != EOperationStatus.Succeed)
            {
                throw new Exception($"[YooInit] 初始化失败: {operation.Error}");
            }

            ConfigureResKit();

            SetInitialized(true);
            KitLogger.Log($"[YooInit] 初始化完成，资源包数量: {packageNames.Count}");
            onComplete?.Invoke();
        }

        #endregion
#endif

        #region 初始化模式

        private static InitializationOperation CreateInitOperation(YooInitConfig config)
        {
            return config.PlayMode switch
            {
                EPlayMode.EditorSimulateMode => InitEditorSimulateMode(config),
                EPlayMode.OfflinePlayMode => InitOfflineMode(config),
                EPlayMode.HostPlayMode => InitHostMode(config),
                EPlayMode.WebPlayMode => InitWebMode(config),
                EPlayMode.CustomPlayMode => InitCustomMode(config),
                _ => throw new NotSupportedException($"[YooInit] 不支持的播放模式: {config.PlayMode}")
            };
        }

        private static InitializationOperation InitEditorSimulateMode(YooInitConfig config)
        {
            if (CustomHandler is not null)
                return CustomHandler(DefaultPackageName, config);

            throw new InvalidOperationException(
                "[YooInit] EditorSimulateMode 需要设置 YooInit.CustomHandler 委托。\n" +
                "2.3.x 示例:\n" +
                "YooInit.CustomHandler = (packageName, cfg) => {\n" +
                "    var simulateParams = new EditorSimulateModeParameters();\n" +
                "    simulateParams.SimulateManifestFilePath = EditorSimulateModeHelper.SimulateBuild(packageName);\n" +
                "    return YooAssets.Initialize(simulateParams);\n" +
                "};");
        }

        private static InitializationOperation InitOfflineMode(YooInitConfig config)
        {
            if (CustomHandler is not null)
                return CustomHandler(DefaultPackageName, config);

            throw new InvalidOperationException(
                "[YooInit] OfflinePlayMode 需要设置 YooInit.CustomHandler 委托。\n" +
                "2.3.x 示例:\n" +
                "YooInit.CustomHandler = (packageName, cfg) => {\n" +
                "    var offlineParams = new OfflinePlayModeParameters();\n" +
                "    return YooAssets.Initialize(offlineParams);\n" +
                "};");
        }

        private static InitializationOperation InitHostMode(YooInitConfig config)
        {
            if (HostModeHandler is not null)
                return HostModeHandler(DefaultPackageName, config);
            if (CustomHandler is not null)
                return CustomHandler(DefaultPackageName, config);

            throw new InvalidOperationException(
                "[YooInit] HostPlayMode 需要设置 YooInit.HostModeHandler 委托。\n" +
                "2.3.x 示例:\n" +
                "YooInit.HostModeHandler = (packageName, cfg) => {\n" +
                "    var hostParams = new HostPlayModeParameters {\n" +
                "        DefaultHostServer = \"http://cdn.example.com\",\n" +
                "        FallbackHostServer = \"http://fallback.example.com\"\n" +
                "    };\n" +
                "    return YooAssets.Initialize(hostParams);\n" +
                "};");
        }

        private static InitializationOperation InitWebMode(YooInitConfig config)
        {
            if (WebModeHandler is not null)
                return WebModeHandler(DefaultPackageName, config);
            if (CustomHandler is not null)
                return CustomHandler(DefaultPackageName, config);

            throw new InvalidOperationException(
                "[YooInit] WebPlayMode 需要设置 YooInit.WebModeHandler 委托。");
        }

        private static InitializationOperation InitCustomMode(YooInitConfig config)
        {
            if (CustomHandler is null)
            {
                throw new InvalidOperationException(
                    "[YooInit] CustomPlayMode 需要设置 YooInit.CustomHandler 委托。");
            }
            return CustomHandler(DefaultPackageName, config);
        }

        private static void ConfigureResKit()
        {
            // 2.3.x: ResKit 加载器使用无参构造函数（YooAssets 静态 API 内部处理包）
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
