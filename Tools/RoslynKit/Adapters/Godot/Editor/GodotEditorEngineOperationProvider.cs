#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// Godot Editor 宿主的引擎侧 Provider：承载 editor 目标（§4）。
    /// </summary>
    /// <remarks>
    /// 目标与 Runtime 宿主严格区分：godot-editor 只承载 editor，godot-runtime 只承载 runtime；
    /// 播放态（play）在 Godot 由 Runtime 宿主承担，因此这里不声明。
    /// </remarks>
    public sealed class GodotEditorEngineOperationProvider : IRoslynOperationProvider, IRoslynScriptEvalProvider
    {
        /// <summary>获取引擎类型。</summary>
        public string EngineKind
        {
            get { return "Godot"; }
        }

        /// <summary>获取或设置引擎版本文本来源；为空时读取 Godot 原生版本。</summary>
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
                    return string.Empty;
                }
            }
        }

        /// <summary>获取宿主承载的目标；godot-editor 只承载 editor。</summary>
        public RoslynExecutionTarget HostTargets
        {
            get { return RoslynExecutionTarget.Editor; }
        }

        private static readonly IRoslynOperation[] sOperations =
        {
            new GodotEditorSceneQueryOperation(),
            new GodotEditorSceneMutateOperation()
        };
        private readonly List<IRoslynOperation> mOperations = new List<IRoslynOperation>(sOperations);

        /// <summary>追加操作。后加的操作与内置场景操作一起暴露，不替换已有描述符。</summary>
        /// <param name="operations">要追加的操作。</param>
        public void AddOperations(params IRoslynOperation[] operations) => mOperations.AddRange(operations);

        /// <summary>获取 Godot 专有操作：场景查询（当前编辑场景）。</summary>
        public IReadOnlyList<IRoslynOperation> Operations
        {
            get { return mOperations; }
        }

        /// <summary>获取或设置"是否正在播放"来源；为空时读 Godot EditorInterface 原生单例。</summary>
        public Func<bool> IsPlayingAccessor { get; set; }

        /// <summary>获取或设置会话标识来源。</summary>
        public Func<string> SessionIdAccessor { get; set; }

        /// <summary>获取或设置域代次来源。</summary>
        public Func<long> GenerationAccessor { get; set; }

        /// <summary>获取或设置运行调度器。</summary>
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


        /// <summary>读取当前编辑器状态。</summary>
        /// <returns>引擎状态。</returns>
        public RoslynDomainState ReadDomainState()
        {
            string sessionId = SessionIdAccessor == null ? string.Empty : SessionIdAccessor() ?? string.Empty;
            long generation = GenerationAccessor == null ? 0L : GenerationAccessor();
            bool isPlaying = IsPlayingAccessor != null ? IsPlayingAccessor() : IsPlaying();
            return new RoslynDomainState
            {
                EngineKind = EngineKind,
                EngineVersion = EngineVersion,
                Mode = isPlaying ? "PlayMode" : "Editor",
                ActiveTarget = RoslynExecutionTargets.EDITOR,
                IsPlaying = isPlaying,
                IsCompiling = false,
                IsBusy = false,
                SessionId = sessionId,
                Generation = generation,
                SessionIdentityAvailable = sessionId.Length > 0
            };
        }

        /// <summary>读取编辑器是否正在播放场景。宿主未初始化或接口抛错时按编辑态处理，不把异常冒泡成命令失败。</summary>
        /// <returns>正在播放时返回 true。</returns>
        private static bool IsPlaying()
        {
            try
            {
                // 编辑器内运行游戏时 EditorInterface 会报告正在播放；宿主未初始化时按编辑态处理。
                return EditorInterface.Singleton != null && EditorInterface.Singleton.IsPlayingScene();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
#endif
