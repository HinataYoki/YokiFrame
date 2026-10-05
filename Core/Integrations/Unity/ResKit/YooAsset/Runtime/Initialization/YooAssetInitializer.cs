#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
using UnityEngine;
using YokiFrame;
using YooAsset;

namespace YokiFrame.Unity
{
#if YOKIFRAME_YOOASSET_3
    /// <summary>YooAsset V3 package 初始化回调。</summary>
    public delegate InitializePackageOperation YooAssetPackageInitializationHandler(
        ResourcePackage package,
        YooAssetInitializationOptions options);
#else
    /// <summary>YooAsset V2 package 初始化回调。</summary>
    public delegate InitializationOperation YooAssetPackageInitializationHandler(
        ResourcePackage package,
        YooAssetInitializationOptions options);
#endif

    /// <summary>
    /// YooAsset 包会话门面。
    /// 它按包准备清单和下载，并把已就绪 package 放进同一个 ResKit Provider；后续追加、更新和移除不再替换 Provider。
    /// package 销毁仍由项目显式调用，门面不会在普通移除时销毁它。
    /// </summary>
    public static partial class YooAssetInitializer
    {
        private static readonly YooAssetPackageRegistry sPackages = new();
        private static YooAssetResourceProvider sProvider;
        private static YooAssetDownloadProgressScope sDownloadProgress;
        private static bool sIsInitializing;

        /// <summary>获取是否已经有 package 接入当前 ResKit Provider。</summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>获取当前是否有一项初始化任务正在执行。</summary>
        public static bool IsInitializing => sIsInitializing;

        /// <summary>获取登记顺序中的首个 package，也是自动探测的起点。</summary>
        public static ResourcePackage DefaultPackage { get; private set; }

        /// <summary>获取默认 package 名称。</summary>
        public static string DefaultPackageName { get; private set; }

        /// <summary>按登记顺序获取本次初始化的 package；第一项是自动探测的起点。</summary>
        public static IReadOnlyList<ResourcePackage> Packages => sPackages.Copy();

        /// <summary>为 CustomPlayMode 提供 package 初始化回调。</summary>
        public static YooAssetPackageInitializationHandler CustomInitializationHandler { get; set; }

        /// <summary>为 HostPlayMode 提供自定义 package 初始化回调。</summary>
        public static YooAssetPackageInitializationHandler HostInitializationHandler { get; set; }

        /// <summary>为 WebPlayMode 提供自定义 package 初始化回调。</summary>
        public static YooAssetPackageInitializationHandler WebInitializationHandler { get; set; }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>使用默认参数初始化 YooAsset 并安装 ResKit Provider。</summary>
        /// <param name="token">取消令牌。</param>
        public static UniTask InitializeAsync(CancellationToken token = default)
#else
        /// <summary>使用默认参数初始化 YooAsset 并安装 ResKit Provider。</summary>
        /// <param name="token">取消令牌。</param>
        public static Task InitializeAsync(CancellationToken token = default)
#endif
        {
            return InitializeAsync(new YooAssetInitializationOptions(), token);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 按登记顺序准备配置中的全部 package，并接入同一个 ResKit Provider。
        /// 已经接入的 Provider 不会被替换；重复调用会按名单继续准备并追加尚未接入的 package。
        /// </summary>
        /// <param name="options">初始化参数。</param>
        /// <param name="token">取消令牌。</param>
        public static async UniTask InitializeAsync(
            YooAssetInitializationOptions options,
            CancellationToken token = default)
#else
        /// <summary>
        /// 按登记顺序准备配置中的全部 package，并接入同一个 ResKit Provider。
        /// 已经接入的 Provider 不会被替换；重复调用会按名单继续准备并追加尚未接入的 package。
        /// </summary>
        /// <param name="options">初始化参数。</param>
        /// <param name="token">取消令牌。</param>
        public static async Task InitializeAsync(
            YooAssetInitializationOptions options,
            CancellationToken token = default)
#endif
        {
            EnsureSessionAvailable(options);
            sIsInitializing = true;
            try
            {
                EnsureYooAssetsInitialized();
                ValidateStrategy(options);
                List<string> packageNames = ResolvePackageNames(options);
                sDownloadProgress = CreateDownloadProgress(packageNames.Count, options);
                for (int index = 0; index < packageNames.Count; index++)
                {
                    token.ThrowIfCancellationRequested();
                    sDownloadProgress?.BeginPackage(packageNames[index], index);
                    await InitializeRegisteredPackageAsync(packageNames[index], options, token);
                    sDownloadProgress?.CompletePackage(packageNames[index], index);
                }
            }
            finally
            {
                sDownloadProgress = null;
                sIsInitializing = false;
            }
        }


        /// <summary>按名称获取已登记 package。</summary>
        /// <param name="packageName">package 名称。</param>
        /// <returns>已登记实例；不存在时返回 null。</returns>
        public static ResourcePackage GetPackage(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
                return null;

            return sPackages.TryGet(packageName, out ResourcePackage package)
                ? package
                : null;
        }

        /// <summary>尝试按名称获取已登记 package。</summary>
        /// <param name="packageName">package 名称。</param>
        /// <param name="package">找到的 package。</param>
        /// <returns>找到时返回 true。</returns>
        public static bool TryGetPackage(string packageName, out ResourcePackage package)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                package = null;
                return false;
            }

