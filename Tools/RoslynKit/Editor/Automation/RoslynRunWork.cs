#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>One domain's load budget. Pruning run records does not refund loaded assemblies.</summary>
    public sealed class RoslynLoadBudget
    {
        /// <summary>Maximum script assemblies loadable per domain before a reload is required.</summary>
        public const int MaxAssemblies = 4096;
        /// <summary>Maximum cumulative PE+PDB bytes loadable per domain.</summary>
        public const long MaxLoadedBytes = 64L * 1024 * 1024;

        public int LoadedCount { get; private set; }
        public long LoadedBytes { get; private set; }
        public const int WarningRemainingAssemblies = 3;
        public const long WarningRemainingBytes = 1024 * 1024;
        public RoslynBudgetStatus ReadStatus()
        {
            bool count = MaxAssemblies - LoadedCount <= WarningRemainingAssemblies;
            bool bytes = MaxLoadedBytes - LoadedBytes <= WarningRemainingBytes;
            return new RoslynBudgetStatus
            {
                LoadedAssemblies = LoadedCount, LoadedBytes = LoadedBytes,
                BudgetWarning = count && bytes ? "assemblies-and-bytes-low" :
                    count ? "assemblies-low" : bytes ? "bytes-low" : ""
            };
        }
        public void EnsureAvailable(int count, long bytes)
        {
            if (count < 0 || bytes < 0) throw new ArgumentOutOfRangeException();
            if (count > MaxAssemblies - LoadedCount || bytes > MaxLoadedBytes - LoadedBytes)
                throw new InvalidOperationException("ScriptAssemblyBudgetExceeded: this domain's load budget is exhausted. "
                    + "Use RoslynKit/live_snapshot to persist supported behaviours without compiling; inspect complete/errors, "
                    + "then explicitly reload and Restore. Patches and arbitrary scene state are not restored. "
                    + "Read script_status for remaining counts/bytes; removal does not refund this budget.");
        }
        public void Reserve(long bytes)
        {
            EnsureAvailable(1, bytes);
            LoadedCount++;
            LoadedBytes += bytes;
        }
    }

    public sealed class RoslynRunWork : IRoslynRunWork
    {
        private readonly AutomationContext mContext;
        private readonly RoslynCompilerLoader mCompiler;
        private readonly RoslynLoadBudget mBudget;
        private string[] mReferences;
        private string mCode;

        public RoslynRunWork(string code, string[] references,
            AutomationContext context, RoslynCompilerLoader compiler, RoslynLoadBudget budget)
        {
            mCode = code;
            mReferences = references;
            mContext = context;
            mCompiler = compiler;
            mBudget = budget;
        }

        public bool IsCompiling { get; private set; }
        public int Frames { get { return mContext.Frames; } }
        public int StaleContextCalls { get { return mContext.StaleContextCalls; } }
        public void AdvanceFrame() { mContext.AdvanceFrame(); }
        public void Invalidate() { mContext.Invalidate(); }

        /// <summary>
        /// 编译并运行一次提交。编译失败提前返回，但 finally 仍复制断言和日志。
        /// </summary>
        /// <returns>运行结论。</returns>
        public async Task<RunResult> Start()
        {
            IsCompiling = true;
            var result = new RunResult();
            try
            {
                var script = await CompileOrLoad(result);
                if (script == null)
                {
                    result.Status = RunStatus.CompileFailed;
                    return result;
                }

                result.Note = script.Diagnostics;
                IsCompiling = false;
                mContext.Guard();
                await script.Run(mContext, mContext.Test, mContext.CancellationToken);
                mContext.CancellationToken.ThrowIfCancellationRequested();
                result.Status = mContext.Test.HasFailures ? RunStatus.Failed : RunStatus.Passed;
            }
            catch (OperationCanceledException)
            {
                result.Status = RunStatus.Cancelled;
            }
            catch (Exception exception)
            {
                ApplyException(result, exception);
            }
            finally
            {
                IsCompiling = false;
                mCode = null;
                mReferences = null;
                result.Assertions = new List<RunAssertion>(mContext.Test.Records);
                result.Logs = new List<string>(mContext.Logs);
            }

            return result;
        }

        /// <summary>命中缓存或在线程池编译后加载脚本。引用在编译期间变化时拒绝加载。</summary>
        /// <param name="result">用于写回编译诊断的结论。</param>
        /// <returns>可运行脚本；编译失败时返回 null。</returns>
        private async Task<RoslynCompilerLoader.CompiledScript> CompileOrLoad(RunResult result)
        {
            mContext.Guard();
            string source = Wrap(mCode);
            mCode = null;
            string key = mCompiler.ScriptKey(source, mReferences);
            if (mCompiler.TryGetScript(key, out var script))
            {
                mReferences = null;
                return script;
            }

            byte[] symbols = null;
            string[][] diagnostics = null;
            byte[] pe = await Task.Run(() => mCompiler.Compile(source, mReferences,
                mContext.CancellationToken, out symbols, out diagnostics), mContext.CancellationToken);
            mContext.Guard();
            result.Note = WriteDiagnostics(diagnostics);
            if (pe == null) return null;
            if (key != mCompiler.ScriptKey(source, mReferences))
                throw new InvalidOperationException("Script references changed while compiling; submit again after compilation is idle.");
            script = mCompiler.LoadScript(key, pe, symbols, WriteDiagnostics(diagnostics), mBudget);
            mReferences = null;
            return script;
        }

        /// <summary>把断言失败和其它异常映射成不同终态。</summary>
        /// <param name="result">运行结论。</param>
        /// <param name="exception">捕获的异常。</param>
        private static void ApplyException(RunResult result, Exception exception)
        {
            result.Status = exception is AutomationAssertionException
                ? RunStatus.Failed : RunStatus.Errored;
            result.ExceptionType = exception.GetType().FullName;
            result.ExceptionMessage = exception.Message;
            result.ExceptionStack = exception.StackTrace ?? string.Empty;
        }

        public static string Wrap(string body)
        {
            return "using System;\nusing System.Threading;\nusing System.Threading.Tasks;\nusing YokiFrame;\n"
                + "namespace YokiFrame.Automation { public static class Script {\n"
                + "#pragma warning disable CS1998\n"
                + "public static async Task Run(AutomationContext engine, "
                + "AutomationAssertions test, CancellationToken cancellationToken) {\n"
                + "#line 1 \"input.csx\"\n" + body
                + "\n#line default\n} } }\n";
        }

        private static string WriteDiagnostics(string[][] diagnostics)
        {
            var json = new RoslynJsonBuilder().StartObject().Name("diagnostics").StartArray();
            foreach (string[] diagnostic in diagnostics ?? Array.Empty<string[]>())
                json.StartObject().Property("code", diagnostic[0]).Property("severity", diagnostic[1])
                    .Property("message", diagnostic[2]).Property("path", diagnostic[3])
                    .Property("line", diagnostic[4]).Property("column", diagnostic[5]).EndObject();
            return json.EndArray().EndObject().ToString();
        }
    }
}
#endif
