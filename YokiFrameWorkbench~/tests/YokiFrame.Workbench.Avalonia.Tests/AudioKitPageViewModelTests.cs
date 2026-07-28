using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using YokiFrame.Tooling.Application.Models.AudioKit;
using YokiFrame.Tooling.Application.Packages;
using YokiFrame.Workbench.Avalonia.ViewModels;
using YokiFrame.Workbench.Avalonia.Views;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>覆盖 AudioKit 只读观察页面的状态、索引和 Headless 渲染。</summary>
public sealed class AudioKitPageViewModelTests
{
    /// <summary>验证等价宿主刷新保持 Bus 选择并更新活动 voice。</summary>
    [Fact]
    public void PeriodicRefreshPreservesSelectedBusChannel()
    {
        AudioKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(1L, 1));
        viewModel.SelectedBusChannel = viewModel.BusChannels[1];

        viewModel.ApplyPeriodicState(CreateState(2L, 2));

        Assert.Equal("Music", viewModel.SelectedBusChannel?.Name);
        Assert.Equal(2, viewModel.SelectedBusChannel?.Voices.Count);
    }

    /// <summary>验证页面契约只保留 Bus 观察与稳定索引，不包含 Runtime 控制绑定。</summary>
    [Fact]
    public void PageContractUsesReadonlyBusObserverAndIndexDrawer()
    {
        string xaml = WorkbenchContractTestFiles.ReadSource("Views", "Pages", "AudioKitPageView.axaml");
        string shell = WorkbenchContractTestFiles.ReadSource("Views", "WorkbenchShellView.axaml");
        string modules = WorkbenchContractTestFiles.ReadSource("Pages", "WorkbenchDefaultPageModules.cs");

        Assert.Contains("AudioKit 观察器", modules, StringComparison.Ordinal);
        Assert.DoesNotContain("AudioKit 混音台", modules, StringComparison.Ordinal);
        Assert.Contains("audiokit-bus-channels", xaml, StringComparison.Ordinal);
        Assert.Contains("AudioBusChannelViewModel", xaml, StringComparison.Ordinal);
        Assert.Contains("SelectedBusChannel", xaml, StringComparison.Ordinal);
        Assert.Contains("AudioVoiceRowTemplate", xaml, StringComparison.Ordinal);
        Assert.Contains("ProgressBar", xaml, StringComparison.Ordinal);
        Assert.Contains("AudioHistoryRowTemplate", xaml, StringComparison.Ordinal);
        Assert.Contains("播放历史", xaml, StringComparison.Ordinal);
        Assert.Contains("audiokit-index-drawer", xaml, StringComparison.Ordinal);
        Assert.Contains("扫描预览", xaml, StringComparison.Ordinal);
        Assert.Contains("生成索引", xaml, StringComparison.Ordinal);
        Assert.Contains("LostFocus=\"OnIndexSettingLostFocus\"", xaml, StringComparison.Ordinal);
        Assert.Contains(
            "ScanFolder, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "IndexOutputPath, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "IndexManifestPath, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "IndexNamespace, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "IndexClassName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "IndexStartId, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains("AudioKitPageViewModel", shell, StringComparison.Ordinal);
        Assert.Contains("ActiveWorkspacePage", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("StopVoiceCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("StopAllCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("StopBusCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyVolumeCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ToggleMuteCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ClearHistoryCommand", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("EventType", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Slider", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("停止全部", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Master 静音", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("清空历史", xaml, StringComparison.Ordinal);
    }

    /// <summary>验证 Bus 选择背景只作用于卡片，避免拉伸的列表模板产生越界高亮。</summary>
    [Fact]
    public void BusSelectionStylesOnlyTheCard()
    {
        string styles = WorkbenchContractTestFiles.ReadSource("Styles", "AudioKit.axaml");

        Assert.DoesNotContain(
            "ListBox.audiokit-bus-channels > ListBoxItem:selected /template/ ContentPresenter",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "ListBox.audiokit-bus-channels > ListBoxItem:selected Border.audiokit-bus-card",
            styles,
            StringComparison.Ordinal);
    }

    /// <summary>验证页面仅公开索引命令，不保留任何 Runtime 音频操作命令。</summary>
    [Fact]
    public void PageExposesOnlyIndexCommands()
    {
        string[] commandNames = typeof(AudioKitPageViewModel)
            .GetProperties()
            .Where(static property => property.PropertyType == typeof(AsyncRelayCommand))
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "GenerateIndexCommand", "ScanIndexCommand" }, commandNames);
    }

    /// <summary>验证观察卡片只按 Master 和 Bus 分组，不丢失 voice 与历史。</summary>
    [Fact]
    public void BusChannelsGroupVoicesAndHistoryByBus()
    {
        AudioKitPageViewModel viewModel = new();

        viewModel.ApplyPeriodicState(CreateState(1L, 2));

        Assert.Equal(6, viewModel.BusChannels.Count);
        Assert.True(viewModel.BusChannels[0].IsMaster);
        Assert.Equal(2, viewModel.BusChannels[0].Voices.Count);
        Assert.Single(viewModel.BusChannels[0].History);
        Assert.Equal("Music", viewModel.BusChannels[1].Name);
        Assert.Equal(2, viewModel.BusChannels[1].Voices.Count);
        Assert.Single(viewModel.BusChannels[1].History);
    }

    /// <summary>验证页面历史只投影可归属到 Bus 的播放记录，不展示 Runtime 控制诊断。</summary>
    [Fact]
    public void BusChannelsExcludeRuntimeControlHistory()
    {
        AudioKitPageViewModel viewModel = new();

        viewModel.ApplyPeriodicState(CreateState(1L, 1, includeControlHistory: true));

        AudioBusChannelViewModel music = viewModel.BusChannels.Single(channel => channel.Name == "Music");
        Assert.Single(music.History);
        Assert.Equal("play_started", music.History[0].EventType);
        Assert.DoesNotContain(music.History, entry => entry.EventType == "volume_changed");
    }

    /// <summary>验证压力遥测刷新复用同一 Bus 投影，避免重建未变化的可视项。</summary>
    [Fact]
    public void BusRefreshPreservesChannelIdentity()
    {
        AudioKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(1L, 2));
        AudioBusChannelViewModel music = viewModel.BusChannels.Single(channel => channel.Name == "Music");

        viewModel.ApplyPeriodicState(CreateState(2L, 3));

        AudioBusChannelViewModel refreshed = viewModel.BusChannels.Single(channel => channel.Name == "Music");
        Assert.Same(music, refreshed);
        Assert.Equal(3, refreshed.ActiveVoiceCount);
        Assert.Equal(3, refreshed.Voices.Count);
    }

    /// <summary>验证大量 Bus 的搜索、活跃过滤和截断覆盖率不会丢失观察事实。</summary>
    [Fact]
    public void BusFiltersLoadedBusesAndReportsCoverage()
    {
        AudioKitPageViewModel viewModel = new();
        viewModel.ApplyPeriodicState(CreateState(1L, 2, busTotal: 20, busesTruncated: true));

        Assert.Equal("已加载 6 / 共 20 条总线", viewModel.BusCoverageText);
        Assert.True(viewModel.HasBusCoverageWarning);
        viewModel.BusSearchText = "sfx";
        Assert.Contains(viewModel.BusChannels, static channel => channel.Name == "SFX");
        Assert.DoesNotContain(viewModel.BusChannels, static channel => channel.Name == "Music");
        viewModel.ShowActiveBusesOnly = true;
        Assert.DoesNotContain(viewModel.BusChannels, static channel => channel.Name == "SFX");
        viewModel.ShowActiveBusesOnly = false;
        viewModel.BusSearchText = string.Empty;
        viewModel.SelectedBusScope = "动态";
        Assert.Contains(viewModel.BusChannels, static channel => channel.Name == "RuntimeOnly");
        Assert.DoesNotContain(viewModel.BusChannels, static channel => channel.Name == "DialogueNpc");
    }

    /// <summary>验证新项目始终使用约定扫描目录和命名空间，不按现存子目录漂移。</summary>
    [Fact]
    public void ProjectRootUsesStableIndexDefaults()
    {
        string projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-audio-page", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Assets", "Art", "Audio", "Desktop"));
        try
        {
            AudioKitPageViewModel viewModel = new();
            viewModel.SetProjectRoot(projectRoot);

            Assert.Equal("Assets/Art/Audio", viewModel.ScanFolder);
            Assert.Equal("GameAudio", viewModel.IndexNamespace);
        }
        finally
        {
            Directory.Delete(projectRoot, true);
        }
    }

    /// <summary>验证项目切换时读取各自保存的索引配置。</summary>
    [Fact]
    public void ProjectRootLoadsIsolatedSettings()
    {
        Dictionary<string, AudioIndexSettings> settings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["A"] = AudioIndexSettings.CreateDefault() with { NamespaceName = "AudioA" },
            ["B"] = AudioIndexSettings.CreateDefault() with { NamespaceName = "AudioB" }
        };
        AudioKitPageViewModel viewModel = new(null, null, root => settings[root]);

        viewModel.SetProjectRoot("A");
        Assert.Equal("AudioA", viewModel.IndexNamespace);
        viewModel.SetProjectRoot("B");
        Assert.Equal("AudioB", viewModel.IndexNamespace);
        viewModel.SetProjectRoot("A");
        Assert.Equal("AudioA", viewModel.IndexNamespace);
    }

    /// <summary>验证索引字段失焦时自动提交当前页面全部字段。</summary>
    [Fact]
    public async Task IndexSettingsAutomaticallyPersistCurrentFields()
    {
        AudioIndexSettings? saved = null;
        string savedRoot = string.Empty;
        AudioKitPageViewModel viewModel = new(
            null,
            null,
            _ => AudioIndexSettings.CreateDefault(),
            (root, value, _) =>
            {
                savedRoot = root;
                saved = value;
                return Task.CompletedTask;
            });
        viewModel.SetProjectRoot("ProjectA");
        viewModel.ScanFolder = "Assets/Art/Audio/Desktop";
        viewModel.IndexNamespace = "ProjectAudio";

        await viewModel.SaveIndexSettingsAsync();

        Assert.Equal("ProjectA", savedRoot);
        Assert.Equal("Assets/Art/Audio/Desktop", saved?.ScanFolder);
        Assert.Equal("ProjectAudio", saved?.NamespaceName);
        Assert.Equal("配置已保存", viewModel.IndexStatusText);
    }

    /// <summary>验证关闭 Workbench 时即使索引输入仍保持焦点也会提交当前草稿。</summary>
    [Fact]
    public void IndexSettingsPersistWhenWorkbenchClosesWithFocusedDraft()
    {
        AudioIndexSettings? saved = null;
        AudioKitPageViewModel viewModel = new(
            null,
            null,
            _ => AudioIndexSettings.CreateDefault(),
            (_, value, _) =>
            {
                saved = value;
                return Task.CompletedTask;
            });
        viewModel.SetProjectRoot("ProjectA");
        viewModel.IndexClassName = "ProjectAudioIds";

        viewModel.PersistIndexSettingsOnClose();

        Assert.Equal("ProjectAudioIds", saved?.ClassName);
    }

    /// <summary>验证扫描预览在调用扫描服务前自动持久化当前配置。</summary>
    [Fact]
    public async Task ScanIndexCommandSavesSettingsBeforeScanning()
    {
        List<string> calls = new();
        AudioKitPageViewModel viewModel = new(
            (_, _) =>
            {
                calls.Add("scan");
                return Task.FromResult(new AudioIndexResult(
                    Array.Empty<AudioIndexEntry>(), string.Empty, string.Empty, false));
            },
            null,
            _ => AudioIndexSettings.CreateDefault(),
            (_, _, _) =>
            {
                calls.Add("save");
                return Task.CompletedTask;
            });
        viewModel.SetProjectRoot("ProjectA");

        await viewModel.ScanIndexCommand.ExecuteAsync();

        Assert.Equal(new[] { "save", "scan" }, calls);
    }

    /// <summary>验证扫描命令会把 Application 结果投影到预览列表和状态文本。</summary>
    [Fact]
    public async Task ScanIndexCommandPublishesPreviewEntries()
    {
        AudioIndexEntry entry = new(1001, "MUSIC_MENU", "Menu", "Assets/Audio/Menu.ogg", string.Empty);
        AudioKitPageViewModel viewModel = new(
            (_, _) => Task.FromResult(new AudioIndexResult(
                new[] { entry }, "Assets/Scripts/Generated/AudioIds.cs",
                "Assets/Settings/YokiFrame/audio-index.json", true)),
            null);
        viewModel.SetProjectRoot(Path.GetTempPath());

        await viewModel.ScanIndexCommand.ExecuteAsync();

        Assert.Same(entry, Assert.Single(viewModel.IndexEntries));
        Assert.Equal("已扫描 1 项", viewModel.IndexStatusText);
        Assert.Equal(string.Empty, viewModel.IndexEmptyText);
    }

    /// <summary>验证真实 Workbench Shell 在目标尺寸渲染完整的只读 Bus 观察区。</summary>
    [Fact]
    public async Task WorkbenchShellRendersObserverAtTargetSizes()
    {
        InstallerHeadlessTestApplication.EnsureInitialized();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            string packageRoot = Directory.GetParent(WorkbenchContractTestFiles.FindWorkbenchRoot())?.FullName
                ?? throw new DirectoryNotFoundException("无法定位 YokiFrame 包根。");
            YokiFramePackageMetadata metadata = YokiFramePackageMetadataReader.Read(packageRoot);
            WorkbenchShellViewModel viewModel = new(
                () => { }, _ => { }, (_, _) => Task.CompletedTask, metadata, _ => Task.CompletedTask);
            viewModel.AudioKitPage.ApplyPeriodicState(CreateState(
                1L, 2, busTotal: 20, busesTruncated: true));
            viewModel.SelectedPage = "AudioKit";
            Window window = new() { Content = new WorkbenchShellView(viewModel) };
            try
            {
                RenderAndSave(window, 1280, 820);
                Border toolbar = window.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Classes.Contains("audiokit-status-strip"));
                Assert.InRange(toolbar.Bounds.Height, 30D, 44D);
                Assert.DoesNotContain(
                    toolbar.GetVisualDescendants().OfType<Control>(),
                    static control => control.Classes.Contains("audiokit-command-button"));
                TabControl compactTabs = window.GetVisualDescendants().OfType<TabControl>()
                    .Single(tab => tab.Classes.Contains("audiokit-compact-detail"));
                Assert.True(compactTabs.IsVisible);
                Assert.True(compactTabs.Bounds.Width > 700D);
                RenderAndSave(window, 1700, 1060);
                Grid wideDetails = window.GetVisualDescendants().OfType<Grid>()
                    .Single(grid => grid.Classes.Contains("audiokit-wide-detail"));
                Assert.True(wideDetails.IsVisible);
                ToggleButton indexToggle = window.GetVisualDescendants().OfType<ToggleButton>()
                    .Single(toggle => toggle.Classes.Contains("audiokit-drawer-toggle"));
                indexToggle.IsChecked = true;
                Dispatcher.UIThread.RunJobs();
                RenderAndSave(window, 1280, 820, "audiokit-observer-index-expanded-1280x820");
                RenderAndSave(window, 1700, 1060, "audiokit-observer-index-expanded");
                Border indexPanel = window.GetVisualDescendants().OfType<Border>()
                    .Single(border => border.Classes.Contains("audiokit-index-drawer"));
                Assert.InRange(indexPanel.Bounds.Height, 180D, 300D);
                Border[] cards = window.GetVisualDescendants().OfType<Border>()
                    .Where(static border => border.Classes.Contains("audiokit-bus-card"))
                    .ToArray();
                Assert.InRange(cards.Length, 4, 7);
                Assert.All(cards, static card => Assert.True(card.Bounds.Width >= 140D));
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>以指定尺寸布局并保存 Headless 帧，拒绝空白或尺寸错误的视觉证据。</summary>
    private static void RenderAndSave(Window window, int width, int height, string? artifactName = null)
    {
        window.Width = width;
        window.Height = height;
        if (!window.IsVisible) window.Show();
        Dispatcher.UIThread.RunJobs();
        using WriteableBitmap? frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(width, frame.PixelSize.Width);
        Assert.Equal(height, frame.PixelSize.Height);
        string outputDirectory = Path.Combine(
            WorkbenchContractTestFiles.FindWorkbenchRoot(), ".artifacts", "screenshots", "workbench");
        Directory.CreateDirectory(outputDirectory);
        string fileName = artifactName ?? $"audiokit-observer-{width}x{height}";
        string outputPath = Path.Combine(outputDirectory, fileName + ".png");
        using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        frame.Save(stream);
        Assert.True(stream.Length > 4096, "AudioKit Workbench Headless 截图内容为空或异常小。");
    }

    /// <summary>创建包含 Bus、voice 和历史的强类型测试状态。</summary>
    private static WorkbenchAudioKitState CreateState(
        long version,
        int voiceCount,
        string engineId = "unity-editor",
        string sessionId = "audio-session",
        long generation = 9L,
        int? busTotal = null,
        bool busesTruncated = false,
        bool includeControlHistory = false)
    {
        WorkbenchAudioBus[] buses =
        {
            new("Master", 0.8f, 0.8f, false, true, voiceCount, true, true),
            new("Music", 0.6f, 0.48f, false, false, voiceCount, true, true),
            new("SFX", 0.85f, 0.68f, false, false, 0, true, true),
            new("Voice", 1f, 0f, true, false, 0, true, true),
            new("DialogueNpc", 1f, 1f, false, false, 0, false, true),
            new("RuntimeOnly", 1f, 1f, false, false, 0, false, false)
        };
        WorkbenchAudioVoice[] voices = Enumerable.Range(1, voiceCount).Select(index => new WorkbenchAudioVoice(
            4L, index, "Audio/Music/" + index, "Music", "UnityAudioSource", true, true,
            false, 0.6f, 1f, 120f, index, false, new WorkbenchAudioPosition(0, 0, 0),
            string.Empty, 1f, 500f, "Logarithmic")).ToArray();
        WorkbenchAudioHistoryEntry[] history = includeControlHistory
            ? new[]
            {
                new WorkbenchAudioHistoryEntry(4, "volume_changed", 0, 0, string.Empty, "Music", 0.4f,
                    "2026-07-17T08:00:01Z"),
                new WorkbenchAudioHistoryEntry(3, "play_started", 4, 1, "Audio/Music/1", "Music", 0.6f,
                    "2026-07-17T08:00:00Z")
            }
            : new[]
            {
                new WorkbenchAudioHistoryEntry(3, "play_started", 4, 1, "Audio/Music/1", "Music", 0.6f,
                    "2026-07-17T08:00:00Z")
            };
        object source = CreateInternalDataSource(
            engineId, sessionId, generation, "PlayMode", DateTimeOffset.UtcNow,
            "telemetry", string.Empty, Array.Empty<string>(), string.Empty, "{}");
        return CreateInternal<WorkbenchAudioKitState>(
            source, version, new WorkbenchAudioBackend("UnityAudioSource", 63, "All", "ResKit"),
            new WorkbenchAudioMaster(0.8f, 0.8f, false, voiceCount), buses, voices, history,
            busTotal ?? buses.Length, voiceCount, 3L, busesTruncated, false, false);
    }

    /// <summary>通过反射创建 Application 内部 AudioKit 数据源。</summary>
    private static object CreateInternalDataSource(params object[] arguments)
    {
        Type type = typeof(WorkbenchAudioKitState).Assembly.GetType(
            "YokiFrame.Tooling.Application.Models.AudioKit.WorkbenchAudioKitDataSource", true)!;
        return Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic, null, arguments, null)!;
    }

    /// <summary>调用 Application 模型的内部构造方法。</summary>
    private static T CreateInternal<T>(params object[] arguments)
    {
        return (T)Activator.CreateInstance(typeof(T),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic, null, arguments, null)!;
    }
}
