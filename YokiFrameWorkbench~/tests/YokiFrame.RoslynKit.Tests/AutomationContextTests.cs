using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

public sealed class AutomationContextTests
{
    [Fact]
    public void Editor_ticks_do_not_count_as_game_frames()
    {
        long frame = 10;
        var context = new AutomationContext(() => frame, () => true, _ => { });
        Task wait = context.WaitFrames(3);
        for (int i = 0; i < 10; i++) context.AdvanceFrame();
        Assert.False(wait.IsCompleted);
        frame = 13;
        context.AdvanceFrame();
        Assert.True(wait.IsCompletedSuccessfully);
    }

    [Fact]
    public void Cancellation_completes_wait_and_rejects_future_calls()
    {
        var context = new AutomationContext(() => 1, () => true, _ => { });
        Task wait = context.WaitFrames(5);
        context.Invalidate();
        Assert.True(wait.IsCanceled);
        Assert.Throws<OperationCanceledException>(() => context.ConsoleLog("late"));
        Assert.Equal(1, context.StaleContextCalls);
    }

    [Fact]
    public void Caught_assertion_does_not_erase_failure()
    {
        var context = new AutomationContext(() => 1, () => true, _ => { });
        Assert.Throws<AutomationAssertionException>(() => context.Test.Equal(1, 2, "mismatch"));
        context.Test.Equal(3, 3);
        Assert.True(context.Test.HasFailures);
    }

