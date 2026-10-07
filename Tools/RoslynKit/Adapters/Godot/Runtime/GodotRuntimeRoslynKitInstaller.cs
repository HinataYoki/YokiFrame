#if GODOT && TOOLS
using System;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// 把 RoslynKit 接到 Godot Runtime 宿主：注册 Kit Provider，并在 Process 中推进入口调度器。
    /// </summary>
    /// <remarks>
    /// 与 Unity 侧同一套 Core 实现（Gate / 操作 / 入口执行器），差异只在引擎接缝：
    /// 目标为 runtime、会话身份来自 Runtime FileBridge 宿主、开关来自 project.godot。
    /// </remarks>
    public static class GodotRuntimeRoslynKitInstaller
    {
        static GodotRuntimeRoslynKitInstaller()
        {
            GodotRoslynKitRuntimeHooks.EnsureInstalled = EnsureInstalled;
            GodotRoslynKitRuntimeHooks.Tick = Tick;
            GodotRoslynKitRuntimeHooks.Shutdown = Shutdown;
        }

        private static RoslynKitProvider sProvider;
        private static RoslynRunScheduler sScheduler;
        private static bool sReconciled;
        private static GodotLiveAutomation sAutomation;

        /// <summary>幂等安装 RoslynKit。</summary>
        /// <param name="owner">用于获取场景树的节点。</param>
        /// <param name="sessionIdAccessor">会话标识来源。</param>
        /// <param name="generationAccessor">域代次来源。</param>
        /// <param name="projectRoot">Godot 项目根绝对路径。</param>
        public static void EnsureInstalled(
            Node owner,
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
            var engineProvider = new GodotRuntimeEngineOperationProvider
            {
                SceneOwner = owner,
                SessionIdAccessor = sessionIdAccessor,
                GenerationAccessor = generationAccessor
            };
            var host = new RoslynRunHost(engineProvider.ReadDomainState);
            var store = new RoslynRunStore(projectRoot);
            var scheduler = new RoslynRunScheduler(
                store,
                host,
                () => settingsSource.Read().BlocksExecution, ownerHostId: "godot-runtime");
            engineProvider.RunScheduler = scheduler;
            sAutomation = new GodotLiveAutomation(projectRoot, scheduler, engineProvider.ReadDomainState, settingsSource, true);
            engineProvider.AddOperations(sAutomation.Operations);

            // 脚本 eval：GDScript 在内存里编译（SourceCode + Reload + Call），不落盘、不建生成目录。
            engineProvider.ScriptEval = new RoslynScriptEvalService(
                new GodotScriptEvalHost(),
                new RoslynEvalStore(projectRoot));

            sScheduler = scheduler;
            sReconciled = false;
            sProvider = new RoslynKitProvider(gate, engineProvider, settingsSource);

            // 走共享 catalog：Runtime FileBridge 宿主按 revision 自动聚合，无需改宿主代码。
            YokiFrameToolKitInteractionCatalog.Register(sProvider);
        }

        /// <summary>在 Godot Process 中推进一次。</summary>
        public static void Tick()
        {
            RoslynRunScheduler scheduler = sScheduler;
            if (scheduler == null)
            {
                return;
            }

            if (!sReconciled)
            {
                // 进程内只对账一次：把上次异常退出留下的 Running 判为 Unknown，绝不重放。
                sReconciled = true;
                scheduler.Reconcile(TimeSpan.FromSeconds(60));
            }

            if (sAutomation != null) sAutomation.Tick();
            scheduler.Tick();
        }

        /// <summary>退出场景树时释放静态引用，避免残留 Provider 指向已销毁的节点。</summary>
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