            return sPackages.TryGet(packageName, out package);
        }

        /// <summary>
        /// 清除初始化器登记状态，但不销毁 YooAsset package，也不替换当前 ResKit Provider。
        /// 项目在自行销毁 package 或测试完成后可调用它，为下一轮初始化释放门面状态。
        /// </summary>
        public static void ResetRegistration()
        {
            if (sIsInitializing)
                throw new InvalidOperationException("Cannot reset YooAsset registration while initialization is running.");

            ResetSessionState();
            CustomInitializationHandler = null;
            HostInitializationHandler = null;
            WebInitializationHandler = null;
        }

        /// <summary>
        /// 在子系统登记阶段加入统一会话重置，真正清理要等全部登记完成之后。
        /// </summary>
        /// <remarks>
        /// 必要性：关闭 Domain Reload（Enter Play Mode Options）后静态字段会跨 Play 会话存活。
        /// 若不清除登记和 Provider 引用，下一次进入 Play Mode 会把资源请求送到上一会话的 package。
        /// <para>
        /// 刻意只重置会话状态而**不清除三个初始化回调**：它们是项目配置。回调由项目在每次会话自行注册。
        /// </para>
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistrationOnSubsystemRegistration()
        {
            YokiFrameSession.Register(
                YokiFrameSession.RELEASE_HOSTS_ORDER,
                "yooasset",
                ResetForSession);
        }

        /// <summary>
        /// 清除上一会话的 YooAsset 门面状态。初始化进行中时不打断当前流程。
        /// </summary>
        private static void ResetForSession()
        {
            if (sIsInitializing)
            {
                return;
            }

            ResetSessionState();
        }

        /// <summary>
        /// 清除单个 Player 会话内积累的登记状态（不含项目配置的初始化回调）。
        /// 重新初始化前不能继续暴露上一会话的 package 或 Provider。
        /// </summary>
        private static void ResetSessionState()
        {
            IsInitialized = false;
            DefaultPackage = null;
            DefaultPackageName = null;
            sProvider = null;
            sPackages.Clear();
        }

        /// <summary>确保 YooAsset 全局驱动已创建。</summary>
        private static void EnsureYooAssetsInitialized()
        {
#if YOKIFRAME_YOOASSET_3
            if (!YooAssets.IsInitialized)
                YooAssets.Initialize();
#else
            if (!YooAssets.Initialized)
                YooAssets.Initialize();
#endif
        }

        /// <summary>规范化并去重配置中的 package 名称。</summary>
        private static List<string> ResolvePackageNames(YooAssetInitializationOptions options)
        {
            List<string> packageNames = new();
            if (options.PackageNames != null)
            {
                for (int index = 0; index < options.PackageNames.Count; index++)
                {
                    string packageName = options.PackageNames[index];
                    if (string.IsNullOrWhiteSpace(packageName))
                        continue;

                    packageName = packageName.Trim();
                    if (!packageNames.Contains(packageName))
                        packageNames.Add(packageName);
                }
            }

            if (packageNames.Count == 0)
                packageNames.Add(YooAssetInitializationOptions.DEFAULT_PACKAGE_NAME);
            return packageNames;
        }

        /// <summary>获取现有 package 或创建一个新的 package。</summary>
        private static ResourcePackage GetOrCreatePackage(string packageName)
        {
#if YOKIFRAME_YOOASSET_3
            if (YooAssets.TryGetPackage(packageName, out ResourcePackage package))
                return package;
            return YooAssets.CreatePackage(packageName);
#else
            ResourcePackage package = YooAssets.TryGetPackage(packageName);
            return package ?? YooAssets.CreatePackage(packageName);
#endif
        }

        /// <summary>登记首个 package 为默认包，并同步 YooAsset V2 全局默认包。</summary>
        private static void SetDefaultPackage(ResourcePackage package)
        {
            DefaultPackage = package;
            DefaultPackageName = package.PackageName;
#if !YOKIFRAME_YOOASSET_3
            YooAssets.SetDefaultPackage(package);
#endif
        }
    }
}
#endif
