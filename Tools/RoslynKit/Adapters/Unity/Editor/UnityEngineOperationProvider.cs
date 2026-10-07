#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// Unity 编辑器宿主的引擎侧 Provider：声明身份、可承载的执行目标，并报告当前引擎状态。
    /// </summary>
    /// <remarks>
    /// Unity 的 Play Mode 运行在编辑器进程内，因此 play 与 editor 都由本宿主承载；
    /// Unity 独立 Player 进程本期没有 FileBridge 宿主，runtime 目标不在 HostTargets 中。
    /// </remarks>
    internal sealed class UnityEngineOperationProvider : IRoslynOperationProvider, IInspectRootSource
    {
        /// <summary>Unity 专有操作；诊断与能力查询由 RoslynKit 内建。</summary>
        private RoslynHostIdentityReader mIdentityReader;

        private static readonly IRoslynOperation[] sOperations =
        {
            new UnityPlayControlOperation(),
            new UnitySceneQueryOperation(),
            new UnitySceneMutateOperation(),
            new UnityAssetOpsOperation()
        };

        /// <summary>获取引擎类型。</summary>
        public string EngineKind
        {
            get { return "Unity"; }
        }

        /// <summary>获取 Unity 版本。</summary>
        public string EngineVersion
        {
            get { return Application.unityVersion; }
        }

        /// <summary>获取当前宿主可承载的目标：编辑态与编辑器进程内的运行态。</summary>
        public RoslynExecutionTarget HostTargets
        {
            get
            {
                return RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play;
            }
        }

        /// <summary>获取 Unity 专有操作集合。</summary>
        public IReadOnlyList<IRoslynOperation> Operations
        {
            get { return mOperations ?? sOperations; }
        }

        private IRoslynOperation[] mOperations;
        internal void AddOperations(IRoslynOperation[] additional)
        {
            var operations = new List<IRoslynOperation>(sOperations);
            operations.AddRange(additional);
            mOperations = operations.ToArray();
        }

        /// <summary>获取或设置运行调度器。</summary>
        internal IRoslynRunScheduler RunScheduler { get; set; }

        /// <summary>获取运行调度器（§10）。</summary>
        IRoslynRunScheduler IRoslynOperationProvider.Runs
        {
            get { return RunScheduler; }
        }

        /// <summary>获取 inspect 的额外根（Unity：单例注册表）。</summary>
        IReadOnlyList<IInspectRoot> IInspectRootSource.Roots
        {
            get { return UnityInspectRoots.Create(); }
        }

        /// <summary>获取或设置项目根目录。</summary>
        internal string ProjectRoot { get; set; }

        /// <summary>获取会话身份读取器；首次访问时按项目根与 engine id 解析 registry 路径。</summary>
        private RoslynHostIdentityReader IdentityReader
        {
            get
            {
                if (mIdentityReader == null && !string.IsNullOrEmpty(ProjectRoot))
                {
                    var paths = new YokiFrameFileBridgeEnginePathSet(
                        ProjectRoot,
                        "unity-editor");
                    mIdentityReader = new RoslynHostIdentityReader(paths.RegistryPath);
                }

                return mIdentityReader;
            }
        }

        /// <summary>
        /// 读取当前编辑器状态；必须在主线程调用。
        /// </summary>
        /// <returns>引擎状态。</returns>
        public RoslynDomainState ReadDomainState()
        {
            bool isPlaying = EditorApplication.isPlaying;

            // 会话身份直接读已发布的 engine.json：既不需要改 pump，也不碰它的私有静态字段。
            RoslynHostIdentityReader reader = IdentityReader;
            string sessionId = string.Empty;
            long generation = 0L;
            bool identityAvailable = reader != null
                                     && reader.TryRead(out sessionId, out generation, out _);
            return new RoslynDomainState
            {
                EngineKind = EngineKind,
                EngineVersion = EngineVersion,
                Mode = isPlaying ? "PlayMode" : "EditMode",
                ActiveTarget = isPlaying ? RoslynExecutionTargets.PLAY : RoslynExecutionTargets.EDITOR,
                IsPlaying = isPlaying,
                IsCompiling = EditorApplication.isCompiling,
                IsBusy = EditorApplication.isUpdating,
                SessionId = identityAvailable ? sessionId : string.Empty,
                Generation = identityAvailable ? generation : 0L,
                SessionIdentityAvailable = identityAvailable
            };
        }
    }
}
#endif
