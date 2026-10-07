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

        /// <summary>创建临时 C# 命令面。预算缺省时使用宿主常量预算。</summary>
        /// <param name="scheduler">运行调度器。</param>
        /// <param name="compiler">项目内编译器。</param>
        /// <param name="state">当前宿主状态。</param>
        /// <param name="permitted">执行开关与受信任 C# 是否同时允许。</param>
        /// <param name="context">按执行期许可创建自动化上下文。</param>
        /// <param name="budget">程序集加载预算。</param>
        /// <param name="targets">命令声明支持的目标。</param>
        /// <param name="trustedSetting">诊断里展示的受信任设置名。</param>
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

        /// <summary>返回 script_run 与 script_status 两个操作。</summary>
        /// <returns>注册进 RoslynKit 的操作。</returns>
        public IRoslynOperation[] CreateOperations()
        {
            return new IRoslynOperation[]
            {
                new Operation(this, "script_run", YokiFrameCommandKind.Dangerous),
                new Operation(this, "script_status", YokiFrameCommandKind.ReadOnly)
            };
        }

        /// <summary>按动作分发。script_status 只读预算，script_run 进入提交。</summary>
        /// <param name="action">操作名。</param>
        /// <param name="request">原始命令。</param>
        /// <returns>命令结果。</returns>
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

        /// <summary>
        /// 提交一段受信任 C#。开关、编译器、确认标记和会话状态都通过后才进入调度器。
        /// </summary>
        /// <param name="request">script_run 请求。</param>
        /// <returns>已接受的 runId，或稳定错误码。</returns>
        private YokiFrameCommandResult Submit(YokiFrameCommandRequest request)
        {
            YokiFrameCommandResult blocked = RejectIfScriptBlocked();
            if (blocked != null) return blocked;
            try
            {
                using (JsonDocument document = JsonDocument.Parse(request.PayloadJson))
                {
                    if (!TryReadScriptRequest(document.RootElement, out ScriptRequest parsed, out blocked))
                        return blocked;
                    return SubmitParsed(request, parsed);
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

        /// <summary>执行开关或编译器包不满足时返回错误，否则返回 null。</summary>
        /// <returns>需要直接返回给调用方的错误；可以继续时返回 null。</returns>
        private YokiFrameCommandResult RejectIfScriptBlocked()
        {
            if (!mPermitted())
                return YokiFrameCommandResult.Error("ScriptExecutionNotPermitted",
                    "Enable RoslynKit operations and explicitly authorize " + mTrustedSetting + ".");
            if (!mCompiler.Installed)
                return YokiFrameCommandResult.Error("ScriptCompilerUnavailable",
                    "The project-local Roslyn bundle is missing. No fallback was attempted.");
            return null;
        }

        /// <summary>
        /// 读取 confirmed、code、target 和可选 timeoutMs。confirmed 必须是 JSON true。
        /// </summary>
        /// <param name="root">payload 根对象。</param>
        /// <param name="parsed">成功时的请求字段。</param>
        /// <param name="error">失败时的命令结果。</param>
        /// <returns>字段合法时返回 true。</returns>
        private static bool TryReadScriptRequest(JsonElement root, out ScriptRequest parsed, out YokiFrameCommandResult error)
        {
            parsed = null;
            error = null;
            if (!root.TryGetProperty("confirmed", out var confirmed) || confirmed.ValueKind != JsonValueKind.True)
            {
                error = YokiFrameCommandResult.Error("ScriptConfirmationRequired", "Trusted C# requires confirmed:true.");
                return false;
            }

            if (!root.TryGetProperty("code", out var input) || input.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("target", out var requestedTarget) || requestedTarget.ValueKind != JsonValueKind.String)
            {
                error = YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, "code and target are required strings.");
                return false;
            }

            string body = input.GetString();
            if (string.IsNullOrWhiteSpace(body) || Encoding.UTF8.GetByteCount(body) > 128 * 1024)
            {
                error = YokiFrameCommandResult.Error("ScriptInputLimit", "C# body must be nonempty and at most 128 KiB.");
                return false;
            }

            if (!TryReadTimeout(root, out int timeoutMs, out error)) return false;
            parsed = new ScriptRequest(body, requestedTarget.GetString(), timeoutMs);
            return true;
        }

        /// <summary>读取 timeoutMs。缺省为 30000，存在时必须是 1 到 600000 的整数。</summary>
        /// <param name="root">payload 根对象。</param>
        /// <param name="timeoutMs">采用的超时。</param>
        /// <param name="error">非法时的命令结果。</param>
        /// <returns>超时可用时返回 true。</returns>
        private static bool TryReadTimeout(JsonElement root, out int timeoutMs, out YokiFrameCommandResult error)
        {
            timeoutMs = 30000;
            error = null;
            if (!root.TryGetProperty("timeoutMs", out var timeout)) return true;
            if (!timeout.TryGetInt64(out long ms) || ms < 1 || ms > 600000)
            {
                error = YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, "timeoutMs must be 1..600000.");
                return false;
            }

            timeoutMs = (int)ms;
            return true;
        }

        /// <summary>
        /// 确认目标会话空闲后提交工作。执行当下再次核对会话，避免等待期间切走播放状态。
        /// </summary>
        /// <param name="request">原始命令，提供 requestId 和来源。</param>
        /// <param name="parsed">已经校验的脚本字段。</param>
        /// <returns>接受结果。</returns>
        private YokiFrameCommandResult SubmitParsed(YokiFrameCommandRequest request, ScriptRequest parsed)
        {
            var state = mState();
            if (!state.SessionIdentityAvailable || state.IsCompiling || state.IsBusy || parsed.Target != state.ActiveTarget)
                return YokiFrameCommandResult.Error(RoslynErrorCodes.UNAVAILABLE,
                    "The requested target must be active, idle and have a published session identity.");
            string hash = RoslynEvalRequest.ComputeSha256(new RoslynJsonBuilder()
                .StartObject().Property("code", parsed.Body).Property("target", parsed.Target)
                .Property("timeoutMs", parsed.TimeoutMs).EndObject().ToString());
            RoslynRunRecord record = mScheduler.SubmitWork(parsed.Target, request.RequestId, request.Source,
                hash, parsed.TimeoutMs, () => CreateRunWork(parsed, state));
            return YokiFrameCommandResult.Success(new RoslynJsonBuilder().StartObject()
                .Property("operation", "script_run").Property("accepted", true)
                .Property("runId", record.RunId).Property("requestId", record.RequestId)
                .Property("state", record.State.ToString()).Property("observeAction", "run_result")
                .EndObject().ToString());
        }

        /// <summary>在调度器真正执行时创建工作。届时会话标识必须仍与提交时一致。</summary>
        /// <param name="parsed">脚本字段。</param>
        /// <param name="state">提交时看到的会话。</param>
        /// <returns>可运行的工作。</returns>
        private RoslynRunWork CreateRunWork(ScriptRequest parsed, RoslynDomainState state)
        {
            Func<bool> allowed = () =>
            {
                var current = mState();
                return mPermitted() && current.SessionId == state.SessionId
                    && current.Generation == state.Generation && current.ActiveTarget == parsed.Target
                    && !current.IsCompiling;
            };
            return new RoslynRunWork(parsed.Body, mCompiler.CaptureReferences(), mContext(allowed), mCompiler, mBudget);
        }

        /// <summary>script_run 中已经校验过的字段。</summary>
        private sealed class ScriptRequest
        {
            /// <summary>创建一项脚本请求。</summary>
            /// <param name="body">C# 正文。</param>
            /// <param name="target">单一执行目标。</param>
            /// <param name="timeoutMs">超时毫秒。</param>
            public ScriptRequest(string body, string target, int timeoutMs)
            {
                Body = body;
                Target = target;
                TimeoutMs = timeoutMs;
            }

            /// <summary>C# 正文。</summary>
            public string Body { get; }

            /// <summary>单一执行目标。</summary>
            public string Target { get; }

            /// <summary>超时毫秒。</summary>
            public int TimeoutMs { get; }
        }

        /// <summary>
        /// 收集当前已加载、可作为原型编译引用的程序集路径。动态程序集和编辑器程序集排除。
        /// </summary>
        /// <param name="referenceDirectory">位置为空时可选的同名 DLL 目录。</param>
        /// <returns>去重后的引用路径。</returns>
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
            /// <summary>把一个动作绑定到所属命令面。</summary>
            /// <param name="owner">命令面。</param>
            /// <param name="action">操作名。</param>
            /// <param name="kind">只读或危险。</param>
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
            /// <summary>转交所属命令面执行。</summary>
            /// <param name="request">原始命令。</param>
            /// <returns>命令结果。</returns>
            public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
            {
                return mOwner.Execute(Descriptor.Action, request);
            }
        }
    }
}
#endif
