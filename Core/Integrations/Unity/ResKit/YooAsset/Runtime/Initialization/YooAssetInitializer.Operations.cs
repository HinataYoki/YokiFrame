#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
using YooAsset;

namespace YokiFrame.Unity
{
    public static partial class YooAssetInitializer
    {
#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>初始化单个 package，并按需加载远端或内置 manifest。</summary>
        private static async UniTask InitializePackageAsync(
            ResourcePackage package,
            YooAssetInitializationOptions options,
            CancellationToken token)
#else
        /// <summary>初始化单个 package，并按需加载远端或内置 manifest。</summary>
        private static async Task InitializePackageAsync(
            ResourcePackage package,
            YooAssetInitializationOptions options,
            CancellationToken token)
#endif
        {
            if (!IsPackageInitialized(package))
            {
#if YOKIFRAME_YOOASSET_3
                InitializePackageOperation operation = CreateInitializationOperation(package, options);
#else
                InitializationOperation operation = CreateInitializationOperation(package, options);
#endif
                await YooAssetOperationAwaiter.WaitAsync(operation, token);
            }

            if (options.LoadManifestAfterInitialization && !package.PackageValid)
                await LoadPackageManifestAsync(package, options.GetManifestTimeoutSeconds(), token);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>请求 package 版本并加载对应 manifest。</summary>
        private static async UniTask LoadPackageManifestAsync(
            ResourcePackage package,
            int timeoutSeconds,
            CancellationToken token)
#else
        /// <summary>请求 package 版本并加载对应 manifest。</summary>
        private static async Task LoadPackageManifestAsync(
            ResourcePackage package,
            int timeoutSeconds,
            CancellationToken token)
#endif
        {
#if YOKIFRAME_YOOASSET_3
            RequestPackageVersionOptions versionOptions = new(true, timeoutSeconds);
            RequestPackageVersionOperation version = package.RequestPackageVersionAsync(versionOptions);
#else
            RequestPackageVersionOperation version = package.RequestPackageVersionAsync(true, timeoutSeconds);
#endif
            await YooAssetOperationAwaiter.WaitAsync(version, token);

#if YOKIFRAME_YOOASSET_3
            LoadPackageManifestOptions manifestOptions = new(version.PackageVersion, timeoutSeconds);
            LoadPackageManifestOperation manifest = package.LoadPackageManifestAsync(manifestOptions);
#else
            UpdatePackageManifestOperation manifest =
                package.UpdatePackageManifestAsync(version.PackageVersion, timeoutSeconds);
#endif
            await YooAssetOperationAwaiter.WaitAsync(manifest, token);
        }

        /// <summary>按 YooAsset 主版本判断 package 初始化操作是否已经成功。</summary>
        private static bool IsPackageInitialized(ResourcePackage package)
        {
#if YOKIFRAME_YOOASSET_3
            return package.InitializeStatus == EOperationStatus.Succeeded;
#else
            return package.InitializeStatus == EOperationStatus.Succeed;
#endif
        }

#if YOKIFRAME_YOOASSET_3
        /// <summary>按配置创建 YooAsset V3 package 初始化操作。</summary>
        private static InitializePackageOperation CreateInitializationOperation(
            ResourcePackage package,
            YooAssetInitializationOptions options)
#else
        /// <summary>按配置创建 YooAsset V2 package 初始化操作。</summary>
        private static InitializationOperation CreateInitializationOperation(
            ResourcePackage package,
            YooAssetInitializationOptions options)
#endif
        {
            switch (options.PlayMode)
            {
                case EPlayMode.EditorSimulateMode:
                    return CreateEditorSimulateOperation(package);
                case EPlayMode.OfflinePlayMode:
                    return CreateOfflineOperation(package, options);
                case EPlayMode.HostPlayMode:
                    return CreateHostOperation(package, options);
                case EPlayMode.WebPlayMode:
                    return CreateWebOperation(package, options);
                case EPlayMode.CustomPlayMode:
                    return CreateCustomOperation(package, options);
                default:
                    throw new NotSupportedException(
                        "Unsupported YooAsset play mode: " + options.PlayMode);
            }
        }

#if YOKIFRAME_YOOASSET_3
        /// <summary>创建 YooAsset V3 EditorSimulate 初始化操作。</summary>
        private static InitializePackageOperation CreateEditorSimulateOperation(ResourcePackage package)
        {
#if UNITY_EDITOR
            var buildResult = EditorSimulateBuildInvoker.Build(
                package.PackageName,
                (int)EBundleType.VirtualAssetBundle);
            FileSystemParameters fileSystem =
                FileSystemParameters.CreateDefaultEditorFileSystemParameters(
                    buildResult.PackageRootDirectory);
            EditorSimulateModeOptions options = new()
            {
                EditorFileSystemParameters = fileSystem
            };
            return package.InitializePackageAsync(options);
#else
            throw new InvalidOperationException(
                "EditorSimulateMode is only available in the Unity Editor.");
#endif
        }

        /// <summary>创建 YooAsset V3 Offline 初始化操作。</summary>
        private static InitializePackageOperation CreateOfflineOperation(
            ResourcePackage package,
            YooAssetInitializationOptions options)
        {
            FileSystemParameters fileSystem =
                FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
            ApplyBundleDecryptor(fileSystem, options);
            OfflinePlayModeOptions runtimeOptions = new()
            {
                BuiltinFileSystemParameters = fileSystem
            };
            return package.InitializePackageAsync(runtimeOptions);
        }

        /// <summary>把初始化配置中的 V3 Bundle 解密器写入内置文件系统参数。</summary>
        private static void ApplyBundleDecryptor(
            FileSystemParameters fileSystem,
            YooAssetInitializationOptions initializationOptions)
        {
            IBundleDecryptor decryptor = YooAssetEncryptionServices.CreateBundleDecryptor(
                initializationOptions);
            if (decryptor == null)
                return;

            fileSystem.AddParameter(EFileSystemParameter.AssetBundleDecryptor, decryptor);
            fileSystem.AddParameter(EFileSystemParameter.RawBundleDecryptor, decryptor);
            fileSystem.AddParameter(EFileSystemParameter.ArchiveBundleDecryptor, decryptor);
            if (decryptor is IBundleMemoryDecryptor memoryDecryptor)
            {
                fileSystem.AddParameter(
                    EFileSystemParameter.AssetBundleFallbackDecryptor,
                    memoryDecryptor);
            }
        }

        /// <summary>创建 YooAsset V3 Host 初始化操作，项目回调优先于默认文件系统。</summary>
        private static InitializePackageOperation CreateHostOperation(
            ResourcePackage package,
            YooAssetInitializationOptions options)
        {
            if (HostInitializationHandler != null)
            {
                return HostInitializationHandler(package, options);
            }

            IRemoteService remoteService = CreateRemoteService(options);
            FileSystemParameters builtinFileSystem =
                FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
            ApplyBundleDecryptor(builtinFileSystem, options);
            FileSystemParameters cacheFileSystem =
                FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService);
            ApplyBundleDecryptor(cacheFileSystem, options);
            HostPlayModeOptions runtimeOptions = new()
            {
                BuiltinFileSystemParameters = builtinFileSystem,
                CacheFileSystemParameters = cacheFileSystem
            };
            return package.InitializePackageAsync(runtimeOptions);
        }

