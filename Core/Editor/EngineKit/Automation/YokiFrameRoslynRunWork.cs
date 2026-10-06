#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>One domain's load budget. Pruning run records does not refund loaded assemblies.</summary>
    public sealed class YokiFrameRoslynLoadBudget
    {
        /// <summary>Maximum script assemblies loadable per domain before a reload is required.</summary>
        public const int MaxAssemblies = 4096;
        /// <summary>Maximum cumulative PE+PDB bytes loadable per domain.</summary>
        public const long MaxLoadedBytes = 64L * 1024 * 1024;

        public int LoadedCount { get; private set; }
        public long LoadedBytes { get; private set; }
        public const int WarningRemainingAssemblies = 3;
        public const long WarningRemainingBytes = 1024 * 1024;
        public YokiFrameRoslynBudgetStatus ReadStatus()
        {
            bool count = MaxAssemblies - LoadedCount <= WarningRemainingAssemblies;
            bool bytes = MaxLoadedBytes - LoadedBytes <= WarningRemainingBytes;
            return new YokiFrameRoslynBudgetStatus
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
                    + "Use Engine/live_snapshot to persist supported behaviours without compiling; inspect complete/errors, "
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

    public sealed class YokiFrameRoslynRunWork : IYokiFrameEngineRunWork
    {
        private readonly YokiFrameAutomationContext mContext;
        private readonly YokiFrameRoslynCompilerLoader mCompiler;
        private readonly YokiFrameRoslynLoadBudget mBudget;
        private readonly string[] mReferences;
        private string mCode;

        public YokiFrameRoslynRunWork(string code, string[] references,
            YokiFrameAutomationContext context, YokiFrameRoslynCompilerLoader compiler, YokiFrameRoslynLoadBudget budget)
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

        public async Task<YokiFrameRunResult> Start()
        {
            IsCompiling = true;
            var result = new YokiFrameRunResult();
            try
            {
                mContext.Guard();
                string source = Wrap(mCode);
                mCode = null;
                byte[] symbols = null;
                string[][] diagnostics = null;
                byte[] pe = await Task.Run(() => mCompiler.Compile(source, mReferences,
                    mContext.CancellationToken, out symbols, out diagnostics), mContext.CancellationToken);
                mContext.Guard();
                result.Note = WriteDiagnostics(diagnostics);
                if (pe == null)
                {
                    result.Status = YokiFrameRunStatus.CompileFailed;
                    return result;
                }
                mBudget.Reserve(pe.Length + symbols.Length);
                Assembly assembly = mCompiler.LoadAssembly(pe, symbols);
                MethodInfo method = assembly.GetType("YokiFrame.Automation.Script", true)
                    .GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
                var run = (Func<YokiFrameAutomationContext, YokiFrameAutomationAssertions, CancellationToken, Task>)
                    Delegate.CreateDelegate(typeof(Func<YokiFrameAutomationContext,
                        YokiFrameAutomationAssertions, CancellationToken, Task>), method);
                IsCompiling = false;
                await run(mContext, mContext.Test, mContext.CancellationToken);
                mContext.CancellationToken.ThrowIfCancellationRequested();
                result.Status = mContext.Test.HasFailures ? YokiFrameRunStatus.Failed : YokiFrameRunStatus.Passed;
            }
            catch (OperationCanceledException)
            {
                result.Status = YokiFrameRunStatus.Cancelled;
            }
            catch (Exception exception)
            {
                result.Status = exception is YokiFrameAutomationAssertionException
                    ? YokiFrameRunStatus.Failed : YokiFrameRunStatus.Errored;
                result.ExceptionType = exception.GetType().FullName;
                result.ExceptionMessage = exception.Message;
                result.ExceptionStack = exception.StackTrace ?? string.Empty;
            }
            finally
            {
                IsCompiling = false;
                result.Assertions = new List<YokiFrameRunAssertion>(mContext.Test.Records);
                result.Logs = new List<string>(mContext.Logs);
            }
            return result;
        }

        public static string Wrap(string body)
        {
            return "using System;\nusing System.Threading;\nusing System.Threading.Tasks;\nusing YokiFrame;\n"
                + "namespace YokiFrame.Automation { public static class Script {\n"
                + "#pragma warning disable CS1998\n"
                + "public static async Task Run(YokiFrameAutomationContext engine, "
                + "YokiFrameAutomationAssertions test, CancellationToken cancellationToken) {\n"
                + "#line 1 \"input.csx\"\n" + body
                + "\n#line default\n} } }\n";
        }

        private static string WriteDiagnostics(string[][] diagnostics)
        {
            var json = new YokiFrameEngineJsonBuilder().StartObject().Name("diagnostics").StartArray();
            foreach (string[] diagnostic in diagnostics ?? Array.Empty<string[]>())
                json.StartObject().Property("code", diagnostic[0]).Property("severity", diagnostic[1])
                    .Property("message", diagnostic[2]).Property("path", diagnostic[3])
                    .Property("line", diagnostic[4]).Property("column", diagnostic[5]).EndObject();
            return json.EndArray().EndObject().ToString();
        }
    }
}
#endif
