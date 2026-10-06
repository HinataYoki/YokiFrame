#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 把 Engine Kit Provider 安装到共享 Tool catalog，使 Engine 命令面与 state snapshot 在 Unity 宿主生效。
    /// </summary>
    public static class EngineKitEditorInstaller
    {
        /// <summary>Engine 执行开关所在的 Editor 设置文件（相对 Unity 工程根）。</summary>
        private const string EDITOR_SETTINGS_RELATIVE_PATH =
            "ProjectSettings/Packages/com.hinatayoki.yokiframe/editor-settings.json";

        private static YokiFrameEngineKitProvider sProvider;
        private static YokiFrameLiveCodeManager sLiveCode;
        private static YokiFrameLiveTuningBinder sTuning;
        private static Func<bool> sLivePermission;
        private static double sNextPermissionCheck;

        /// <summary>Unity Editor 程序集加载后幂等注册 Engine Kit Provider。</summary>
        [InitializeOnLoadMethod]
        private static void InstallOnEditorLoad()
        {
            EnsureInstalled();
        }

        /// <summary>幂等注册 Engine Kit Provider；重复调用不会创建第二个 Provider。</summary>
        public static void EnsureInstalled()
        {
            YokiFrameToolKitInteractionCatalog.Register(GetProvider());
        }

        /// <summary>获取当前会话的 Engine Kit Provider。</summary>
        /// <returns>已装配 Gate、引擎 Provider 与开关读取端口的 Kit Provider。</returns>
        private static YokiFrameEngineKitProvider GetProvider()
        {
            if (sProvider == null)
            {
                UnityLiveSnapshotIdentities.Install();
                var settingsSource = new YokiFrameEngineJsonSettingsSource(ResolveSettingsPath());
                var gate = YokiFrameEngineGate.CreateDefault(settingsSource);
                var engineProvider = new UnityEngineOperationProvider();
                string projectRoot = ResolveProjectRoot();
                engineProvider.ProjectRoot = projectRoot;

                // Only in-memory work can be submitted; persisted records never supply executable code.
                var host = new YokiFrameEngineRunHost(engineProvider.ReadDomainState);
                var store = new YokiFrameEngineRunStore(projectRoot);
                var scheduler = new YokiFrameEngineRunScheduler(
                    store,
                    host,
                    () => settingsSource.Read().BlocksExecution,
                    ownerHostId: "unity-editor");
                engineProvider.RunScheduler = scheduler;
                UnityEngineRunSchedulerDriver.EnsureStarted(scheduler);

                var trustedSource = new YokiFrameEngineJsonSettingsSource(
                    ResolveSettingsPath(), "scripts.trustedCSharp", "trustedCSharp");
                sLivePermission = () => !settingsSource.Read().BlocksExecution && !trustedSource.Read().BlocksExecution;
                var compiler = new YokiFrameRoslynCompilerLoader(projectRoot,
                    new[] { ResolvePackageDirectory(YokiFrameRoslynCompilerLoader.PACKAGE_RELATIVE_PATH) });
                var budget = new YokiFrameRoslynLoadBudget();
                var liveHost = new UnityLiveCodeHost(projectRoot,
                    (id, method, arguments) => sLiveCode.Invoke(id, method, arguments, () => { }));
                sLiveCode = new YokiFrameLiveCodeManager(compiler, budget,
                    new YokiFrameMethodPatchBackend(ResolvePatchBundle(projectRoot)),
                    liveHost,
                    engineProvider.ReadDomainState, sLivePermission, projectRoot);
                var scripts = new YokiFrameEngineScriptOperations(scheduler,
                    compiler, engineProvider.ReadDomainState, sLivePermission,
                    allowed => new YokiFrameAutomationContext(
                        () => UnityAutomationFrameClock.Started,
                        allowed, message => Debug.Log("[YokiFrame.Script] " + message),
                        (mode, path, autoNumber, token) =>
                            UnityAutomationCapture.Capture(projectRoot, mode, path, autoNumber, token),
                        sLiveCode, () => UnityAutomationFrameClock.Completed), budget);
                sTuning = new YokiFrameLiveTuningBinder(projectRoot, sLiveCode, sLivePermission, engineProvider.ReadDomainState);
                var operations = new System.Collections.Generic.List<IYokiFrameEngineOperation>(scripts.CreateOperations())
                {
                    new UnityLiveExportOperation(liveHost, sLiveCode, "live_export_status", sLivePermission),
                    new UnityLiveExportOperation(liveHost, sLiveCode, "live_export_commit", sLivePermission),
                    new YokiFrameLiveCodeOperation(sLiveCode, "live_status"),
                    new YokiFrameLiveCodeOperation(sLiveCode, "live_remove"),
                    new YokiFrameLiveCodeOperation(sLiveCode, "live_snapshot", sLivePermission),
                    new YokiFrameLiveCodeOperation(sLiveCode, "live_set_fields", sLivePermission),
                    new YokiFrameLiveTuningOperation(sTuning, "live_tuning_bind", sLivePermission),
                    new YokiFrameLiveTuningOperation(sTuning, "live_tuning_refresh", sLivePermission),
                    new YokiFrameLiveTuningOperation(sTuning, "live_tuning_status"),
                    new YokiFrameLiveTuningOperation(sTuning, "live_tuning_unbind")
                };
                engineProvider.AddOperations(operations.ToArray());

                sProvider = new YokiFrameEngineKitProvider(gate, engineProvider, settingsSource);
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                EditorApplication.update += CheckLivePermission;
                AssemblyReloadEvents.beforeAssemblyReload += ClearLiveCode;
                EditorApplication.quitting += ClearLiveCode;
            }

            return sProvider;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            ClearLiveCode();
            if (sProvider != null) sProvider.InvalidateObjectHandles();
        }

        private static void CheckLivePermission()
        {
            if (EditorApplication.timeSinceStartup < sNextPermissionCheck) return;
            sNextPermissionCheck = EditorApplication.timeSinceStartup + 0.1d;
            if (sLivePermission != null && !sLivePermission()) ClearLiveCode();
            if (sTuning != null) sTuning.Tick(EditorApplication.timeSinceStartup);
        }

        private static void ClearLiveCode()
        {
            if (sTuning != null) sTuning.StopAll("Live session cleared; rebind explicitly.");
            if (sLiveCode != null) sLiveCode.Clear(exception => Debug.LogException(exception));
        }

        /// <summary>
        /// 解析 HarmonyX 补丁负载目录：包内 <c>Dependencies~</c> 优先，
        /// 独立分发的项目本地回退路径次之（见 Engine-LiveCode-Contract §3）。
        /// </summary>
        /// <param name="projectRoot">Unity 工程根。</param>
        /// <returns>补丁负载目录绝对路径。</returns>
        private static string ResolvePatchBundle(string projectRoot)
        {
            string packaged = ResolvePackageDirectory(
                "Core/Adapters/Unity/Editor/EngineKit/Dependencies~/harmonyx-2.16.1");
            if (File.Exists(Path.Combine(packaged, "0Harmony.dll"))) return packaged;
            return Path.GetFullPath(Path.Combine(projectRoot,
                ".yokiframe/automation/patches/harmonyx-2.16.1".Replace('/', Path.DirectorySeparatorChar)));
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
            const string suffix = "/Core/Adapters/Unity/Editor/EngineKit/EngineKitEditorInstaller.cs";
            foreach (string guid in AssetDatabase.FindAssets("EngineKitEditorInstaller t:MonoScript"))
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
