using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Workbench.Avalonia.Services;
using YokiFrame.Workbench.Avalonia.ViewModels;
using Xunit;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>验证 AudioKit 页面及 Bus 投影会随语言切换刷新用户可见动态文案。</summary>
public sealed class AudioKitI18nTests
{
    /// <summary>语言切换应刷新筛选选项和已记录的索引状态，而不改变筛选语义。</summary>
    /// <remarks>整个测试体必须在 UI 线程执行：SetCulture 只有在 UI 线程上才会同步触发
    /// CultureChanged，否则重投影被 Post 到无人泵的 Dispatcher 队列。</remarks>
    [Fact]
    public async Task AudioPage_ReprojectsScopeAndIndexStatus()
    {
        // 单独运行时也必须先初始化 Headless UI 线程，否则 Dispatcher 无人泵导致挂起。
        InstallerHeadlessTestApplication.EnsureInitialized();
        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            WorkbenchI18nService service = WorkbenchI18nService.Instance;
            service.SetCulture("zh-CN");
            AudioKitPageViewModel viewModel = new(
                (_, _) => Task.FromResult(new AudioIndexResult(
                    Array.Empty<AudioIndexEntry>(), string.Empty, string.Empty, false)),
                null);
            try
            {
                Assert.Equal("全部", viewModel.SelectedBusScope);
                Assert.Contains("内置", viewModel.BusScopeOptions);
                viewModel.SetProjectRoot("ProjectA");
                await viewModel.ScanIndexCommand.ExecuteAsync();
                Assert.Equal("已扫描 0 项", viewModel.IndexStatusText);
                Assert.Equal("未找到 wav、mp3、ogg、aiff、flac 或 m4a", viewModel.IndexEmptyText);

                viewModel.SelectedBusScope = "内置";
                service.SetCulture("en-US");

                Assert.Equal("Built-in", viewModel.SelectedBusScope);
                Assert.Equal(new[] { "All", "Built-in", "Registered", "Dynamic" }, viewModel.BusScopeOptions);
                Assert.Equal("Scanned 0 item(s)", viewModel.IndexStatusText);
                Assert.Equal("No wav, mp3, ogg, aiff, flac, or m4a files found", viewModel.IndexEmptyText);
            }
            finally
            {
                viewModel.Dispose();
                service.SetCulture("zh-CN");
            }
        });
    }

    /// <summary>Bus 来源副标题应由当前语言资源投影，Runtime Bus 名称保持原样。</summary>
    [Fact]
    public void BusChannel_ReprojectsLocalizedSourceSubtitle()
    {
        WorkbenchI18nService service = WorkbenchI18nService.Instance;
        service.SetCulture("zh-CN");
        WorkbenchAudioBus bus = new("Music", 1f, 1f, false, false, 0, true, true);
        AudioBusChannelViewModel channel = new(
            "bus:Music", "Music", "内置总线", bus, false, 0,
            Array.Empty<WorkbenchAudioVoice>(), Array.Empty<WorkbenchAudioHistoryEntry>());
        try
        {
            Assert.Equal("内置总线", channel.Subtitle);
            service.SetCulture("en-US");
            channel.RefreshLocalization();
            Assert.Equal("Built-in bus", channel.Subtitle);
            Assert.Equal("Music", channel.Name);
        }
        finally
        {
            service.SetCulture("zh-CN");
        }
    }
}