    [Fact]
    public async Task Game_wait_requires_playing_and_unknown_clock_is_rejected()
    {
        var context = new AutomationContext(() => -1, () => true, _ => { });
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.WaitFrames(1));
        await Assert.ThrowsAsync<ArgumentException>(() => context.WaitFrames(1, "milliseconds"));
    }

    [Fact]
    public void Missing_live_service_does_not_initialize_architecture()
    {
        Assert.False(ArchitectureRegistry.TryResolveLiveService(typeof(UnregisteredService), null,
            out _, out _));
    }

    [Fact]
    public void Trusted_setting_is_separate_from_general_engine_switch()
    {
        using var bed = RunTestBed.Create();
        Directory.CreateDirectory(bed.Root);
        string path = Path.Combine(bed.Root, "settings.json");
        File.WriteAllText(path, """{"settings":[{"kit":"RoslynKit","key":"operations.enabled","value":"true"}]}""");
        Assert.True(new RoslynJsonSettingsSource(path).Read().IsEnabled);
        Assert.True(new RoslynJsonSettingsSource(path, "scripts.trustedCSharp", "trustedCSharp")
            .Read().BlocksExecution);
    }

    [Fact]
    public void Load_budget_never_silently_reloads()
    {
        var budget = new RoslynLoadBudget();
        for (int i = 0; i < RoslynLoadBudget.MaxAssemblies; i++) budget.Reserve(100);
        Assert.Throws<InvalidOperationException>(() => budget.Reserve(1));
        Assert.Equal(RoslynLoadBudget.MaxAssemblies, budget.LoadedCount);
    }

    [Fact]
    public void Single_frame_wait_requires_a_subsequent_completed_update_not_a_started_frame()
    {
        long started = 10, completed = 9;
        var context = new AutomationContext(() => started, () => true, _ => { },
            completedGameFrame: () => completed);
        Task wait = context.WaitFrames(1);
        completed = 10;
        context.AdvanceFrame();
        Assert.False(wait.IsCompleted);
        started = 11;
        for (int i = 0; i < 12; i++) context.AdvanceFrame();
        Assert.False(wait.IsCompleted);
        completed = 11;
        context.AdvanceFrame();
        Assert.True(wait.IsCompletedSuccessfully);
        wait = context.WaitFrames(1);
        context.AdvanceFrame();
        Assert.False(wait.IsCompleted);
        started = completed = 12;
        context.AdvanceFrame();
        Assert.True(wait.IsCompletedSuccessfully);
    }

    [Fact]
    public void Numbered_capture_preserves_existing_evidence_and_returns_available_path()
    {
        using var bed = RunTestBed.Create();
        const string relative = ".yokiframe/automation/evidence/repeat.png";
        string first = AutomationPaths.EvidencePng(bed.Root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        File.WriteAllText(first, "previous");
        Assert.Contains("autoNumber", Assert.Throws<IOException>(
            () => AutomationPaths.EvidencePng(bed.Root, relative)).Message);
        string next = AutomationPaths.EvidencePng(bed.Root, relative, true);
        Assert.EndsWith("repeat-001.png", next);
        File.WriteAllText(next, "other");
        Assert.EndsWith("repeat-002.png", AutomationPaths.EvidencePng(bed.Root, relative, true));
        Assert.Equal("previous", File.ReadAllText(first));
        Assert.Throws<IOException>(() => AutomationPaths.EvidencePng(bed.Root, "../outside.png", true));
    }

    [Fact]
    public async Task Capture_passes_numbering_to_host_and_returns_actual_artifact_path()
    {
        bool observed = false;
        var context = new AutomationContext(() => 0, () => true, _ => { },
            (mode, path, numbered, token) =>
            {
                observed = numbered;
                return Task.FromResult("actual-001.png");
            });
        Assert.Equal("actual-001.png", await context.Capture("game", "actual.png", autoNumber: true));
        Assert.True(observed);
    }

    [Fact]
    public void Byte_budget_and_negative_reservation_cannot_corrupt_counters()
    {
        var budget = new RoslynLoadBudget();
        Assert.Throws<ArgumentOutOfRangeException>(() => budget.Reserve(-1));
        budget.Reserve(RoslynLoadBudget.MaxLoadedBytes);
        Assert.Throws<InvalidOperationException>(() => budget.Reserve(1));
        Assert.Equal(1, budget.LoadedCount);
        Assert.Equal(RoslynLoadBudget.MaxLoadedBytes, budget.LoadedBytes);
    }

    [Fact]
    public void Evidence_cannot_escape_controlled_directory()
    {
        using var bed = RunTestBed.Create();
        Assert.Throws<IOException>(() => AutomationPaths.EvidencePng(bed.Root, "../outside.png"));
        Assert.Throws<IOException>(() => AutomationPaths.EvidencePng(bed.Root, "Assets/file.png"));
        string path = AutomationPaths.EvidencePng(bed.Root, ".yokiframe/automation/evidence/test.png");
        Assert.EndsWith("test.png", path);
    }

    [Fact]
    public void Permission_revocation_cancels_waits()
    {
        bool permitted = true;
        var context = new AutomationContext(() => 1, () => permitted, _ => { });
        Task wait = context.WaitFrames(10, "editorTick");
        permitted = false;
        context.AdvanceFrame();
        Assert.True(wait.IsCanceled);
        Assert.Throws<OperationCanceledException>(() => context.ConsoleLog("revoked"));
    }

    [Fact]
    public void Service_lookup_preserves_identity_rejects_ambiguity_and_tracks_disposal()
    {
        var first = FirstArchitecture.Interface;
        var second = SecondArchitecture.Interface;
        try
        {
            Assert.False(ArchitectureRegistry.TryResolveLiveService(typeof(LiveService), null, out _, out _));
            var context = new AutomationContext(() => 1, () => true, _ => { });
            Assert.Same(first.GetService<LiveService>(),
                context.RequireService<LiveService>(typeof(FirstArchitecture).FullName));
            first.Dispose();
            Assert.Same(second.GetService<LiveService>(), context.RequireService<LiveService>());
            second.Dispose();
            Assert.False(ArchitectureRegistry.TryResolveLiveService(typeof(LiveService), null, out _, out _));
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }
    }

    public sealed class LiveService : AbstractService { protected override void OnInit() { } }
    public sealed class FirstArchitecture : Architecture<FirstArchitecture>
    {
        protected override void OnInit() { Register(new LiveService()); }
    }
    public sealed class SecondArchitecture : Architecture<SecondArchitecture>
    {
        protected override void OnInit() { Register(new LiveService()); }
    }

    private sealed class UnregisteredService : AbstractService { protected override void OnInit() { } }
}
