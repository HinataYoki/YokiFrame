#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>Command surface for transient C# work. No entry catalog is consulted.</summary>
    public sealed class YokiFrameEngineScriptOperations
    {
        private readonly YokiFrameEngineRunScheduler mScheduler;
        private readonly YokiFrameRoslynCompilerLoader mCompiler;
        private readonly YokiFrameRoslynLoadBudget mBudget;
        private readonly Func<YokiFrameEngineDomainState> mState;
        private readonly Func<bool> mPermitted;
        private readonly Func<Func<bool>, YokiFrameAutomationContext> mContext;
        private readonly YokiFrameEngineExecutionTarget mTargets;
        private readonly string mTrustedSetting;

        public YokiFrameEngineScriptOperations(YokiFrameEngineRunScheduler scheduler,
            YokiFrameRoslynCompilerLoader compiler, Func<YokiFrameEngineDomainState> state,
            Func<bool> permitted, Func<Func<bool>, YokiFrameAutomationContext> context,
            YokiFrameRoslynLoadBudget budget = null,
            YokiFrameEngineExecutionTarget targets = YokiFrameEngineExecutionTarget.Editor | YokiFrameEngineExecutionTarget.Play,
            string trustedSetting = "Engine/scripts.trustedCSharp")
        {
            mScheduler = scheduler;
            mCompiler = compiler;
            mState = state;
            mPermitted = permitted;
            mContext = context;
            mBudget = budget ?? new YokiFrameRoslynLoadBudget();
            mTargets = targets;
            mTrustedSetting = trustedSetting;
        }

        public IYokiFrameEngineOperation[] CreateOperations()
        {
            return new IYokiFrameEngineOperation[]
            {
                new Operation(this, "script_run", YokiFrameCommandKind.Dangerous),
                new Operation(this, "script_status", YokiFrameCommandKind.ReadOnly)
            };
        }

        private YokiFrameCommandResult Execute(string action, YokiFrameCommandRequest request)
        {
            if (action == "script_status")
                return YokiFrameCommandResult.Success(new YokiFrameEngineJsonBuilder().StartObject()
                    .Property("operation", action).Property("backend", "roslyn-4.8.0")
                    .Property("language", "csharp-9").Property("installed", mCompiler.Installed)
                    .Property("executionPermitted", mPermitted())
                    .Property("loadedAssemblies", mBudget.LoadedCount).Property("loadedBytes", mBudget.LoadedBytes)
                    .Property("maxAssemblies", YokiFrameRoslynLoadBudget.MaxAssemblies)
                    .Property("maxLoadedBytes", YokiFrameRoslynLoadBudget.MaxLoadedBytes)
                    .Property("remainingAssemblies", YokiFrameRoslynLoadBudget.MaxAssemblies - mBudget.LoadedCount)
                    .Property("remainingBytes", YokiFrameRoslynLoadBudget.MaxLoadedBytes - mBudget.LoadedBytes)
                    .Property("budgetWarning", mBudget.ReadStatus().BudgetWarning)
                    .Property("recoveryHint", mBudget.ReadStatus().RecoveryHint)
                    .Property("warningRemainingAssemblies", YokiFrameRoslynLoadBudget.WarningRemainingAssemblies)
                    .Property("warningRemainingBytes", YokiFrameRoslynLoadBudget.WarningRemainingBytes)
                    .Property("budgetScope", "domain").Property("budgetConfiguration", "hostConstants")
                    .Property("trustedSetting", mTrustedSetting).EndObject().ToString());

            return Submit(request);
        }

        private YokiFrameCommandResult Submit(YokiFrameCommandRequest request)
        {
            if (!mPermitted())
                return YokiFrameCommandResult.Error("ScriptExecutionNotPermitted",
                    "Enable Engine operations and explicitly authorize " + mTrustedSetting + ".");
            if (!mCompiler.Installed)
                return YokiFrameCommandResult.Error("ScriptCompilerUnavailable",
                    "The project-local Roslyn bundle is missing. No fallback was attempted.");
            try
            {
                using (JsonDocument document = JsonDocument.Parse(request.PayloadJson))
                {
                    JsonElement root = document.RootElement;
                    if (!root.TryGetProperty("confirmed", out var confirmed) || confirmed.ValueKind != JsonValueKind.True)
                        return YokiFrameCommandResult.Error("ScriptConfirmationRequired", "Trusted C# requires confirmed:true.");
                    if (!root.TryGetProperty("code", out var input) || input.ValueKind != JsonValueKind.String
                        || !root.TryGetProperty("target", out var requestedTarget) || requestedTarget.ValueKind != JsonValueKind.String)
                        return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, "code and target are required strings.");
                    string body = input.GetString();
                    string target = requestedTarget.GetString();
                    if (string.IsNullOrWhiteSpace(body) || Encoding.UTF8.GetByteCount(body) > 128 * 1024)
                        return YokiFrameCommandResult.Error("ScriptInputLimit", "C# body must be nonempty and at most 128 KiB.");
                    int timeoutMs = 30000;
                    if (root.TryGetProperty("timeoutMs", out var timeout))
                    {
                        if (!timeout.TryGetInt64(out long ms) || ms < 1 || ms > 600000)
                            return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, "timeoutMs must be 1..600000.");
                        timeoutMs = (int)ms;
                    }
                    var state = mState();
                    if (!state.SessionIdentityAvailable || state.IsCompiling || state.IsBusy || target != state.ActiveTarget)
                        return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.UNAVAILABLE,
                            "The requested target must be active, idle and have a published session identity.");
                    string hash = YokiFrameEngineEvalRequest.ComputeSha256(new YokiFrameEngineJsonBuilder()
                        .StartObject().Property("code", body).Property("target", target)
                        .Property("timeoutMs", timeoutMs).EndObject().ToString());
                    YokiFrameEngineRunRecord record = mScheduler.SubmitWork(target, request.RequestId, request.Source,
                        hash, timeoutMs, () =>
                        {
                            Func<bool> allowed = () =>
                            {
                                var current = mState();
                                return mPermitted() && current.SessionId == state.SessionId
                                    && current.Generation == state.Generation && current.ActiveTarget == target
                                    && !current.IsCompiling;
                            };
                            return new YokiFrameRoslynRunWork(body, mCompiler.CaptureReferences(), mContext(allowed), mCompiler, mBudget);
                        });
                    return YokiFrameCommandResult.Success(new YokiFrameEngineJsonBuilder().StartObject()
                        .Property("operation", "script_run").Property("accepted", true)
                        .Property("runId", record.RunId).Property("requestId", record.RequestId)
                        .Property("state", record.State.ToString()).Property("observeAction", "run_result")
                        .EndObject().ToString());
                }
            }
            catch (JsonException exception)
            {
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, exception.Message);
            }
            catch (ArgumentException exception)
            {
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                return YokiFrameCommandResult.Error("ScriptSubmissionRejected", exception.Message);
            }
        }

        internal static string[] CaptureReferences(string referenceDirectory = null)
        {
            var references = new List<string>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;
                string path = assembly.Location;
                if (string.IsNullOrEmpty(path) && referenceDirectory != null)
                {
                    string candidate = YokiFrameFilePathPolicy.CombineInside(referenceDirectory, assembly.GetName().Name + ".dll");
                    if (File.Exists(candidate) && AssemblyName.GetAssemblyName(candidate).FullName == assembly.FullName)
                        path = candidate;
                }
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                if (names.Add(assembly.GetName().Name)) references.Add(path);
            }
            return references.ToArray();
        }

        private sealed class Operation : IYokiFrameEngineOperation
        {
            private readonly YokiFrameEngineScriptOperations mOwner;
            public Operation(YokiFrameEngineScriptOperations owner, string action, YokiFrameCommandKind kind)
            {
                mOwner = owner;
                Descriptor = new YokiFrameEngineOperationDescriptor(action, kind,
                    isDiagnostic: kind == YokiFrameCommandKind.ReadOnly, isCancellation: action == "run_cancel",
                    targets: owner.mTargets,
                    isTargetAgnostic: action != "script_run",
                    executionPermission: action == "script_run" ? owner.mPermitted : null);
            }
            public YokiFrameEngineOperationDescriptor Descriptor { get; }
            public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
            {
                return mOwner.Execute(Descriptor.Action, request);
            }
        }
    }
}
#endif
