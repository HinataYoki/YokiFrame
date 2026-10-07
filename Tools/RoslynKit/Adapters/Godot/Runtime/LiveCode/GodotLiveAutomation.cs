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
        private static readonly RoslynLoadBudget sBudget = new RoslynLoadBudget();
        private readonly LiveCodeManager mManager;
        private readonly LiveTuningBinder mTuning;
        private readonly RoslynCompilerLoader mCompiler;
        private bool mDisposed;
        private readonly Func<RoslynDomainState> mState;
        private readonly Func<bool> mPermitted;
        private long mStarted = -1;
        private long mCompleted = -1;
        private long mObservedProcessFrame = -1;
        private string mSession;
        private long mGeneration;
        private readonly bool mRuntime;
        public IRoslynOperation[] Operations { get; }

        public GodotLiveAutomation(string projectRoot, RoslynRunScheduler scheduler,
            Func<RoslynDomainState> state, IRoslynSettingsSource settings, bool runtime)
        {
            mState = state;
            mRuntime = runtime;
            mPermitted = () => !mDisposed && !settings.Read().BlocksExecution
                && ProjectSettings.HasSetting(TrustedSetting)
                && ProjectSettings.GetSetting(TrustedSetting).VariantType == Variant.Type.Bool
                && ProjectSettings.GetSetting(TrustedSetting).AsBool();
            var compiler = mCompiler = GodotRoslynCompiler.Create(projectRoot);
            var budget = sBudget;
            mManager = new LiveCodeManager(compiler, budget, null,
                new GodotLiveCodeHost((id, method, args) => mManager.Invoke(id, method, args, () => { })),
                state, mPermitted, projectRoot);
            mTuning = new LiveTuningBinder(projectRoot, mManager, mPermitted, state);
            var target = runtime ? RoslynExecutionTarget.Runtime : RoslynExecutionTarget.Editor;
            var scripts = new RoslynScriptOperations(scheduler, compiler, state, mPermitted,
                allowed => new AutomationContext(() => mStarted, allowed,
                    message => GD.Print("[YokiFrame.Script] " + message), liveCode: mManager,
                    completedGameFrame: () => mCompleted), budget, target, TrustedSetting);
            var operations = new List<IRoslynOperation>(scripts.CreateOperations())
            {
                new LiveCodeOperation(mManager, "live_status"),
                new LiveCodeOperation(mManager, "live_remove"),
                new LiveCodeOperation(mManager, "live_snapshot", mPermitted, target),
                new LiveCodeOperation(mManager, "live_set_fields", mPermitted, target),
                new LiveTuningOperation(mTuning, "live_tuning_bind", mPermitted, target),
                new LiveTuningOperation(mTuning, "live_tuning_refresh", mPermitted, target),
                new LiveTuningOperation(mTuning, "live_tuning_status"),
                new LiveTuningOperation(mTuning, "live_tuning_unbind")
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
