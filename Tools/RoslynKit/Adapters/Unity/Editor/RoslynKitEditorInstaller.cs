#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#if UNITY_6000_5_OR_NEWER
using UnityEngine.Assemblies;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 把 RoslynKit Provider 安装到共享 Tool catalog，使 Engine 命令面与 state snapshot 在 Unity 宿主生效。
    /// </summary>
    public static class RoslynKitEditorInstaller
    {
        /// <summary>Engine 执行开关所在的 Editor 设置文件（相对 Unity 工程根）。</summary>
        private const string EDITOR_SETTINGS_RELATIVE_PATH =
            "ProjectSettings/Packages/com.hinatayoki.yokiframe/editor-settings.json";

        private static RoslynKitProvider sProvider;
        private static LiveCodeManager sLiveCode;
        private static LiveTuningBinder sTuning;
        private static Func<bool> sLivePermission;
        private static double sNextPermissionCheck;

        /// <summary>Unity Editor 程序集加载后幂等注册 RoslynKit Provider。</summary>
        [InitializeOnLoadMethod]
        private static void InstallOnEditorLoad()
        {
            EnsureInstalled();
        }

        /// <summary>幂等注册 RoslynKit Provider；重复调用不会创建第二个 Provider。</summary>
        public static void EnsureInstalled()
        {
#if UNITY_6000_5_OR_NEWER
            // Unity 6.5 不再保证 AppDomain 只返回仍存活的程序集。
            LoadedAssemblies.Use(CurrentAssemblies.GetLoadedAssemblies);
#endif
            YokiFrameToolKitInteractionCatalog.Register(GetProvider());
        }

        /// <summary>获取当前会话的 RoslynKit Provider。</summary>
        /// <returns>已装配 Gate、引擎 Provider 与开关读取端口的 Kit Provider。</returns>
        private static RoslynKitProvider GetProvider()
        {
            if (sProvider == null)
            {
                UnityLiveSnapshotIdentities.Install();
                var settingsSource = new RoslynJsonSettingsSource(ResolveSettingsPath());
                var gate = RoslynGate.CreateDefault(settingsSource);
                var engineProvider = new UnityEngineOperationProvider();
                string projectRoot = ResolveProjectRoot();
                engineProvider.ProjectRoot = projectRoot;
                RoslynRunScheduler scheduler = InstallRunScheduler(engineProvider, settingsSource, projectRoot);
                InstallLiveCode(engineProvider, settingsSource, projectRoot, scheduler);
                sProvider = new RoslynKitProvider(gate, engineProvider, settingsSource);
                SubscribeProviderLifetime();
            }

            return sProvider;
        }

        /// <summary>
        /// 创建只接受内存脚本的运行调度器，写到引擎 Provider 后立即挂上编辑器驱动。
        /// 已落盘记录不能从这里提供可执行代码。
        /// </summary>
        /// <param name="engineProvider">接收调度器的引擎 Provider。</param>
        /// <param name="settingsSource">执行开关；调度器稍后读取 BlocksExecution。</param>
        /// <param name="projectRoot">运行记录所在的工程根。</param>
        /// <returns>已启动驱动的调度器。</returns>
        private static RoslynRunScheduler InstallRunScheduler(
            UnityEngineOperationProvider engineProvider,
            RoslynJsonSettingsSource settingsSource,
            string projectRoot)
        {
            // Only in-memory work can be submitted; persisted records never supply executable code.
            var host = new RoslynRunHost(engineProvider.ReadDomainState);
            var store = new RoslynRunStore(projectRoot);
            var scheduler = new RoslynRunScheduler(
                store,
                host,
                () => settingsSource.Read().BlocksExecution,
                ownerHostId: "unity-editor");
            engineProvider.RunScheduler = scheduler;
            UnityEngineRunSchedulerDriver.EnsureStarted(scheduler);
            return scheduler;
        }

        /// <summary>
        /// 按原顺序装配许可、编译器、LiveCode、脚本操作和调参，再追加到引擎 Provider。
        /// 回调捕获的是静态字段，允许在 LiveCode 赋值前创建宿主。
        /// </summary>
        /// <param name="engineProvider">接收附加操作的引擎 Provider。</param>
        /// <param name="settingsSource">执行开关，与受信任 C# 开关同时放行。</param>
        /// <param name="projectRoot">编译器、补丁和调参使用的工程根。</param>
        /// <param name="scheduler">脚本操作提交到的调度器。</param>
        private static void InstallLiveCode(
            UnityEngineOperationProvider engineProvider,
            RoslynJsonSettingsSource settingsSource,
            string projectRoot,
            RoslynRunScheduler scheduler)
        {
            var trustedSource = new RoslynJsonSettingsSource(
                ResolveSettingsPath(), "scripts.trustedCSharp", "trustedCSharp");
            sLivePermission = () => !settingsSource.Read().BlocksExecution && !trustedSource.Read().BlocksExecution;
            var compiler = new RoslynCompilerLoader(projectRoot,
                new[] { ResolvePackageDirectory(RoslynCompilerLoader.PACKAGE_RELATIVE_PATH) },
                null, null, LoadCompilerAssembly);
            var budget = new RoslynLoadBudget();
            var liveHost = new UnityLiveCodeHost(projectRoot,
                (id, method, arguments) => sLiveCode.Invoke(id, method, arguments, () => { }));
            sLiveCode = new LiveCodeManager(compiler, budget,
                new MethodPatchBackend(ResolvePatchBundle(projectRoot), LoadCompilerAssembly),
                liveHost,
                engineProvider.ReadDomainState, sLivePermission, projectRoot);
            var scripts = new RoslynScriptOperations(scheduler,
                compiler, engineProvider.ReadDomainState, sLivePermission,
                allowed => new AutomationContext(
                    () => UnityAutomationFrameClock.Started,
                    allowed, message => Debug.Log("[YokiFrame.Script] " + message),
                    (mode, path, autoNumber, token) =>
                        UnityAutomationCapture.Capture(projectRoot, mode, path, autoNumber, token),
                    sLiveCode, () => UnityAutomationFrameClock.Completed), budget);
            sTuning = new LiveTuningBinder(projectRoot, sLiveCode, sLivePermission, engineProvider.ReadDomainState);
            var operations = new System.Collections.Generic.List<IRoslynOperation>(scripts.CreateOperations())
            {
                new UnityLiveExportOperation(liveHost, sLiveCode, "live_export_status", sLivePermission),
                new UnityLiveExportOperation(liveHost, sLiveCode, "live_export_commit", sLivePermission),
                new LiveCodeOperation(sLiveCode, "live_status"),
                new LiveCodeOperation(sLiveCode, "live_remove"),
                new LiveCodeOperation(sLiveCode, "live_snapshot", sLivePermission),
                new LiveCodeOperation(sLiveCode, "live_set_fields", sLivePermission),
                new LiveTuningOperation(sTuning, "live_tuning_bind", sLivePermission),
                new LiveTuningOperation(sTuning, "live_tuning_refresh", sLivePermission),
                new LiveTuningOperation(sTuning, "live_tuning_status"),
                new LiveTuningOperation(sTuning, "live_tuning_unbind")
            };
            engineProvider.AddOperations(operations.ToArray());
        }

        /// <summary>Provider 创建后按固定顺序订阅播放、更新、重载和退出。重复安装不会再次订阅。</summary>
        private static void SubscribeProviderLifetime()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += CheckLivePermission;
            AssemblyReloadEvents.beforeAssemblyReload += ClearLiveCode;
            EditorApplication.quitting += ClearLiveCode;
        }

        /// <summary>播放模式变化时清掉 LiveCode，并在 Provider 仍在时作废对象句柄。</summary>
        /// <param name="state">播放模式变化；不区分进入或退出。</param>
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            ClearLiveCode();
            if (sProvider != null) sProvider.InvalidateObjectHandles();
        }

        /// <summary>
        /// 约每 0.1 秒复查许可。许可丢失时清理 LiveCode；调参绑定存在时用编辑器时间 Tick。
        /// </summary>
        private static void CheckLivePermission()
        {
            if (EditorApplication.timeSinceStartup < sNextPermissionCheck) return;
            sNextPermissionCheck = EditorApplication.timeSinceStartup + 0.1d;
            if (sLivePermission != null && !sLivePermission()) ClearLiveCode();
            if (sTuning != null) sTuning.Tick(EditorApplication.timeSinceStartup);
        }

        /// <summary>先停调参再清 LiveCode。清理由重载、退出、许可丢失和播放模式变化共用。</summary>
        private static void ClearLiveCode()
        {
            if (sTuning != null) sTuning.StopAll("Live session cleared; rebind explicitly.");
            if (sLiveCode != null) sLiveCode.Clear(exception => Debug.LogException(exception));
        }

        /// <summary>
        /// 解析 HarmonyX 补丁负载目录：包内 <c>Dependencies~</c> 优先，
        /// 独立分发时回退到项目 <c>.yokiframe/automation/patches/harmonyx-2.16.1</c>。
        /// </summary>
        /// <param name="projectRoot">Unity 工程根。</param>
        /// <returns>补丁负载目录绝对路径。</returns>
        private static string ResolvePatchBundle(string projectRoot)
        {
            string packaged = ResolvePackageDirectory(
                "Tools/RoslynKit/Adapters/Unity/Editor/Dependencies~/harmonyx-2.16.1");
            if (File.Exists(Path.Combine(packaged, "0Harmony.dll"))) return packaged;
            return Path.GetFullPath(Path.Combine(projectRoot,
                ".yokiframe/automation/patches/harmonyx-2.16.1".Replace('/', Path.DirectorySeparatorChar)));
        }

        /// <summary>
        /// 加载 Roslyn bundle。Unity 6.5 起默认 <c>Assembly.LoadFrom</c> 会进入可回收上下文，
        /// 依赖解析随后返回 0x80131515，因此必须走当前不可回收的程序集上下文。
        /// </summary>
        /// <param name="path">编译器或依赖程序集绝对路径。</param>
        /// <returns>已加载程序集。</returns>
        private static Assembly LoadCompilerAssembly(string path)
        {
#if UNITY_6000_5_OR_NEWER
            return CurrentAssemblies.LoadFromPath(path);
#else
            return Assembly.LoadFrom(path);
#endif
        }

        /// <summary>
        /// 解析包内目录的绝对路径；用于定位随包分发的 <c>Dependencies~</c> 负载。
        /// 找不到包根时回退到工程根下的同相对路径。
        /// </summary>
        /// <param name="packageRelativePath">相对包根的路径，使用正斜杠分隔。</param>
        /// <returns>目录绝对路径。</returns>
        private static string ResolvePackageDirectory(string packageRelativePath)
        {
            string packageRoot = ResolvePackageRoot();
            return Path.GetFullPath(Path.Combine(packageRoot,
                packageRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        /// <summary>
        /// 通过本脚本自身的 GUID 反查包根；不硬编码 <c>Assets/YokiFrame</c>。
        /// 包被移动到其它目录或改为只读包缓存时仍能定位。
        /// </summary>
        /// <returns>包根绝对路径；解析失败时回退到工程根。</returns>
        private static string ResolvePackageRoot()
        {
            string projectRoot = ResolveProjectRoot();
            const string suffix = "/Tools/RoslynKit/Adapters/Unity/Editor/RoslynKitEditorInstaller.cs";
            foreach (string guid in AssetDatabase.FindAssets("RoslynKitEditorInstaller t:MonoScript"))
            {
                string script = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (script.EndsWith(suffix, StringComparison.Ordinal))
                {
                    string packageRelative = script.Substring(0, script.Length - suffix.Length);
                    return Path.GetFullPath(Path.Combine(projectRoot, packageRelative));
                }
            }

            return projectRoot;
        }

        /// <summary>
        /// 解析开关文件绝对路径；以工程根为基准，避免依赖进程当前目录。
        /// </summary>
        /// <returns>设置文件绝对路径。</returns>
        private static string ResolveSettingsPath()
        {
            return Path.Combine(ResolveProjectRoot(), EDITOR_SETTINGS_RELATIVE_PATH.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// 解析 Unity 工程根；运行记录与设置文件都以它为基准。
        /// </summary>
        /// <returns>工程根绝对路径。</returns>
        private static string ResolveProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
#endif
