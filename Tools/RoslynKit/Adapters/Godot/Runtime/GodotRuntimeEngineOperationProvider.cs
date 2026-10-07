#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// Godot Runtime 宿主（godot-runtime）的引擎侧 Provider：承载 runtime 目标（§4）。
    /// </summary>
    /// <remarks>
    /// 会话身份直接来自 Runtime FileBridge 宿主（它已经维护 sessionId/generation），
    /// 因此这里的 <c>sessionIdentityAvailable</c> 为 true —— 与 Unity 侧"未接入 pump"的缺口不同。
    /// </remarks>
    public sealed class GodotRuntimeEngineOperationProvider : IRoslynOperationProvider, IRoslynScriptEvalProvider
    {
        /// <summary>获取引擎类型。</summary>
        public string EngineKind
        {
            get { return "Godot"; }
        }

        /// <summary>获取或设置引擎版本文本来源；为空时读取 Godot 原生版本。</summary>
        /// <remarks>
        /// 允许注入的理由与其它接缝一致：Godot 原生静态 API 只在真实宿主机进程内可用，
        /// 单元测试进程调用可能直接终止进程，而不是抛可捕获的托管异常。
        /// </remarks>
        public Func<string> EngineVersionAccessor { get; set; }

        /// <summary>获取 Godot 版本。</summary>
        public string EngineVersion
        {
            get
            {
                Func<string> accessor = EngineVersionAccessor;
                if (accessor != null)
                {
                    return accessor() ?? string.Empty;
                }

                try
                {
                    Godot.Collections.Dictionary info = Engine.GetVersionInfo();
                    return info.ContainsKey("string") ? info["string"].AsString() : string.Empty;
                }
                catch (Exception)
                {
                    // 宿主未初始化时不伪造版本号，返回空串由调用方判定。
                    return string.Empty;
                }
            }
        }

        /// <summary>获取宿主承载的目标；godot-runtime 只承载 runtime。</summary>
        public RoslynExecutionTarget HostTargets
        {
            get { return RoslynExecutionTarget.Runtime; }
        }

        private IRoslynOperation[] mOperations;
        private readonly List<IRoslynOperation> mAdditional = new List<IRoslynOperation>();
        public void AddOperations(params IRoslynOperation[] operations)
        {
            mAdditional.AddRange(operations);
            mOperations = null;
        }

        /// <summary>获取或设置场景根来源（Bootstrap 节点）；未接线时不注册场景操作。</summary>
        public Node SceneOwner { get; set; }

        /// <summary>获取 Godot 专有操作：场景查询（运行中场景树）。</summary>
        public IReadOnlyList<IRoslynOperation> Operations
        {
            get
            {
                if (mOperations == null)
                {
                    var operations = new List<IRoslynOperation>(2);
                    if (SceneOwner != null)
                    {
                        operations.Add(new GodotRuntimeSceneQueryOperation(SceneOwner));
                        operations.Add(new GodotRuntimeSceneMutateOperation(SceneOwner));
                    }

                    operations.AddRange(mAdditional);
                    mOperations = operations.ToArray();
                }

                return mOperations;
            }
        }

        /// <summary>获取或设置会话标识来源；由安装器接线。</summary>
        public Func<string> SessionIdAccessor { get; set; }

        /// <summary>获取或设置域代次来源；由安装器接线。</summary>
        public Func<long> GenerationAccessor { get; set; }

        /// <summary>获取或设置运行调度器；由安装器接线。</summary>
        public IRoslynRunScheduler RunScheduler { get; set; }

        /// <summary>获取运行调度器（§10）。</summary>
        IRoslynRunScheduler IRoslynOperationProvider.Runs
        {
            get { return RunScheduler; }
        }

        /// <summary>获取或设置脚本 eval 服务（Godot 走 GDScript 运行时编译，内存中完成）；由安装器接线。</summary>
        public IRoslynScriptEvalService ScriptEval { get; set; }

        /// <summary>获取脚本 eval 服务（§11 脚本路径）。</summary>
        IRoslynScriptEvalService IRoslynScriptEvalProvider.ScriptEval
        {
            get { return ScriptEval; }
        }


        /// <summary>读取当前运行时状态。</summary>
        /// <returns>引擎状态。</returns>
        public RoslynDomainState ReadDomainState()
        {
            string sessionId = SessionIdAccessor == null ? string.Empty : SessionIdAccessor() ?? string.Empty;
            long generation = GenerationAccessor == null ? 0L : GenerationAccessor();
            bool identityAvailable = sessionId.Length > 0;
            return new RoslynDomainState
            {
                EngineKind = EngineKind,
                EngineVersion = EngineVersion,
                Mode = "Runtime",
                ActiveTarget = RoslynExecutionTargets.RUNTIME,
                IsPlaying = true,
                IsCompiling = false,
                IsBusy = false,
                SessionId = sessionId,
                Generation = generation,
                SessionIdentityAvailable = identityAvailable
            };
        }
    }
}
#endif
