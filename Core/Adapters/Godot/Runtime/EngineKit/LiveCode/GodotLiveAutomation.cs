#if GODOT && TOOLS
using System;
using System.Collections.Generic;
using Godot;

namespace YokiFrame
{
    /// <summary>Per-host trusted automation. Runtime prototypes run only in Tools builds, never exported players.</summary>
    public sealed class GodotLiveAutomation : IDisposable
    {
        public const string TrustedSetting = "yokiframe/engine/trusted_csharp";
        // Recreating a plugin/bootstrap does not unload assemblies in this Godot load context.
        private static readonly YokiFrameRoslynLoadBudget sBudget = new YokiFrameRoslynLoadBudget();
        private readonly YokiFrameLiveCodeManager mManager;
        private readonly YokiFrameLiveTuningBinder mTuning;
        private readonly YokiFrameRoslynCompilerLoader mCompiler;
        private bool mDisposed;
        private readonly Func<YokiFrameEngineDomainState> mState;
        private readonly Func<bool> mPermitted;
        private long mStarted = -1;
        private long mCompleted = -1;
        private long mObservedProcessFrame = -1;
        private string mSession;
        private long mGeneration;
        private readonly bool mRuntime;
        public IYokiFrameEngineOperation[] Operations { get; }

        public GodotLiveAutomation(string projectRoot, YokiFrameEngineRunScheduler scheduler,
            Func<YokiFrameEngineDomainState> state, IYokiFrameEngineSettingsSource settings, bool runtime)
        {
            mState = state;
            mRuntime = runtime;
            mPermitted = () => !mDisposed && !settings.Read().BlocksExecution
                && ProjectSettings.HasSetting(TrustedSetting)
                && ProjectSettings.GetSetting(TrustedSetting).VariantType == Variant.Type.Bool
                && ProjectSettings.GetSetting(TrustedSetting).AsBool();
            var compiler = mCompiler = GodotRoslynCompiler.Create(projectRoot);
            var budget = sBudget;
            mManager = new YokiFrameLiveCodeManager(compiler, budget, null,
                new GodotLiveCodeHost((id, method, args) => mManager.Invoke(id, method, args, () => { })),
                state, mPermitted, projectRoot);
            mTuning = new YokiFrameLiveTuningBinder(projectRoot, mManager, mPermitted, state);
            var target = runtime ? YokiFrameEngineExecutionTarget.Runtime : YokiFrameEngineExecutionTarget.Editor;
            var scripts = new YokiFrameEngineScriptOperations(scheduler, compiler, state, mPermitted,
                allowed => new YokiFrameAutomationContext(() => mStarted, allowed,
                    message => GD.Print("[YokiFrame.Script] " + message), liveCode: mManager,
                    completedGameFrame: () => mCompleted), budget, target, TrustedSetting);
            var operations = new List<IYokiFrameEngineOperation>(scripts.CreateOperations())
            {
                new YokiFrameLiveCodeOperation(mManager, "live_status"),
                new YokiFrameLiveCodeOperation(mManager, "live_remove"),
                new YokiFrameLiveCodeOperation(mManager, "live_snapshot", mPermitted, target),
                new YokiFrameLiveCodeOperation(mManager, "live_set_fields", mPermitted, target),
                new YokiFrameLiveTuningOperation(mTuning, "live_tuning_bind", mPermitted, target),
                new YokiFrameLiveTuningOperation(mTuning, "live_tuning_refresh", mPermitted, target),
                new YokiFrameLiveTuningOperation(mTuning, "live_tuning_status"),
                new YokiFrameLiveTuningOperation(mTuning, "live_tuning_unbind")
            };
            Operations = operations.ToArray();
        }

        public void Tick()
        {
            var state = mState();
            if (!mPermitted() || (mSession != null && (mSession != state.SessionId || mGeneration != state.Generation)))
            {
                mTuning.StopAll("Godot host session or permission changed; rebind explicitly.");
                mManager.Clear(error => GD.PushError(error.ToString()));
            }
            mSession = state.SessionId; mGeneration = state.Generation;
            if (mRuntime)
            {
                // Count observed passes, not Godot's global frame number (which advances while paused).
                long frame = (long)Engine.GetProcessFrames();
                var tree = Engine.GetMainLoop() as SceneTree;
                if (tree != null && tree.Paused) mObservedProcessFrame = -1;
                else if (frame != mObservedProcessFrame)
                {
                    if (mStarted < 0) { mStarted = 0; mCompleted = 0; }
                    else if (frame == mObservedProcessFrame + 1) { mCompleted = mStarted; mStarted++; }
                    mObservedProcessFrame = frame;
                }
            }
            mTuning.Tick(Time.GetTicksMsec() / 1000d);
        }

        public void Dispose()
        {
            if (mDisposed) return;
            mDisposed = true;
            mTuning.StopAll("Godot host stopped.");
            mManager.Clear(error => GD.PushError(error.ToString()));
            mCompiler.Dispose();
        }
    }
}
#endif
