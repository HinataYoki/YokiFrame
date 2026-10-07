using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

public sealed partial class LiveCodeTests
{
    private const string RepeatedScript = "test.Equal(1, 1, \"fresh assertion\"); engine.ConsoleLog(\"fresh log\");";

    [Fact]
    /// <summary>同一脚本四次运行只加载一个程序集，但每次断言和日志都是新的。缓存命中三次。</summary>
    public void Repeated_script_reuses_one_assembly_but_runs_with_fresh_contexts()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            string[] references = bed.Compiler.CaptureReferences();
            for (int i = 0; i < 4; i++)
            {
                var context = new AutomationContext(() => 0, () => true, _ => { });
                var work = new RoslynRunWork(RepeatedScript, references, context, bed.Compiler, bed.Budget);
                var result = await work.Start();
                Assert.Equal(RunStatus.Passed, result.Status);
                Assert.Single(result.Assertions);
                Assert.Single(result.Logs);
                context.Invalidate();
            }
            Assert.Equal(1, bed.Budget.LoadedCount);
            Assert.Equal(1, bed.Compiler.CachedScripts);
            Assert.Equal(3, bed.Compiler.ScriptCacheHits);
            Assert.Equal(1, bed.Compiler.ScriptCacheMisses);
        });
    }

    [Fact]
    /// <summary>两个并发的首次运行只加载一次，并且都通过。上下文在结束后失效。</summary>
    public void Concurrent_identical_first_runs_load_only_once()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            string[] references = bed.Compiler.CaptureReferences();
            var first = new AutomationContext(() => 0, () => true, _ => { });
            var second = new AutomationContext(() => 0, () => true, _ => { });
            var one = new RoslynRunWork(RepeatedScript, references, first, bed.Compiler, bed.Budget).Start();
            var two = new RoslynRunWork(RepeatedScript, references, second, bed.Compiler, bed.Budget).Start();
            var results = await Task.WhenAll(one, two);
            Assert.All(results, result => Assert.Equal(RunStatus.Passed, result.Status));
            Assert.Equal(1, bed.Budget.LoadedCount);
            Assert.Equal(1, bed.Compiler.CachedScripts);
            first.Invalidate();
            second.Invalidate();
        });
    }

    [Fact]
    /// <summary>复用仍检查许可；非法源码不进缓存。另一段合法源码会再加载一次。</summary>
    public void Reuse_still_enforces_permission_and_invalid_source_never_enters_cache()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            string[] references = bed.Compiler.CaptureReferences();
            /// <summary>用给定许可运行一段源码，结束后使上下文失效。不保留上下文。</summary>
            /// <param name="source">脚本源码。</param>
            /// <param name="allowed">自动化上下文是否允许执行。</param>
            /// <returns>这一次运行的结果。</returns>
            async Task<RunResult> Run(string source, bool allowed)
            {
                var context = new AutomationContext(() => 0, () => allowed, _ => { });
                try { return await new RoslynRunWork(source, references, context, bed.Compiler, bed.Budget).Start(); }
                finally { context.Invalidate(); }
            }
            Assert.Equal(RunStatus.Passed, (await Run(RepeatedScript, true)).Status);
            Assert.Equal(RunStatus.Cancelled, (await Run(RepeatedScript, false)).Status);
            Assert.Equal(RunStatus.CompileFailed, (await Run("no_such_method();", true)).Status);
            Assert.Equal(1, bed.Budget.LoadedCount);
            Assert.Equal(1, bed.Compiler.CachedScripts);
            Assert.Equal(RunStatus.Passed, (await Run("test.Equal(2, 2);", true)).Status);
            Assert.Equal(2, bed.Budget.LoadedCount);
        });
    }

    [Fact]
    /// <summary>引用文件时间变化会使脚本缓存失效并重新加载。之后未变化的运行不再加载。</summary>
    public void Reference_version_change_invalidates_script_reuse()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            string reference = Path.Combine(bed.Root, "reference.dll");
            File.Copy(typeof(LiveCodeManager).Assembly.Location, reference);
            var references = bed.Compiler.CaptureReferences();
            for (int i = 0; i < references.Length; i++)
                if (references[i] == typeof(LiveCodeManager).Assembly.Location) references[i] = reference;
            /// <summary>用当前引用运行重复脚本并断言通过。结束时使上下文失效。</summary>
            async Task Run()
            {
                var context = new AutomationContext(() => 0, () => true, _ => { });
                try
                {
                    var result = await new RoslynRunWork(RepeatedScript, references, context, bed.Compiler, bed.Budget).Start();
                    Assert.Equal(RunStatus.Passed, result.Status);
                }
                finally { context.Invalidate(); }
            }
            await Run();
            File.SetLastWriteTimeUtc(reference, File.GetLastWriteTimeUtc(reference).AddSeconds(10));
            await Run();
            Assert.Equal(2, bed.Budget.LoadedCount);
            await Run();
            Assert.Equal(2, bed.Budget.LoadedCount);
        });
    }

    [Fact]
    /// <summary>预算耗尽后已缓存脚本仍能运行，新脚本被拒绝且不增加加载计数。</summary>
    public void Cached_script_can_run_after_load_budget_is_exhausted()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            string[] references = bed.Compiler.CaptureReferences();
            /// <summary>运行一段源码并在结束时使上下文失效。不修改预算。</summary>
            /// <param name="source">脚本源码。</param>
            /// <returns>这一次运行的结果。</returns>
            async Task<RunResult> Run(string source)
            {
                var context = new AutomationContext(() => 0, () => true, _ => { });
                try { return await new RoslynRunWork(source, references, context, bed.Compiler, bed.Budget).Start(); }
                finally { context.Invalidate(); }
            }
            Assert.Equal(RunStatus.Passed, (await Run(RepeatedScript)).Status);
            bed.Budget.Reserve(RoslynLoadBudget.MaxLoadedBytes - bed.Budget.LoadedBytes);
            int loaded = bed.Budget.LoadedCount;
            Assert.Equal(RunStatus.Passed, (await Run(RepeatedScript)).Status);
            var rejected = await Run("test.Equal(3, 3);");
            Assert.Equal(RunStatus.Errored, rejected.Status);
            Assert.Contains("ScriptAssemblyBudgetExceeded", rejected.ExceptionMessage);
            Assert.Equal(loaded, bed.Budget.LoadedCount);
            Assert.Equal(1, bed.Compiler.CachedScripts);
        });
    }

    [Fact]
    /// <summary>运行期失败不缓存结果或上下文。程序集仍只加载一次，后两次命中缓存。</summary>
    public void Runtime_failure_does_not_cache_run_results_or_contexts()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            string[] references = bed.Compiler.CaptureReferences();
            for (int i = 0; i < 3; i++)
            {
                var context = new AutomationContext(() => 0, () => true, _ => { });
                try
                {
                    var result = await new RoslynRunWork(RepeatedScript + "throw new InvalidOperationException(\"probe\");",
                        references, context, bed.Compiler, bed.Budget).Start();
                    Assert.Equal(RunStatus.Errored, result.Status);
                    Assert.Equal("probe", result.ExceptionMessage);
                    Assert.Single(result.Assertions);
                    Assert.Single(result.Logs);
                }
                finally { context.Invalidate(); }
            }
            Assert.Equal(1, bed.Budget.LoadedCount);
            Assert.Equal(2, bed.Compiler.ScriptCacheHits);
        });
    }
}
