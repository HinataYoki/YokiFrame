#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
using UnityEngine;
using YooAsset;

namespace YokiFrame.Unity
{
    public static partial class YooAssetInitializer
    {
        /// <summary>按联网策略初始化 package，并在远端失败时执行明确的本地回退。</summary>
#if YOKIFRAME_UNITASK_SUPPORT
        private static async UniTask<ResourcePackage> InitializePackageWithStrategyAsync(
            string packageName,
            ResourcePackage package,
            YooAssetInitializationOptions options,
            CancellationToken token)
#else
        private static async Task<ResourcePackage> InitializePackageWithStrategyAsync(
            string packageName,
            ResourcePackage package,
            YooAssetInitializationOptions options,
            CancellationToken token)
#endif
        {
            ValidateStrategy(options);
            if (!ShouldRunRemoteUpdate(options))
            {
                await InitializePackageAsync(package, options, token);
                return package;
            }

            try
            {
                await InitializePackageAsync(package, options, token);
                await UpdateRemotePackageAsync(package, options, token);
                return package;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception remoteException)
            {
                return await RecoverAfterRemoteFailureAsync(
                    packageName,
                    package,
                    options,
                    remoteException,
                    token);
            }
        }

        /// <summary>校验联网策略只用于它能够提供完整语义的运行模式。</summary>
        private static void ValidateStrategy(YooAssetInitializationOptions options)
        {
            if (options.InitializationStrategy == YooAssetInitializationStrategy.ManifestOnly
                || (options.PlayMode != EPlayMode.HostPlayMode
                    && options.PlayMode != EPlayMode.WebPlayMode))
                return;

            if (options.PlayMode == EPlayMode.WebPlayMode
                && options.InitializationStrategy != YooAssetInitializationStrategy.RemoteOnly)
            {
                throw new InvalidOperationException(
                    "WebPlayMode does not support local package fallback strategies.");
            }
        }