        /// <summary>创建 YooAsset V3 Web 初始化操作，项目回调优先于默认文件系统。</summary>
        private static InitializePackageOperation CreateWebOperation(
            ResourcePackage package,
            YooAssetInitializationOptions options)
        {
            if (WebInitializationHandler != null)
            {
                return WebInitializationHandler(package, options);
            }

            IRemoteService remoteService = CreateRemoteService(options);
            FileSystemParameters webServerFileSystem =
                FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
            ApplyBundleDecryptor(webServerFileSystem, options);
            FileSystemParameters webNetworkFileSystem =
                FileSystemParameters.CreateDefaultWebNetworkFileSystemParameters(remoteService);
            ApplyBundleDecryptor(webNetworkFileSystem, options);
            WebPlayModeOptions runtimeOptions = new()
            {
                WebServerFileSystemParameters = webServerFileSystem,
                WebNetworkFileSystemParameters = webNetworkFileSystem
            };
            return package.InitializePackageAsync(runtimeOptions);
        }

        /// <summary>通过项目回调创建 YooAsset V3 Custom 初始化操作。</summary>
        private static InitializePackageOperation CreateCustomOperation(
            ResourcePackage package,
            YooAssetInitializationOptions options)
        {
            if (CustomInitializationHandler == null)
            {
                throw new InvalidOperationException(
                    "CustomPlayMode requires CustomInitializationHandler.");
            }

            return CustomInitializationHandler(package, options);
        }

