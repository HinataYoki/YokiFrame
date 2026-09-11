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
    /// YooAsset 一键初始化门面。
    /// 它负责创建并初始化 package，初始化成功后把默认 package 接入 ResKit；package 销毁仍由项目生命周期负责。
    /// </summary>
    public static partial class YooAssetInitializer
    {
        private static readonly Dictionary<string, ResourcePackage> sPackages = new(StringComparer.Ordinal);
        private static bool sIsInitializing;

        /// <summary>获取 YooAsset 初始化是否已成功完成。</summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>获取当前是否有一项初始化任务正在执行。</summary>
        public static bool IsInitializing => sIsInitializing;

        /// <summary>获取初始化后选为 ResKit 默认 package 的实例。</summary>
        public static ResourcePackage DefaultPackage { get; private set; }

        /// <summary>获取默认 package 名称。</summary>
        public static string DefaultPackageName { get; private set; }

        /// <summary>获取本次初始化登记的 package 集合。</summary>
        public static IReadOnlyDictionary<string, ResourcePackage> Packages => sPackages;

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
        /// <summary>按指定参数初始化全部 package，并把首个有效 package 接入 ResKit。</summary>
        /// <param name="options">初始化参数。</param>
        /// <param name="token">取消令牌。</param>
        public static async UniTask InitializeAsync(
            YooAssetInitializationOptions options,
            CancellationToken token = default)
#else
        /// <summary>按指定参数初始化全部 package，并把首个有效 package 接入 ResKit。</summary>
        /// <param name="options">初始化参数。</param>
        /// <param name="token">取消令牌。</param>
        public static async Task InitializeAsync(
            YooAssetInitializationOptions options,
            CancellationToken token = default)
#endif
        {
            if (IsInitialized)
                return;
            if (sIsInitializing)
                throw new InvalidOperationException("YooAsset initialization is already running.");
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            sIsInitializing = true;
            try
            {
                EnsureYooAssetsInitialized();
                await InitializePackagesAsync(options, token);
                if (DefaultPackage == null)
                    throw new InvalidOperationException("No valid YooAsset package was initialized.");

                InstallProvider(DefaultPackage, options.PlayMode == EPlayMode.EditorSimulateMode);
                IsInitialized = true;
            }
            finally
            {
                sIsInitializing = false;
            }
        }

        /// <summary>
        /// 将已初始化的 package 直接接入 ResKit，适合项目自行管理非 EditorSimulate 的 YooAsset 初始化流程。
        /// </summary>
        /// <param name="package">已经完成初始化并加载有效 manifest 的 package。</param>
        public static void InstallProvider(ResourcePackage package)
        {
            InstallProvider(package, false);
        }

        /// <summary>
        /// 将已初始化的 package 直接接入 ResKit，并传递当前是否为 EditorSimulateMode。
        /// </summary>
        /// <param name="package">已经完成初始化并加载有效 manifest 的 package。</param>
        /// <param name="editorSimulateMode">是否使用 Unity Editor 的 EditorSimulateMode。</param>
        public static void InstallProvider(ResourcePackage package, bool editorSimulateMode)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));

            DefaultPackage = package;
            DefaultPackageName = package.PackageName;
            sPackages[package.PackageName] = package;
            ResKit.SetProvider(new YooAssetResourceProvider(package, editorSimulateMode));
        }

        /// <summary>按名称获取已登记 package。</summary>
        /// <param name="packageName">package 名称。</param>
        /// <returns>已登记实例；不存在时返回 null。</returns>
        public static ResourcePackage GetPackage(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
                return null;

            return sPackages.TryGetValue(packageName, out ResourcePackage package)
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

            return sPackages.TryGetValue(packageName, out package);
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
        /// 在进入新 Player 子系统时释放上一会话的登记状态。
        /// </summary>
        /// <remarks>
        /// 必要性：关闭 Domain Reload（Enter Play Mode Options）后静态字段会跨 Play 会话存活，
        /// 而 <see cref="InitializeAsync(YooAssetInitializationOptions, CancellationToken)"/> 开头的
        /// <c>if (IsInitialized) return;</c> 会因此静默提前返回 —— 结果是第二次进入 Play Mode 时
        /// 既不初始化 package、也不执行 <see cref="InstallProvider(ResourcePackage, bool)"/>，
        /// ResKit 静默退回 Unity Resources 加载资源（不抛异常、不打日志）。
        /// 本钩子让每个会话都从干净状态重新初始化。
        /// <para>
        /// 刻意只重置会话状态而**不清除三个初始化回调**：它们是项目配置，而 Unity 对同一
        /// <c>SubsystemRegistration</c> 阶段多个钩子的调用顺序不作保证；若项目的注册钩子先于本钩子执行，
        /// 清除会永久丢失项目配置。回调由项目在每次会话自行注册，无需框架代为清理。
        /// </para>
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistrationOnSubsystemRegistration()
        {
            if (sIsInitializing)
            {
                return;
            }

            ResetSessionState();
        }

        /// <summary>
        /// 清除单个 Player 会话内积累的登记状态（不含项目配置的初始化回调）。
        /// </summary>
        /// <remarks>
        /// <see cref="InitializePackagesAsync(YooAssetInitializationOptions, CancellationToken)"/> 开头本就会重清
        /// package 登记，因此真正会阻断重新初始化的只有 <see cref="IsInitialized"/>；
        /// 这里一并清理 <see cref="DefaultPackage"/>/<see cref="DefaultPackageName"/>/<see cref="sPackages"/>，
        /// 使门面在重新初始化前不暴露上一会话的 package 实例。
        /// </remarks>
        private static void ResetSessionState()
        {
            IsInitialized = false;
            DefaultPackage = null;
            DefaultPackageName = null;
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

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>顺序初始化有效 package，确保首个 package 成为 ResKit 默认 package。</summary>
        private static async UniTask InitializePackagesAsync(
            YooAssetInitializationOptions options,
            CancellationToken token)
#else
        /// <summary>顺序初始化有效 package，确保首个 package 成为 ResKit 默认 package。</summary>
        private static async Task InitializePackagesAsync(
            YooAssetInitializationOptions options,
            CancellationToken token)
#endif
        {
            sPackages.Clear();
            DefaultPackage = null;
            DefaultPackageName = null;

            List<string> packageNames = ResolvePackageNames(options);
            for (int index = 0; index < packageNames.Count; index++)
            {
                token.ThrowIfCancellationRequested();
                ResourcePackage package = GetOrCreatePackage(packageNames[index]);
                if (DefaultPackage == null)
                    SetDefaultPackage(package);

                await InitializePackageAsync(package, options, token);
                sPackages[package.PackageName] = package;
            }
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
