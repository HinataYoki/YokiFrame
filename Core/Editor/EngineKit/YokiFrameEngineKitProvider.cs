#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Engine Kit 的宿主 Provider：把引擎侧 Provider、Gate 与命令面组装成 Kit 交互契约。
    /// </summary>
    /// <remarks>
    /// 该类是胶水层的宿主侧入口：宿主只需注册它，命令面、snapshot 与能力报告一并生效。
    /// </remarks>
#if UNITY_EDITOR || (GODOT && TOOLS)
    public sealed class YokiFrameEngineKitProvider : IYokiFrameSnapshotVersionedKitInteractionProvider
#else
    public sealed class YokiFrameEngineKitProvider : IYokiFrameKitInteractionProvider
#endif
    {
        /// <summary>Engine Kit 的稳定 Kit 标识。</summary>
        public const string KIT_NAME = "Engine";

        /// <summary>state snapshot 名称。</summary>
        public const string STATE_SNAPSHOT = "state";

        private static readonly string[] sSnapshotNames = { STATE_SNAPSHOT };

        private readonly YokiFrameEngineCommandHandler mHandler;
        private readonly IYokiFrameEngineOperation[] mOperations;
        private readonly YokiFrameEngineObjectCatalog mObjects;

        /// <summary>运行枚举缓存时长（毫秒）：版本指纹可能被每帧读取，不能每次枚举目录。</summary>
        private const int RUNS_CACHE_MS = 250;

        private IReadOnlyList<YokiFrameEngineRunRecord> mCachedRuns;
        private DateTime mCachedRunsAtUtc = DateTime.MinValue;
        private readonly IYokiFrameEngineOperationProvider mEngineProvider;
        private readonly IYokiFrameEngineSettingsSource mSettingsSource;
        private readonly object mStateVersionSync = new object();
        private long mStateVersion = 1L;
        private string mLastStateFingerprint = string.Empty;

        /// <summary>
        /// 创建 Engine Kit Provider。
        /// </summary>
        /// <param name="gate">Engine 策略 Gate。</param>
        /// <param name="engineProvider">引擎侧 Provider。</param>
        /// <param name="settingsSource">执行开关读取端口。</param>
        public YokiFrameEngineKitProvider(
            YokiFrameEngineGate gate,
            IYokiFrameEngineOperationProvider engineProvider,
            IYokiFrameEngineSettingsSource settingsSource)
        {
            if (gate == null)
            {
                throw new ArgumentNullException(nameof(gate));
            }

            mEngineProvider = engineProvider ?? throw new ArgumentNullException(nameof(engineProvider));
            mSettingsSource = settingsSource ?? throw new ArgumentNullException(nameof(settingsSource));

            // 内建诊断操作 + 引擎侧操作共同组成命令面；capabilities 通过回调看到完整集合。
            var operations = new List<IYokiFrameEngineOperation>();
            var capabilities = new YokiFrameEngineCapabilitiesOperation(engineProvider, settingsSource, () => operations);
            operations.Add(new YokiFrameEngineDomainStateOperation(engineProvider, settingsSource));
            // Legacy value inspection is separate from metadata-only object discovery.
            YokiFrameInspectRootRegistry inspectRoots = YokiFrameInspectRootRegistry.CreateDefault();
            IYokiFrameInspectRootSource inspectSource = engineProvider as IYokiFrameInspectRootSource;
            if (inspectSource != null && inspectSource.Roots != null)
            {
                for (var rootIndex = 0; rootIndex < inspectSource.Roots.Count; rootIndex++)
                {
                    inspectRoots.Add(inspectSource.Roots[rootIndex]);
                }
            }

            operations.Add(new YokiFrameEngineInspectOperation(inspectRoots));
            mObjects = new YokiFrameEngineObjectCatalog(engineProvider);
            operations.AddRange(mObjects.CreateOperations());

            operations.Add(capabilities);
            IReadOnlyList<IYokiFrameEngineOperation> engineOperations = engineProvider.Operations;
            for (var index = 0; index < engineOperations.Count; index++)
            {
                operations.Add(engineOperations[index]);
            }

            // Only Godot's independent script eval remains. Unity uses script_run.
            IYokiFrameEngineScriptEvalProvider scriptProvider = engineProvider as IYokiFrameEngineScriptEvalProvider;
            IYokiFrameEngineScriptEvalService scriptService = scriptProvider == null ? null : scriptProvider.ScriptEval;
            if (scriptService != null)
            {
                operations.Add(new YokiFrameEngineEvalOperation(scriptService, engineProvider.HostTargets));
                operations.Add(new YokiFrameEngineEvalResultOperation(scriptService));
                operations.Add(new YokiFrameEngineEvalPruneOperation(scriptService));
            }

            if (engineProvider.Runs != null)
            {
                operations.Add(new YokiFrameEngineRunOperation(engineProvider.Runs, "run_result"));
                operations.Add(new YokiFrameEngineRunOperation(engineProvider.Runs, "run_lookup"));
                operations.Add(new YokiFrameEngineRunOperation(engineProvider.Runs, "run_cancel"));
            }

            mOperations = operations.ToArray();
            mHandler = new YokiFrameEngineCommandHandler(gate, mOperations, engineProvider.HostTargets);
        }

        /// <summary>
        /// 获取 state snapshot 的单调变化版本；引擎状态或执行开关变化时递增。
        /// </summary>
        /// <remarks>
        /// 实现 <see cref="IYokiFrameSnapshotVersionedKitInteractionProvider"/>，宿主因此只在状态变化时重写文件快照，
        /// 无需增加全量刷新频率，也不需要修改 pump。
        /// </remarks>
        public long StateVersion
        {
            get
            {
                string fingerprint = BuildStateFingerprint();
                lock (mStateVersionSync)
                {
                    if (!string.Equals(fingerprint, mLastStateFingerprint, StringComparison.Ordinal))
                    {
                        mLastStateFingerprint = fingerprint;
                        mStateVersion++;
                    }

                    return mStateVersion;
                }
            }
        }

        /// <summary>
        /// 读取最近运行（带 TTL 缓存）。
        /// </summary>
        /// <remarks>
        /// 版本指纹会被宿主泵每帧读取，而运行枚举要读目录；不缓存会把"每帧一次目录枚举"带进编辑器主循环。
        /// 250ms 的陈旧窗口对发布语义无影响：下一次刷新会推进版本、触发快照重写。
        /// </remarks>
        /// <returns>最近运行；宿主未接调度器时为 null。</returns>
        private IReadOnlyList<YokiFrameEngineRunRecord> ReadRecentRunsCached()
        {
            IYokiFrameEngineRunScheduler scheduler = mEngineProvider.Runs;
            if (scheduler == null)
            {
                return null;
            }

            DateTime now = DateTime.UtcNow;
            if (mCachedRuns != null && (now - mCachedRunsAtUtc).TotalMilliseconds < RUNS_CACHE_MS)
            {
                return mCachedRuns;
            }

            mCachedRuns = scheduler.ReadRecentRuns(YokiFrameEngineSnapshotWriter.MAX_RUNS);
            mCachedRunsAtUtc = now;
            return mCachedRuns;
        }

        /// <summary>
        /// 计算引擎状态、开关、命令面与服务注册的组合指纹，用于判定 snapshot 是否需要重写。
        /// </summary>
        /// <returns>稳定指纹文本。</returns>
        private string BuildStateFingerprint()
        {
            YokiFrameEngineDomainState state = mEngineProvider.ReadDomainState();
            YokiFrameEngineSettingsSnapshot settings = mSettingsSource.Read();
            var builder = new System.Text.StringBuilder(128);
            builder.Append(state.EngineKind).Append('|')
                .Append(state.EngineVersion).Append('|')
                .Append(state.Mode).Append('|')
                .Append(state.ActiveTarget).Append('|')
                .Append(state.IsPlaying ? '1' : '0')
                .Append(state.IsCompiling ? '1' : '0')
                .Append(state.IsBusy ? '1' : '0').Append('|')
                .Append(state.SessionIdentityAvailable ? '1' : '0')
                .Append(state.SessionId).Append('|')
                .Append(state.Generation).Append('|')
                .Append(settings.State.ToString()).Append('|')
                .Append(settings.Reason);

            // 命令面变化（例如 eval 子系统接线、适配器新增操作）也必须让快照失效重写。
            builder.Append('|');
            for (var index = 0; index < mOperations.Length; index++)
            {
                YokiFrameEngineOperationDescriptor descriptor = mOperations[index].Descriptor;
                builder.Append(descriptor.Action).Append(',')
                    .Append(descriptor.Kind).Append(',')
                    .Append((int)descriptor.Targets).Append(',')
                    .Append(descriptor.ExemptFromExecutionSwitch ? '1' : '0')
                    .Append(descriptor.ExecutionPermitted ? '1' : '0').Append(';');
            }

            // 运行状态变化（新运行、运行结束）必须让快照重写，否则页面停在旧记录。
            if (mEngineProvider.Runs != null)
            {
                IReadOnlyList<YokiFrameEngineRunRecord> runs = ReadRecentRunsCached();
                builder.Append("|runs:").Append(runs.Count);
                for (var index = 0; index < runs.Count; index++)
                {
                    builder.Append(runs[index].RunId).Append(runs[index].State.ToString())
                        .Append(runs[index].UpdatedAtUtc.Ticks);
                }
            }

            builder.Append("|objects:").Append(ArchitectureRegistry.DiagnosticVersion)
                .Append('|').Append(mObjects.ObserveScope(state));

            return builder.ToString();
        }

        /// <summary>获取 Kit 标识。</summary>
        public string Kit
        {
            get { return KIT_NAME; }
        }

        /// <summary>获取可创建的 snapshot 名称。</summary>
        public IReadOnlyList<string> SnapshotNames
        {
            get { return sSnapshotNames; }
        }

        /// <summary>获取命令描述；宿主策略由它聚合。</summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Commands
        {
            get { return mHandler.Descriptors; }
        }

        /// <summary>Invalidates object IDs at host lifecycle boundaries, including no-domain-reload Play Mode.</summary>
        public void InvalidateObjectHandles() => mObjects.Invalidate();

        /// <summary>
        /// 创建 state snapshot；与 domain_state 动作返回同一份数据，保证 CLI 读取路径一致。
        /// </summary>
        /// <param name="snapshotName">snapshot 名称。</param>
        /// <returns>snapshot JSON。</returns>
        public string CreateSnapshot(string snapshotName)
        {
            if (!string.Equals(snapshotName, STATE_SNAPSHOT, StringComparison.Ordinal))
            {
                throw new ArgumentException("Unsupported Engine snapshot: " + snapshotName + ".", nameof(snapshotName));
            }

            // Workbench reads capabilities, live registrations and persisted results.
            IReadOnlyList<YokiFrameEngineRunRecord> recentRuns = ReadRecentRunsCached();

            return YokiFrameEngineSnapshotWriter.Write(
                mEngineProvider.ReadDomainState(),
                mSettingsSource.Read(),
                mOperations,
                mEngineProvider.HostTargets,
                mObjects,
                recentRuns);
        }

        /// <summary>判断当前 Provider 是否处理该命令。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命中时返回 true。</returns>
        public bool CanHandle(YokiFrameCommandRequest request)
        {
            return mHandler.CanHandle(request);
        }

        /// <summary>执行命令。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            return mHandler.Handle(request);
        }
    }
}
#endif
