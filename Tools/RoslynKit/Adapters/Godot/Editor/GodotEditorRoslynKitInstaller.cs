#if GODOT && TOOLS
using System;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// 把 RoslynKit 接到 Godot Editor 宿主：注册 Kit Provider，并在插件 Process 中推进入口调度器。
    /// </summary>
    /// <remarks>
    /// 只注册 catalog（Tool Provider 目录）：编辑器不会因此获得 Core Kit 的命令面，
    /// 与 Godot Editor 宿主"最小只读命令面"的既有姿态一致。宿主的命令策略按 catalog revision 自动重建。
    /// </remarks>
    public static class GodotEditorRoslynKitInstaller
    {
        static GodotEditorRoslynKitInstaller()
        {
            GodotRoslynKitEditorHooks.EnsureInstalled = EnsureInstalled;
            GodotRoslynKitEditorHooks.Tick = Tick;
            GodotRoslynKitEditorHooks.Shutdown = Shutdown;
        }

        private static RoslynKitProvider sProvider;
        private static RoslynRunScheduler sScheduler;
        private static bool sReconciled;
        private static GodotLiveAutomation sAutomation;

        /// <summary>幂等安装 RoslynKit。</summary>
        /// <param name="sessionIdAccessor">会话标识来源。</param>
        /// <param name="generationAccessor">域代次来源。</param>
        /// <param name="projectRoot">Godot 项目根绝对路径。</param>
        public static void EnsureInstalled(
            Func<string> sessionIdAccessor,
            Func<long> generationAccessor,
            string projectRoot)
        {
            if (sProvider != null)
            {
                return;
            }

            var settingsSource = new GodotEngineSettingsSource();
            var gate = RoslynGate.CreateDefault(settingsSource);
            var engineProvider = new GodotEditorEngineOperationProvider
            {
                SessionIdAccessor = sessionIdAccessor,
                GenerationAccessor = generationAccessor
            };
            var host = new RoslynRunHost(engineProvider.ReadDomainState);
            var store = new RoslynRunStore(projectRoot);
            var scheduler = new RoslynRunScheduler(
                store,
                host,
                () => settingsSource.Read().BlocksExecution, ownerHostId: "godot-editor");
            engineProvider.RunScheduler = scheduler;
            sAutomation = new GodotLiveAutomation(projectRoot, scheduler, engineProvider.ReadDomainState, settingsSource, false);
            engineProvider.AddOperations(sAutomation.Operations);

            // 脚本 eval：GDScript 在内存里编译（SourceCode + Reload + Call），不落盘、不建生成目录。
            engineProvider.ScriptEval = new RoslynScriptEvalService(
                new GodotScriptEvalHost(),
                new RoslynEvalStore(projectRoot));

            sScheduler = scheduler;
            sReconciled = false;
            sProvider = new RoslynKitProvider(gate, engineProvider, settingsSource);

            // 走共享 catalog：Editor 宿主按 revision 重建命令策略，无需在宿主里硬编码 Engine。
            YokiFrameToolKitInteractionCatalog.Register(sProvider);
        }

        /// <summary>在插件 Process 中推进一次。</summary>
        public static void Tick()
        {
            RoslynRunScheduler scheduler = sScheduler;
            if (scheduler == null)
            {
                return;
            }

            if (!sReconciled)
            {
                sReconciled = true;
                scheduler.Reconcile(TimeSpan.FromSeconds(60));
            }

            if (sAutomation != null) sAutomation.Tick();
            scheduler.Tick();
        }

        /// <summary>插件退出编辑器树时释放静态引用。</summary>
        public static void Shutdown()
        {
            if (sAutomation != null) sAutomation.Dispose();
            sAutomation = null;
            sProvider = null;
            sScheduler = null;
            sReconciled = false;
        }
    }
}
#endif
