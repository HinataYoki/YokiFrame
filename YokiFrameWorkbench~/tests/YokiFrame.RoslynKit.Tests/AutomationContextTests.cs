using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

public sealed class AutomationContextTests
{
    [Fact]
    /// <summary>编辑器推进不计入游戏帧。只有游戏帧前进到目标后等待才成功。</summary>
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
    /// <summary>失效会取消等待，并拒绝之后的调用。过期调用计数加一。</summary>
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
    /// <summary>被捕获的断言失败仍然保留。后续成功断言不会清除失败标记。</summary>
    public void Caught_assertion_does_not_erase_failure()
    {
        var context = new AutomationContext(() => 1, () => true, _ => { });
        Assert.Throws<AutomationAssertionException>(() => context.Test.Equal(1, 2, "mismatch"));
        context.Test.Equal(3, 3);
        Assert.True(context.Test.HasFailures);
    }

    [Fact]
    /// <summary>未知游戏时钟拒绝按帧等待；未知单位抛出 ArgumentException。不启动等待。</summary>
    public async Task Game_wait_requires_playing_and_unknown_clock_is_rejected()
    {
        var context = new AutomationContext(() => -1, () => true, _ => { });
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.WaitFrames(1));
        await Assert.ThrowsAsync<ArgumentException>(() => context.WaitFrames(1, "milliseconds"));
    }

    [Fact]
    /// <summary>未注册服务解析失败，并且不因此初始化架构。</summary>
    public void Missing_live_service_does_not_initialize_architecture()
    {
        Assert.False(ArchitectureRegistry.TryResolveLiveService(typeof(UnregisteredService), null,
            out _, out _));
    }

    [Fact]
    /// <summary>总开关开启不等于可信 C# 开启。专用键缺失时仍阻断执行。</summary>
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
    /// <summary>达到程序集上限后再预留会抛异常。已加载数量保持上限，不会静默重载。</summary>
    public void Load_budget_never_silently_reloads()
    {
        var budget = new RoslynLoadBudget();
        for (int i = 0; i < RoslynLoadBudget.MaxAssemblies; i++) budget.Reserve(100);
        Assert.Throws<InvalidOperationException>(() => budget.Reserve(1));
        Assert.Equal(RoslynLoadBudget.MaxAssemblies, budget.LoadedCount);
    }

    [Fact]
    /// <summary>等待一帧要求后续完成的更新，而不是已经开始的帧。完成后下一次等待重新计数。</summary>
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
    /// <summary>编号捕获不覆盖已有证据，并返回下一个可用路径。越界路径仍抛出 IOException。</summary>
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
    /// <summary>捕获把自动编号传给宿主，并返回宿主给出的实际路径。</summary>
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
    /// <summary>负数预留被拒绝；字节上限后再预留失败。计数保持一次满额加载。</summary>
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
    /// <summary>证据路径不能逃出受控目录，也不能写到 Assets。合法相对路径以文件名结尾。</summary>
    public void Evidence_cannot_escape_controlled_directory()
    {
        using var bed = RunTestBed.Create();
        Assert.Throws<IOException>(() => AutomationPaths.EvidencePng(bed.Root, "../outside.png"));
        Assert.Throws<IOException>(() => AutomationPaths.EvidencePng(bed.Root, "Assets/file.png"));
        string path = AutomationPaths.EvidencePng(bed.Root, ".yokiframe/automation/evidence/test.png");
        Assert.EndsWith("test.png", path);
    }

    [Fact]
    /// <summary>撤销许可会取消编辑器帧等待，并拒绝之后的日志调用。</summary>
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
    /// <summary>服务查找保持实例身份；释放后切换到另一个架构，全部释放后不再解析。finally 再次释放。</summary>
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

    public sealed class LiveService : AbstractService
    {
        /// <summary>测试服务不注册额外依赖。初始化保持为空。</summary>
        protected override void OnInit() { }
    }
    public sealed class FirstArchitecture : Architecture<FirstArchitecture>
    {
        /// <summary>初始化时注册一个 LiveService。不注册其他服务。</summary>
        protected override void OnInit() { Register(new LiveService()); }
    }
    public sealed class SecondArchitecture : Architecture<SecondArchitecture>
    {
        /// <summary>初始化时注册另一个 LiveService。用于释放后的回退查找。</summary>
        protected override void OnInit() { Register(new LiveService()); }
    }

    private sealed class UnregisteredService : AbstractService
    {
        /// <summary>未注册服务的初始化保持为空，避免测试期间产生副作用。</summary>
        protected override void OnInit() { }
    }
}
