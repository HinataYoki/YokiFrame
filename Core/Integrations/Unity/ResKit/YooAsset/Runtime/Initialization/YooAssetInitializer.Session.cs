#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
using YokiFrame;
using YooAsset;

namespace YokiFrame.Unity
{
    public static partial class YooAssetInitializer
    {
#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 准备一个 package 并接入当前 Provider。
        /// Provider 尚未安装时只安装这一次；之后的 package 只追加到同一实例。
        /// </summary>
        /// <param name="packageName">package 名称。</param>
        /// <param name="options">运行模式、远端和下载参数。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>已经激活 manifest 的 package。</returns>
        public static async UniTask<ResourcePackage> InitializePackageAsync(
            string packageName,
            YooAssetInitializationOptions options,
            CancellationToken token = default)
#else
        /// <summary>
        /// 准备一个 package 并接入当前 Provider。
        /// Provider 尚未安装时只安装这一次；之后的 package 只追加到同一实例。
        /// </summary>
        /// <param name="packageName">package 名称。</param>
        /// <param name="options">运行模式、远端和下载参数。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>已经激活 manifest 的 package。</returns>
        public static async Task<ResourcePackage> InitializePackageAsync(
            string packageName,
            YooAssetInitializationOptions options,
            CancellationToken token = default)
#endif
        {
            EnsureSessionAvailable(options);
            sIsInitializing = true;
            try
            {
                return await InitializeRegisteredPackageAsync(packageName, options, token);
            }
            finally
            {
                sIsInitializing = false;
            }
        }

