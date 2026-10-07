using System;
using System.IO;
using System.Threading;
using Xunit;

namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// 跨进程认领（§10）：同一项目目录下多个宿主时，同一条 queued 运行只能被执行一次。
/// </summary>
public sealed class RunClaimTests : IDisposable
{
    private readonly string mRoot = Path.Combine(
        Path.GetTempPath(),
        "yokiframe-run-claim-tests",
        Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(mRoot))
        {
            Directory.Delete(mRoot, recursive: true);
        }
    }

    [Fact]
    public void Foreign_fresh_claim_blocks_the_local_scheduler()
    {
        RoslynRunScheduler scheduler = CreateScheduler();
        RoslynRunRecord run = Submit(scheduler, "req-claim-block");
        string claimPath = ClaimPath(run.RunId);

        // 模拟"另一个宿主刚刚认领了它"：租约未过期。
        File.WriteAllText(claimPath, "{\"ownerSessionId\":\"other\",\"generation\":1,\"expiresAtUtc\":\""
            + DateTime.UtcNow.AddMinutes(5).ToString("o") + "\"}");

        Tick(scheduler, 5);

        Assert.True(scheduler.TryReadRun(run.RunId, out RoslynRunRecord current));
        Assert.Equal(RunStatus.Queued, current.State);
        Assert.Equal(0, CountClaimSteps(current));
    }

    [Fact]
    public void Expired_claim_can_be_taken_over()
    {
        RoslynRunScheduler scheduler = CreateScheduler();
        RoslynRunRecord run = Submit(scheduler, "req-claim-expired");

        File.WriteAllText(ClaimPath(run.RunId), "{\"ownerSessionId\":\"stale\",\"generation\":1,\"expiresAtUtc\":\""
            + DateTime.UtcNow.AddMinutes(-5).ToString("o") + "\"}");

        Tick(scheduler, 60);

        Assert.True(scheduler.TryReadRun(run.RunId, out RoslynRunRecord current));
        Assert.NotEqual(RunStatus.Queued, current.State);
        Assert.Equal(1, CountClaimSteps(current));
    }

    [Fact]
    public void Claim_json_survives_hostile_owner_ids()
    {
        // owner 含引号/反斜杠时，认领文件仍必须是合法 JSON，否则会被当成"租约过期"而错误接管。
        RoslynRunScheduler scheduler = CreateScheduler();
        RoslynRunRecord run = Submit(scheduler, "req-claim-hostile");
        var store = new RoslynRunStore(mRoot);
        string hostileOwner = "owner\"with\\slash";

        Assert.True(store.TryClaim(run.RunId, hostileOwner, 1L, TimeSpan.FromMinutes(5), out _));

        // 第二个宿主必须被挡住（而不是把破损 JSON 当成过期后接管）
        var other = new RoslynRunStore(mRoot);
        Assert.False(other.TryClaim(run.RunId, "other", 2L, TimeSpan.FromMinutes(5), out _));
    }

    [Fact]
    public void Claim_file_is_released_once_the_run_is_terminal()
    {
        RoslynRunScheduler scheduler = CreateScheduler();
        RoslynRunRecord run = Submit(scheduler, "req-claim-release");
        Tick(scheduler, 60);
        Assert.True(scheduler.TryReadRun(run.RunId, out RoslynRunRecord done));
        Assert.True(done.IsTerminal);

        // 终态记录不再需要认领文件；下一次扫描会惰性清理。
        Thread.Sleep(300);
        Tick(scheduler, 3);

        Assert.False(File.Exists(ClaimPath(run.RunId)));
    }

    [Fact]
    public void A_second_host_never_re_executes_an_already_claimed_run()
    {
        var store = new RoslynRunStore(mRoot);
        var first = new RoslynRunScheduler(store, new TestRunHost());
        var second = new RoslynRunScheduler(store, new TestRunHost());

        RoslynRunRecord run = Submit(first, "req-claim-single");
        Tick(first, 60);
        Assert.True(first.TryReadRun(run.RunId, out RoslynRunRecord afterFirst));

        // 第二个宿主（模拟旧会话/另一个编辑器）随后也扫描：不得再次执行。
        Thread.Sleep(300);
        Tick(second, 10);

        Assert.True(second.TryReadRun(run.RunId, out RoslynRunRecord afterSecond));
        Assert.Equal(1, CountClaimSteps(afterSecond));
        Assert.Equal(1, afterSecond.Attempt);
        Assert.Equal(afterFirst.State, afterSecond.State);
    }

    private RoslynRunScheduler CreateScheduler()
    {
        return new RoslynRunScheduler(
            new RoslynRunStore(mRoot),
            new TestRunHost());
    }

    private RoslynRunRecord Submit(RoslynRunScheduler scheduler, string requestId)
    {
        RoslynRunRecord run = scheduler.SubmitWork("editor", requestId, "cli", "hash", 30000, () => new TestRunWork());
        Assert.NotNull(run);
        return run;
    }

    private static void Tick(RoslynRunScheduler scheduler, int times)
    {
        for (var index = 0; index < times; index++)
        {
            scheduler.Tick();
            Thread.Sleep(5);
        }
    }

    private static int CountClaimSteps(RoslynRunRecord record)
    {
        var count = 0;
        for (var index = 0; index < record.Steps.Count; index++)
        {
            if (string.Equals(record.Steps[index].Name, "claim", StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private string ClaimPath(string runId)
    {
        return Path.Combine(mRoot, ".yokiframe", "engine", "runs", runId + ".claim");
    }
}
