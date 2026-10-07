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
    public sealed class RoslynScriptOperations
    {
        private readonly RoslynRunScheduler mScheduler;
        private readonly RoslynCompilerLoader mCompiler;
        private readonly RoslynLoadBudget mBudget;
        private readonly Func<RoslynDomainState> mState;
        private readonly Func<bool> mPermitted;
        private readonly Func<Func<bool>, AutomationContext> mContext;
        private readonly RoslynExecutionTarget mTargets;
        private readonly string mTrustedSetting;

        public RoslynScriptOperations(RoslynRunScheduler scheduler,
            RoslynCompilerLoader compiler, Func<RoslynDomainState> state,
            Func<bool> permitted, Func<Func<bool>, AutomationContext> context,
            RoslynLoadBudget budget = null,
            RoslynExecutionTarget targets = RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play,
            string trustedSetting = "RoslynKit/scripts.trustedCSharp")
        {
            mScheduler = scheduler;
            mCompiler = compiler;
            mState = state;
            mPermitted = permitted;
            mContext = context;
            mBudget = budget ?? new RoslynLoadBudget();
            mTargets = targets;
            mTrustedSetting = trustedSetting;
        }

        public IRoslynOperation[] CreateOperations()
        {
            return new IRoslynOperation[]
            {
                new Operation(this, "script_run", YokiFrameCommandKind.Dangerous),
                new Operation(this, "script_status", YokiFrameCommandKind.ReadOnly)
            };
        }

        private YokiFrameCommandResult Execute(string action, YokiFrameCommandRequest request)
        {
            if (action == "script_status")
                return YokiFrameCommandResult.Success(new RoslynJsonBuilder().StartObject()
                    .Property("operation", action).Property("backend", "roslyn-4.8.0")
                    .Property("language", "csharp-9").Property("installed", mCompiler.Installed)
                    .Property("executionPermitted", mPermitted())
                    .Property("loadedAssemblies", mBudget.LoadedCount).Property("loadedBytes", mBudget.LoadedBytes)
                    .Property("loadedBytesMeaning", "cumulative-pe-pdb-not-process-memory")
                    .Property("cachedScripts", mCompiler.CachedScripts).Property("maxCachedScripts", mCompiler.MaxCachedScripts)
                    .Property("scriptCacheHits", mCompiler.ScriptCacheHits).Property("scriptCacheMisses", mCompiler.ScriptCacheMisses)
                    .Property("scriptCacheScope", "compiler-loader; source-and-reference-files")
                    .Property("assemblyReclamation", "host-domain-or-load-context-reset-required")
                    .Property("maxAssemblies", RoslynLoadBudget.MaxAssemblies)
                    .Property("maxLoadedBytes", RoslynLoadBudget.MaxLoadedBytes)
                    .Property("remainingAssemblies", RoslynLoadBudget.MaxAssemblies - mBudget.LoadedCount)
                    .Property("remainingBytes", RoslynLoadBudget.MaxLoadedBytes - mBudget.LoadedBytes)
                    .Property("budgetWarning", mBudget.ReadStatus().BudgetWarning)
                    .Property("recoveryHint", mBudget.ReadStatus().RecoveryHint)
                    .Property("warningRemainingAssemblies", RoslynLoadBudget.WarningRemainingAssemblies)
                    .Property("warningRemainingBytes", RoslynLoadBudget.WarningRemainingBytes)
                    .Property("budgetScope", "domain").Property("budgetConfiguration", "hostConstants")
                    .Property("trustedSetting", mTrustedSetting).EndObject().ToString());

            return Submit(request);
        }

        private YokiFrameCommandResult Submit(YokiFrameCommandRequest request)
        {
            if (!mPermitted())
                return YokiFrameCommandResult.Error("ScriptExecutionNotPermitted",
                    "Enable RoslynKit operations and explicitly authorize " + mTrustedSetting + ".");
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
                        return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, "code and target are required strings.");
                    string body = input.GetString();
                    string target = requestedTarget.GetString();
                    if (string.IsNullOrWhiteSpace(body) || Encoding.UTF8.GetByteCount(body) > 128 * 1024)
                        return YokiFrameCommandResult.Error("ScriptInputLimit", "C# body must be nonempty and at most 128 KiB.");
                    int timeoutMs = 30000;
                    if (root.TryGetProperty("timeoutMs", out var timeout))
                    {
                        if (!timeout.TryGetInt64(out long ms) || ms < 1 || ms > 600000)
                            return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, "timeoutMs must be 1..600000.");
                        timeoutMs = (int)ms;
                    }
                    var state = mState();
                    if (!state.SessionIdentityAvailable || state.IsCompiling || state.IsBusy || target != state.ActiveTarget)
                        return YokiFrameCommandResult.Error(RoslynErrorCodes.UNAVAILABLE,
                            "The requested target must be active, idle and have a published session identity.");
                    string hash = RoslynEvalRequest.ComputeSha256(new RoslynJsonBuilder()
                        .StartObject().Property("code", body).Property("target", target)
                        .Property("timeoutMs", timeoutMs).EndObject().ToString());
                    RoslynRunRecord record = mScheduler.SubmitWork(target, request.RequestId, request.Source,
                        hash, timeoutMs, () =>
                        {
                            Func<bool> allowed = () =>
                            {
                                var current = mState();
                                return mPermitted() && current.SessionId == state.SessionId
                                    && current.Generation == state.Generation && current.ActiveTarget == target
                                    && !current.IsCompiling;
                            };
                            return new RoslynRunWork(body, mCompiler.CaptureReferences(), mContext(allowed), mCompiler, mBudget);
                        });
                    return YokiFrameCommandResult.Success(new RoslynJsonBuilder().StartObject()
                        .Property("operation", "script_run").Property("accepted", true)
                        .Property("runId", record.RunId).Property("requestId", record.RequestId)
                        .Property("state", record.State.ToString()).Property("observeAction", "run_result")
                        .EndObject().ToString());
                }
            }
            catch (JsonException exception)
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, exception.Message);
            }
            catch (ArgumentException exception)
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, exception.Message);
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
            foreach (Assembly assembly in LoadedAssemblies.Get())
            {
                if (assembly.IsDynamic || !IsLiveCompileReference(assembly)) continue;
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

        /// <summary>
        /// 判断程序集能否作为 Play 原型的编译引用。
        /// Editor 程序集除原型外观外一律排除，避免用户代码引用到不进 Player 的编辑器类型。
        /// </summary>
        /// <param name="assembly">已加载程序集。</param>
        /// <returns>可以加入 Roslyn 引用时返回 true。</returns>
        private static bool IsLiveCompileReference(Assembly assembly)
        {
            string name = assembly.GetName().Name;
            if (string.Equals(name, "YokiFrame.Unity.LiveCode.Facade", StringComparison.Ordinal)) return true;
            if (name.StartsWith("UnityEditor", StringComparison.Ordinal)) return false;
            return !name.EndsWith(".Editor", StringComparison.Ordinal)
                || string.Equals(name, "YokiFrame.RoslynKit.Editor", StringComparison.Ordinal);
        }

        private sealed class Operation : IRoslynOperation
        {
            private readonly RoslynScriptOperations mOwner;
            public Operation(RoslynScriptOperations owner, string action, YokiFrameCommandKind kind)
            {
                mOwner = owner;
                Descriptor = new RoslynOperationDescriptor(action, kind,
                    isDiagnostic: kind == YokiFrameCommandKind.ReadOnly, isCancellation: action == "run_cancel",
                    targets: owner.mTargets,
                    isTargetAgnostic: action != "script_run",
                    executionPermission: action == "script_run" ? owner.mPermitted : null);
            }
            public RoslynOperationDescriptor Descriptor { get; }
            public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
            {
                return mOwner.Execute(Descriptor.Action, request);
            }
        }
    }
}
#endif
