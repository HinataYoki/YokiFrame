using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

public sealed partial class LiveCodeTests
{
    private const string RepeatedScript = "test.Equal(1, 1, \"fresh assertion\"); engine.ConsoleLog(\"fresh log\");";

    [Fact]
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
    public void Reuse_still_enforces_permission_and_invalid_source_never_enters_cache()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            string[] references = bed.Compiler.CaptureReferences();
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
    public void Cached_script_can_run_after_load_budget_is_exhausted()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            string[] references = bed.Compiler.CaptureReferences();
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
