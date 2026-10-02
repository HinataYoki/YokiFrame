using YokiFrame;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>验证三套 FileBridge 宿主共用的轮询时钟不会因帧异常提前或跳过周期。</summary>
public sealed class FileBridgePumpScheduleTests
{
    /// <summary>未满间隔时不触发任何阶段，满间隔后按心跳、清理、命令各自到期。</summary>
    [Fact]
    public void AdvanceFiresEachPhaseOnItsOwnInterval()
    {
        YokiFrameFileBridgePumpSchedule schedule = new YokiFrameFileBridgePumpSchedule();

        YokiFrameFileBridgePumpSchedule.Due early = schedule.Advance(0.1d);
        Assert.False(early.RefreshHeartbeat);
        Assert.False(early.PruneStorage);
        Assert.False(early.PollCommands);

        YokiFrameFileBridgePumpSchedule.Due commandDue = schedule.Advance(0.1d);
        Assert.True(commandDue.PollCommands);
        Assert.False(commandDue.RefreshHeartbeat);

        YokiFrameFileBridgePumpSchedule.Due heartbeatDue = schedule.Advance(
            YokiFrameFileBridgePumpSchedule.HEARTBEAT_INTERVAL_SECONDS);
        Assert.True(heartbeatDue.RefreshHeartbeat);
        Assert.True(heartbeatDue.PollCommands);
    }

    /// <summary>非法帧间隔不得被当成超长周期，否则一次 NaN 会连续触发清理和心跳。</summary>
    [Fact]
    public void AdvanceIgnoresNonFiniteDelta()
    {
        YokiFrameFileBridgePumpSchedule schedule = new YokiFrameFileBridgePumpSchedule();

        YokiFrameFileBridgePumpSchedule.Due due = schedule.Advance(double.NaN);

        Assert.False(due.RefreshHeartbeat);
        Assert.False(due.PruneStorage);
        Assert.False(due.PollCommands);
    }
}