        /// <summary>
        /// 准备并接入一个 package。调用方必须已经持有初始化锁，避免批量入口再次抢锁。
        /// </summary>
#if YOKIFRAME_UNITASK_SUPPORT
        private static async UniTask<ResourcePackage> InitializeRegisteredPackageAsync(
            string packageName,
            YooAssetInitializationOptions options,
            CancellationToken token)
#else
        /// <summary>
        /// 准备并接入一个 package。调用方必须已经持有初始化锁，避免批量入口再次抢锁。
        /// </summary>
        private static async Task<ResourcePackage> InitializeRegisteredPackageAsync(
            string packageName,
            YooAssetInitializationOptions options,
            CancellationToken token)
#endif
        {
            packageName = NormalizePackageName(packageName);
            EnsureYooAssetsInitialized();
            ResourcePackage package = GetOrCreatePackage(packageName);
            package = await InitializePackageWithStrategyAsync(packageName, package, options, token);
            AttachPackage(package, options.PlayMode == EPlayMode.EditorSimulateMode);
            return package;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 为已接入的 package 请求并激活一份新清单。
        /// 不替换 Provider；未传版本时按配置请求远端版本。Web 模式只换清单，不整包下载。
        /// </summary>
        /// <param name="packageName">已经接入的 package 名称。</param>
        /// <param name="options">超时、时间戳和下载回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <param name="packageVersion">指定版本；为空时请求远端版本。</param>
        public static async UniTask UpdatePackageAsync(
            string packageName,
            YooAssetInitializationOptions options,
            CancellationToken token = default,
            string packageVersion = null)
#else
        /// <summary>
        /// 为已接入的 package 请求并激活一份新清单。
        /// 不替换 Provider；未传版本时按配置请求远端版本。Web 模式只换清单，不整包下载。
        /// </summary>
        /// <param name="packageName">已经接入的 package 名称。</param>
        /// <param name="options">超时、时间戳和下载回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <param name="packageVersion">指定版本；为空时请求远端版本。</param>
        public static async Task UpdatePackageAsync(
            string packageName,
            YooAssetInitializationOptions options,
            CancellationToken token = default,
            string packageVersion = null)
#endif
        {
            EnsureSessionAvailable(options);
            ResourcePackage package = RequireRegisteredPackage(packageName);
            sIsInitializing = true;
            try
            {
                await LoadPackageManifestAsync(
                    package,
                    options.GetManifestTimeoutSeconds(),
                    options.AppendTimestampToVersionRequest,
                    token,
                    packageVersion);
                if (options.PlayMode != EPlayMode.WebPlayMode
                    && options.InitializationStrategy != YooAssetInitializationStrategy.ManifestOnly)
                {
                    await DownloadActivePackageAsync(package, options, token);
                    SaveSuccessfulVersion(package);
                }
            }
            finally
            {
                sIsInitializing = false;
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 预下载指定版本的内容，但不激活那份清单，也不改当前加载结果。
        /// </summary>
        /// <param name="packageName">已经接入的 package 名称。</param>
        /// <param name="packageVersion">要预下载的版本。</param>
        /// <param name="options">超时和下载回调。</param>
        /// <param name="token">取消令牌。</param>
        public static async UniTask PrefetchPackageAsync(
            string packageName,
            string packageVersion,
            YooAssetInitializationOptions options,
            CancellationToken token = default)
#else
        /// <summary>
        /// 预下载指定版本的内容，但不激活那份清单，也不改当前加载结果。
        /// </summary>
        /// <param name="packageName">已经接入的 package 名称。</param>
        /// <param name="packageVersion">要预下载的版本。</param>
        /// <param name="options">超时和下载回调。</param>
        /// <param name="token">取消令牌。</param>
        public static async Task PrefetchPackageAsync(
            string packageName,
            string packageVersion,
            YooAssetInitializationOptions options,
            CancellationToken token = default)
#endif
        {
            EnsureSessionAvailable(options);
            if (string.IsNullOrWhiteSpace(packageVersion))
                throw new ArgumentException("Package version cannot be empty.", nameof(packageVersion));

            ResourcePackage package = RequireRegisteredPackage(packageName);
            sIsInitializing = true;
            try
            {
                ResourceDownloaderOperation downloader = await CreatePrefetchDownloaderAsync(
                    package,
                    packageVersion.Trim(),
                    options,
                    token);
                await DownloadPackageAsync(downloader, options, token);
            }
            finally
            {
                sIsInitializing = false;
            }
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 按当前激活清单下载缺失资源。ManifestOnly 和不完整下载不会把 package 移出 Provider。
        /// </summary>
        /// <param name="packageName">已经接入的 package 名称。</param>
        /// <param name="options">下载并发和回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <param name="tags">要下载的标签；为空时下载当前清单的全部缺失资源。</param>
        public static async UniTask DownloadPackageAsync(
            string packageName,
            YooAssetInitializationOptions options,
            CancellationToken token = default,
            params string[] tags)
#else
        /// <summary>
        /// 按当前激活清单下载缺失资源。ManifestOnly 和不完整下载不会把 package 移出 Provider。
        /// </summary>
        /// <param name="packageName">已经接入的 package 名称。</param>
        /// <param name="options">下载并发和回调。</param>
        /// <param name="token">取消令牌。</param>
        /// <param name="tags">要下载的标签；为空时下载当前清单的全部缺失资源。</param>
        public static async Task DownloadPackageAsync(
            string packageName,
            YooAssetInitializationOptions options,
            CancellationToken token = default,
            params string[] tags)
#endif
        {
            EnsureSessionAvailable(options);
            ResourcePackage package = RequireRegisteredPackage(packageName);
            sIsInitializing = true;
            try
            {
                ResourceDownloaderOperation downloader = CreateActiveDownloader(package, options, tags);
                await DownloadPackageAsync(downloader, options, token);
            }
            finally
            {
                sIsInitializing = false;
            }
        }

        /// <summary>
        /// 将已初始化的 package 登记并接入 ResKit。
        /// 只传入一个 package 时，它既是默认包，也是自动探测的唯一候选。
        /// </summary>
        /// <param name="package">已经完成初始化并加载有效 manifest 的 package。</param>
        public static void InstallProvider(ResourcePackage package)
        {
            InstallProvider(package, false);
        }

        /// <summary>
        /// 将已初始化的 package 登记并接入 ResKit，同时保留此前已登记的其它 package。
        /// </summary>
        /// <param name="package">已经完成初始化并加载有效 manifest 的 package。</param>
        /// <param name="editorSimulateMode">是否使用 Unity Editor 的 EditorSimulateMode。</param>
        public static void InstallProvider(ResourcePackage package, bool editorSimulateMode)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));
            if (sIsInitializing)
                throw new InvalidOperationException("YooAsset initialization is already running.");

            AttachPackage(package, editorSimulateMode);
        }

        /// <summary>
        /// 从探测名单移除 package。不销毁 YooAsset package，也不释放已经加载的资源。
        /// </summary>
        /// <param name="packageName">package 名称。</param>
        /// <returns>登记中存在并已移除时返回 true。</returns>
        public static bool RemovePackage(string packageName)
        {
            if (sIsInitializing)
                throw new InvalidOperationException("Cannot remove a YooAsset package while initialization is running.");
            if (string.IsNullOrWhiteSpace(packageName))
                return false;

            packageName = packageName.Trim();
            bool removed = sPackages.Remove(packageName);
            if (sProvider != null)
                removed = sProvider.RemovePackage(packageName) || removed;
            RefreshDefaultPackage();
            return removed;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 等待 package 销毁完成后移出 YooAsset 和当前 Provider。
        /// 同名重建前必须使用该顺序，不能只调用 <see cref="RemovePackage"/>。
        /// </summary>
        /// <param name="packageName">package 名称。</param>
        /// <param name="token">取消令牌。</param>
        public static async UniTask DestroyPackageAsync(string packageName, CancellationToken token = default)
#else
        /// <summary>
        /// 等待 package 销毁完成后移出 YooAsset 和当前 Provider。
        /// 同名重建前必须使用该顺序，不能只调用 <see cref="RemovePackage"/>。
        /// </summary>
        /// <param name="packageName">package 名称。</param>
        /// <param name="token">取消令牌。</param>
        public static async Task DestroyPackageAsync(string packageName, CancellationToken token = default)
#endif
        {
            if (sIsInitializing)
                throw new InvalidOperationException("Cannot destroy a YooAsset package while initialization is running.");

            packageName = NormalizePackageName(packageName);
            sPackages.TryGet(packageName, out ResourcePackage package);
            sIsInitializing = true;
            try
            {
                await DestroyAndRemovePackageAsync(packageName, package, token);
            }
            finally
            {
                sIsInitializing = false;
            }

            RemovePackage(packageName);
        }

        /// <summary>把已就绪 package 放进当前 Provider；只有第一次安装会替换 ResKit Provider。</summary>
        /// <param name="package">已经完成初始化并加载有效 manifest 的 package。</param>
        /// <param name="editorSimulateMode">是否使用 Unity Editor 的 EditorSimulateMode。</param>
        private static void AttachPackage(ResourcePackage package, bool editorSimulateMode)
        {
            sPackages.Add(package);
            if (DefaultPackage == null || !YooAssetPackageReadiness.IsReady(DefaultPackage))
                SetDefaultPackage(package);

            YooAssetResourceProvider provider = GetInstalledProvider();
            if (provider == null)
            {
                sProvider = new YooAssetResourceProvider(sPackages.Copy(), editorSimulateMode);
                ResKit.SetProvider(sProvider);
            }
            else
            {
                sProvider = provider;
                provider.AddPackage(package);
            }

            IsInitialized = true;
        }

        /// <summary>读取当前 ResKit Provider；外部替换成其它 Provider 后不再向旧实例追加。</summary>
        private static YooAssetResourceProvider GetInstalledProvider()
        {
            if (ResKit.GetProvider() is YooAssetResourceProvider provider)
                return provider;

            sProvider = null;
            return null;
        }

        /// <summary>在移除默认包后，把探测起点改为登记中的下一项。</summary>
        private static void RefreshDefaultPackage()
        {
            ResourcePackage[] packages = sPackages.Copy();
            if (packages.Length == 0)
            {
                DefaultPackage = null;
                DefaultPackageName = null;
                IsInitialized = false;
                return;
            }

            if (DefaultPackage == null
                || !ContainsPackage(packages, DefaultPackage.PackageName))
            {
                SetDefaultPackage(packages[0]);
            }

            IsInitialized = true;
        }

        /// <summary>判断登记快照是否仍包含指定 package。</summary>
        private static bool ContainsPackage(ResourcePackage[] packages, string packageName)
        {
            for (int index = 0; index < packages.Length; index++)
            {
                if (string.Equals(packages[index].PackageName, packageName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>拒绝空配置和重入，供全部包会话共用。</summary>
        private static void EnsureSessionAvailable(YooAssetInitializationOptions options)
        {
            if (sIsInitializing)
                throw new InvalidOperationException("YooAsset initialization is already running.");
            if (options == null)
                throw new ArgumentNullException(nameof(options));
        }

        /// <summary>整理调用方传入的 package 名称。</summary>
        private static string NormalizePackageName(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
                throw new ArgumentException("Package name cannot be empty.", nameof(packageName));

            return packageName.Trim();
        }

        /// <summary>获取已经接入的 package，未接入时给出稳定错误。</summary>
        private static ResourcePackage RequireRegisteredPackage(string packageName)
        {
            packageName = NormalizePackageName(packageName);
            if (sPackages.TryGet(packageName, out ResourcePackage package))
                return package;

            throw new InvalidOperationException(
                "YooAsset package '" + packageName + "' is not registered with the ResKit provider.");
        }
    }
}
#endif