        /// <summary>判断当前配置是否需要在 package 初始化后执行远端更新流程。</summary>
        private static bool ShouldRunRemoteUpdate(YooAssetInitializationOptions options)
        {
            return options.InitializationStrategy != YooAssetInitializationStrategy.ManifestOnly
                && (options.PlayMode == EPlayMode.HostPlayMode
                    || options.PlayMode == EPlayMode.WebPlayMode);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>执行远端版本和清单检查；Host 还下载全部缺失资源后记录完整版本。</summary>
        private static async UniTask UpdateRemotePackageAsync(
            ResourcePackage package,
            YooAssetInitializationOptions options,
            CancellationToken token)
#else
        /// <summary>执行远端版本和清单检查；Host 还下载全部缺失资源后记录完整版本。</summary>
        private static async Task UpdateRemotePackageAsync(
            ResourcePackage package,
            YooAssetInitializationOptions options,
            CancellationToken token)
#endif
        {
            await LoadPackageManifestAsync(
                package,
                options.GetManifestTimeoutSeconds(),
                options.AppendTimestampToVersionRequest,
                token);
            if (options.PlayMode == EPlayMode.WebPlayMode)
                return;

#if YOKIFRAME_YOOASSET_3
            ResourceDownloaderOperation downloader = package.CreateResourceDownloader(
                new ResourceDownloaderOptions(
                    options.GetDownloadMaximumConcurrency(),
                    options.GetDownloadRetryCount()));
#else
            ResourceDownloaderOperation downloader = package.CreateResourceDownloader(
                options.GetDownloadMaximumConcurrency(),
                options.GetDownloadRetryCount());
#endif
            await DownloadPackageAsync(downloader, options, token);

            SaveSuccessfulVersion(package);
        }

        /// <summary>有缺失文件时启动下载并转发进度；空下载器只返回，不触发回调。</summary>
#if YOKIFRAME_UNITASK_SUPPORT
        private static async UniTask DownloadPackageAsync(
            ResourceDownloaderOperation downloader,
            YooAssetInitializationOptions options,
            CancellationToken token)
#else
        /// <summary>有缺失文件时启动下载并转发进度；空下载器只返回，不触发回调。</summary>
        private static async Task DownloadPackageAsync(
            ResourceDownloaderOperation downloader,
            YooAssetInitializationOptions options,
            CancellationToken token)
#endif
        {
            if (downloader.TotalDownloadCount <= 0)
                return;

            YooAssetDownloaderObserver.Attach(
                downloader,
                options.OnPackageDownloadProgress,
                options.OnPackageDownloadError,
                options.OnPackageDownloadFileBegin);
#if YOKIFRAME_YOOASSET_3
            downloader.StartDownload();
#else
            downloader.BeginDownload();
#endif
            await YooAssetOperationAwaiter.WaitAsync(downloader, token);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>按策略恢复远端失败；异常链保留在最终失败信息中。</summary>
        private static async UniTask<ResourcePackage> RecoverAfterRemoteFailureAsync(
            string packageName,
            ResourcePackage package,
            YooAssetInitializationOptions options,
            Exception remoteException,
            CancellationToken token)
#else
        /// <summary>按策略恢复远端失败；异常链保留在最终失败信息中。</summary>
        private static async Task<ResourcePackage> RecoverAfterRemoteFailureAsync(
            string packageName,
            ResourcePackage package,
            YooAssetInitializationOptions options,
            Exception remoteException,
            CancellationToken token)
#endif
        {
            if (options.InitializationStrategy == YooAssetInitializationStrategy.RemoteOnly)
                throw CreateRemoteFailureException(packageName, remoteException, null);

            if (options.InitializationStrategy == YooAssetInitializationStrategy.RemoteThenCached)
            {
                try
                {
                    await LoadSuccessfulLocalVersionAsync(package, options, token);
                    return package;
                }
                catch (Exception cachedException) when (!(cachedException is OperationCanceledException))
                {
                    try
                    {
                        return await InitializeOfflinePackageAsync(packageName, package, options, token);
                    }
                    catch (Exception offlineException) when (!(offlineException is OperationCanceledException))
                    {
                        throw CreateRemoteFailureException(
                            packageName,
                            remoteException,
                            new AggregateException(cachedException, offlineException));
                    }
                }
            }

            try
            {
                return await InitializeOfflinePackageAsync(packageName, package, options, token);
            }
            catch (Exception offlineException) when (!(offlineException is OperationCanceledException))
            {
                throw CreateRemoteFailureException(packageName, remoteException, offlineException);
            }
        }

        /// <summary>从本地持久化的完整版本恢复清单，并确认所有资源均已缓存。</summary>
#if YOKIFRAME_UNITASK_SUPPORT
        private static async UniTask LoadSuccessfulLocalVersionAsync(
            ResourcePackage package,
            YooAssetInitializationOptions options,
            CancellationToken token)
#else
        private static async Task LoadSuccessfulLocalVersionAsync(
            ResourcePackage package,
            YooAssetInitializationOptions options,
            CancellationToken token)
#endif
        {
            string version = PlayerPrefs.GetString(GetVersionKey(package.PackageName), string.Empty);
            if (string.IsNullOrWhiteSpace(version))
                throw new InvalidOperationException("No previously completed YooAsset version is recorded.");

            await LoadPackageManifestAsync(
                package,
                options.GetManifestTimeoutSeconds(),
                options.AppendTimestampToVersionRequest,
                token,
                version);
#if YOKIFRAME_YOOASSET_3
            ResourceDownloaderOperation downloader = package.CreateResourceDownloader(
                new ResourceDownloaderOptions(1, 0));
#else
            ResourceDownloaderOperation downloader = package.CreateResourceDownloader(1, 0);
#endif
            if (downloader.TotalDownloadCount > 0)
            {
                throw new InvalidOperationException(
                    "The recorded YooAsset version is incomplete in the local cache.");
            }
        }

        /// <summary>销毁联网 package 后以 OfflinePlayMode 重新创建并加载包体清单。</summary>
#if YOKIFRAME_UNITASK_SUPPORT
        private static async UniTask<ResourcePackage> InitializeOfflinePackageAsync(
            string packageName,
            ResourcePackage package,
            YooAssetInitializationOptions options,
            CancellationToken token)
#else
        private static async Task<ResourcePackage> InitializeOfflinePackageAsync(
            string packageName,
            ResourcePackage package,
            YooAssetInitializationOptions options,
            CancellationToken token)
#endif
        {
            await DestroyAndRemovePackageAsync(packageName, package, token);
            ResourcePackage offlinePackage = GetOrCreatePackage(packageName);
#if YOKIFRAME_YOOASSET_3
            InitializePackageOperation operation = CreateOfflineOperation(offlinePackage, options);
#else
            InitializationOperation operation = CreateOfflineOperation(offlinePackage, options);
#endif
            await YooAssetOperationAwaiter.WaitAsync(operation, token);
            if (!offlinePackage.PackageValid)
            {
                await LoadPackageManifestAsync(
                    offlinePackage,
                    options.GetManifestTimeoutSeconds(),
                    options.AppendTimestampToVersionRequest,
                    token);
            }

            return offlinePackage;
        }

        /// <summary>等待 package 销毁完成后从 YooAsset 全局注册表移除它。</summary>
#if YOKIFRAME_UNITASK_SUPPORT
        private static async UniTask DestroyAndRemovePackageAsync(
            string packageName,
            ResourcePackage package,
            CancellationToken token)
#else
        private static async Task DestroyAndRemovePackageAsync(
            string packageName,
            ResourcePackage package,
            CancellationToken token)
#endif
        {
            if (package == null)
                return;

#if YOKIFRAME_YOOASSET_3
            DestroyPackageOperation operation = package.DestroyPackageAsync();
#else
            DestroyOperation operation = package.DestroyAsync();
#endif
            await YooAssetOperationAwaiter.WaitAsync(operation, token);
#if YOKIFRAME_YOOASSET_3
            YooAssets.RemovePackage(packageName);
#else
            YooAssets.RemovePackage(package);
#endif
        }

        /// <summary>保存远端下载完整成功的版本，避免清单成功但资源不完整时误回退。</summary>
        private static void SaveSuccessfulVersion(ResourcePackage package)
        {
            PlayerPrefs.SetString(GetVersionKey(package.PackageName), package.GetPackageVersion());
            PlayerPrefs.Save();
        }

        /// <summary>生成按 package 隔离的本地成功版本键。</summary>
        private static string GetVersionKey(string packageName)
        {
            return "YokiFrame.YooAsset.SuccessfulVersion." + packageName;
        }

        /// <summary>把远端、缓存和离线失败组合成可诊断的初始化异常。</summary>
        private static InvalidOperationException CreateRemoteFailureException(
            string packageName,
            Exception remoteException,
            Exception fallbackException)
        {
            string message = "YooAsset package '" + packageName + "' remote initialization failed.";
            if (fallbackException != null)
            {
                message += " Local fallback failed.";
                return new InvalidOperationException(
                    message,
                    new AggregateException(remoteException, fallbackException));
            }

            return new InvalidOperationException(message, remoteException);
        }

        /// <summary>按当前激活清单创建下载器。标签为空时覆盖全部缺失资源。</summary>
        private static ResourceDownloaderOperation CreateActiveDownloader(
            ResourcePackage package,
            YooAssetInitializationOptions options,
            string[] tags)
        {
            bool hasTags = tags != null && tags.Length > 0;
#if YOKIFRAME_YOOASSET_3
            ResourceDownloaderOptions downloaderOptions = hasTags
                ? new ResourceDownloaderOptions(
                    tags,
                    options.GetDownloadMaximumConcurrency(),
                    options.GetDownloadRetryCount())
                : new ResourceDownloaderOptions(
                    options.GetDownloadMaximumConcurrency(),
                    options.GetDownloadRetryCount());
            return package.CreateResourceDownloader(downloaderOptions);
#else
            if (hasTags)
            {
                return package.CreateResourceDownloader(
                    tags,
                    options.GetDownloadMaximumConcurrency(),
                    options.GetDownloadRetryCount());
            }

            return package.CreateResourceDownloader(
                options.GetDownloadMaximumConcurrency(),
                options.GetDownloadRetryCount());
#endif
        }

        /// <summary>下载当前激活清单的缺失资源，供版本更新在激活清单后使用。</summary>
#if YOKIFRAME_UNITASK_SUPPORT
        private static UniTask DownloadActivePackageAsync(
            ResourcePackage package,
            YooAssetInitializationOptions options,
            CancellationToken token)
#else
        /// <summary>下载当前激活清单的缺失资源，供版本更新在激活清单后使用。</summary>
        private static Task DownloadActivePackageAsync(
            ResourcePackage package,
            YooAssetInitializationOptions options,
            CancellationToken token)
#endif
        {
            ResourceDownloaderOperation downloader = CreateActiveDownloader(package, options, null);
            return DownloadPackageAsync(downloader, options, token);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>预载指定版本清单并创建下载器，不把该清单设为当前激活清单。</summary>
        private static async UniTask<ResourceDownloaderOperation> CreatePrefetchDownloaderAsync(
            ResourcePackage package,
            string packageVersion,
            YooAssetInitializationOptions options,
            CancellationToken token)
#else
        /// <summary>预载指定版本清单并创建下载器，不把该清单设为当前激活清单。</summary>
        private static async Task<ResourceDownloaderOperation> CreatePrefetchDownloaderAsync(
            ResourcePackage package,
            string packageVersion,
            YooAssetInitializationOptions options,
            CancellationToken token)
#endif
        {
#if YOKIFRAME_YOOASSET_3
            PrefetchManifestOptions prefetchOptions = new(packageVersion, options.GetManifestTimeoutSeconds());
            PrefetchManifestOperation prefetch = package.PrefetchManifestAsync(prefetchOptions);
#else
            PreDownloadContentOperation prefetch = package.PreDownloadContentAsync(
                packageVersion,
                options.GetManifestTimeoutSeconds());
#endif
            await YooAssetOperationAwaiter.WaitAsync(prefetch, token);
#if YOKIFRAME_YOOASSET_3
            return prefetch.CreateResourceDownloader(new ResourceDownloaderOptions(
                options.GetDownloadMaximumConcurrency(),
                options.GetDownloadRetryCount()));
#else
            return prefetch.CreateResourceDownloader(
                options.GetDownloadMaximumConcurrency(),
                options.GetDownloadRetryCount());
#endif
        }
    }
}
#endif