        /// <summary>校验远端地址并创建 YooAsset V3 主备资源服务。</summary>
        private static IRemoteService CreateRemoteService(YooAssetInitializationOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.DefaultHostServer))
            {
                throw new InvalidOperationException(
                    "Host/Web play mode requires DefaultHostServer.");
            }

            return new YooAssetRemoteServices(
                options.DefaultHostServer,
                options.FallbackHostServer);
        }
#else
        /// <summary>创建 YooAsset V2 EditorSimulate 初始化操作。</summary>
        private static InitializationOperation CreateEditorSimulateOperation(ResourcePackage package)
        {
#if UNITY_EDITOR
            var buildResult = EditorSimulateModeHelper.SimulateBuild(package.PackageName);
            EditorSimulateModeParameters parameters = new()
            {
                EditorFileSystemParameters =
                    FileSystemParameters.CreateDefaultEditorFileSystemParameters(
                        buildResult.PackageRootDirectory)
            };
            return package.InitializeAsync(parameters);
#else
            throw new InvalidOperationException(
                "EditorSimulateMode is only available in the Unity Editor.");
#endif
        }

        /// <summary>创建 YooAsset V2 Offline 初始化操作。</summary>
        private static InitializationOperation CreateOfflineOperation(
            ResourcePackage package,
            YooAssetInitializationOptions options)
        {
            OfflinePlayModeParameters parameters = new()
            {
                BuildinFileSystemParameters =
                    FileSystemParameters.CreateDefaultBuildinFileSystemParameters(
                        YooAssetEncryptionServices.CreateDecryptionServices(options))
            };
            return package.InitializeAsync(parameters);
        }

        /// <summary>创建 YooAsset V2 Host 初始化操作，项目回调优先于默认远端服务。</summary>
        private static InitializationOperation CreateHostOperation(
            ResourcePackage package,
            YooAssetInitializationOptions options)
        {
            if (HostInitializationHandler != null)
                return HostInitializationHandler(package, options);

            YooAssetRemoteServices remoteServices = CreateRemoteServices(options);
            HostPlayModeParameters parameters = new()
            {
                BuildinFileSystemParameters =
                    FileSystemParameters.CreateDefaultBuildinFileSystemParameters(
                        YooAssetEncryptionServices.CreateDecryptionServices(options)),
                CacheFileSystemParameters =
                    FileSystemParameters.CreateDefaultCacheFileSystemParameters(
                        remoteServices,
                        YooAssetEncryptionServices.CreateDecryptionServices(options))
            };
            return package.InitializeAsync(parameters);
        }

        /// <summary>创建 YooAsset V2 Web 初始化操作，项目回调优先于默认远端服务。</summary>
        private static InitializationOperation CreateWebOperation(
            ResourcePackage package,
            YooAssetInitializationOptions options)
        {
            if (WebInitializationHandler != null)
                return WebInitializationHandler(package, options);

            YooAssetRemoteServices remoteServices = CreateRemoteServices(options);
            WebPlayModeParameters parameters = new()
            {
                WebServerFileSystemParameters =
                    FileSystemParameters.CreateDefaultWebServerFileSystemParameters(),
                WebRemoteFileSystemParameters =
                    FileSystemParameters.CreateDefaultWebRemoteFileSystemParameters(remoteServices)
            };
            return package.InitializeAsync(parameters);
        }

        /// <summary>通过项目回调创建 YooAsset V2 Custom 初始化操作。</summary>
        private static InitializationOperation CreateCustomOperation(
            ResourcePackage package,
            YooAssetInitializationOptions options)
        {
            if (CustomInitializationHandler == null)
            {
                throw new InvalidOperationException(
                    "CustomPlayMode requires CustomInitializationHandler.");
            }

            return CustomInitializationHandler(package, options);
        }

        /// <summary>校验远端地址并创建 YooAsset V2 远端服务。</summary>
        private static YooAssetRemoteServices CreateRemoteServices(
            YooAssetInitializationOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.DefaultHostServer))
            {
                throw new InvalidOperationException(
                    "Host/Web play mode requires DefaultHostServer.");
            }

            return new YooAssetRemoteServices(
                options.DefaultHostServer,
                options.FallbackHostServer);
        }
#endif
    }
}
#endif
