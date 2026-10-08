using YokiFrame.Tooling.Application.Models.LogKit;
using YokiFrame.Workbench.Avalonia.ViewModels;

namespace YokiFrame.Workbench.Avalonia.Tests;

/// <summary>创建 LogKit Workbench 测试需要的强类型状态和内部页面组合边界。</summary>
internal static class LogKitContractTestData
{
    /// <summary>创建确定宿主身份、能力和历史的 LogKit 状态。</summary>
    internal static WorkbenchLogKitState CreateState(
        IReadOnlyList<WorkbenchLogKitHistoryEntry>? entries = null,
        WorkbenchLogKitSettings? settings = null,
        string engineId = "unity-editor",
        string sessionId = "log-session",
        long generation = 7L,
        string source = "telemetry",
        bool filePreview = true,
        long diagnosticVersion = 1L,
        long settingsVersion = 1L,
        string directory = "C:/Project/Logs")
    {
        var historyEntries = entries ?? Array.Empty<WorkbenchLogKitHistoryEntry>();
        WorkbenchLogKitDataSource dataSource = CreateDataSource(engineId, sessionId, generation, source);
        var stats = new WorkbenchLogKitStats(
            "UnityEngineLogger",
            true,
            true,
            settings?.MinimumLevel ?? "Debug",
            historyEntries.Count,
            0);
        var capabilities = new WorkbenchLogKitCapabilities(true, filePreview, true, true, true);
        var files = new WorkbenchLogKitFiles(
            directory,
            CreateFile("editor", "yoki_editor.log", directory),
            CreateFile("player", "yoki_player.log", directory));
        var history = new WorkbenchLogKitHistory(
            historyEntries,
            historyEntries.Count,
            historyEntries.Count,
            0,
            false);
        return new WorkbenchLogKitState(
            dataSource,
            1,
            diagnosticVersion,
            settingsVersion,
            settings ?? WorkbenchLogKitSettings.CreateDefault(),
            stats,
            capabilities,
            files,
            history);
    }

    /// <summary>创建一条具备可搜索上下文和可选异常的内存日志。</summary>
    internal static WorkbenchLogKitHistoryEntry CreateEntry(
        string level,
        string message,
        string timestamp,
        string context = "",
        string exceptionMessage = "")
    {
        return new WorkbenchLogKitHistoryEntry(
            level,
            message,
            context,
            string.IsNullOrWhiteSpace(exceptionMessage) ? string.Empty : "InvalidOperationException",
            exceptionMessage,
            string.IsNullOrWhiteSpace(exceptionMessage) ? string.Empty : "at Demo.Run()",
            timestamp);
    }

    /// <summary>创建可持久化项目设置及并发指纹。</summary>
    internal static WorkbenchLogKitProjectSettings CreateProjectSettings(
        WorkbenchLogKitSettings settings,
        string fingerprint = "fingerprint-1",
        string engineId = "unity-editor",
        bool canPersist = true)
    {
        return new WorkbenchLogKitProjectSettings(
            engineId,
            "Unity",
            canPersist,
            true,
            "C:/Project/Assets/Settings/Resources/YokiFrame/runtime-settings.json",
            fingerprint,
            settings,
            "项目配置已加载");
    }

    /// <summary>创建一次保存与 Runtime 应用结果。</summary>
    internal static WorkbenchLogKitSettingsSaveResult CreateSaveResult(
        WorkbenchLogKitProjectSettings projectSettings,
        WorkbenchLogKitState? state)
    {
        return new WorkbenchLogKitSettingsSaveResult(
            true,
            state != null,
            false,
            projectSettings,
            state!,
            string.Empty);
    }

    /// <summary>创建确定内容的文件尾部预览。</summary>
    internal static WorkbenchLogKitFilePreview CreatePreview(string kind, string content)
    {
        return new WorkbenchLogKitFilePreview(
            kind,
            "C:/Project/Logs/yoki_" + kind + ".log",
            "yoki_" + kind + ".log",
            true,
            (long)content.Length,
            "2026-07-15T08:00:00Z",
            1,
            false,
            content,
            string.Empty,
            "NamedPipe",
            new[] { "test://" + kind });
    }

    /// <summary>通过内部组合构造方法注入 Application 用例。</summary>
    internal static LogKitPageViewModel CreateViewModel(
        Func<string, WorkbenchLogKitProjectSettings>? load = null,
        Func<string, WorkbenchLogKitSettings, string, CancellationToken, Task<WorkbenchLogKitSettingsSaveResult>>? save = null,
        Func<string, CancellationToken, Task<WorkbenchLogKitState>>? clear = null,
        Func<string, string, CancellationToken, Task<WorkbenchLogKitFilePreview>>? read = null)
    {
        return new LogKitPageViewModel(load, save, clear, read);
    }

    /// <summary>通过页面内部生命周期入口切换激活状态。</summary>
    internal static void SetPageActive(LogKitPageViewModel viewModel, bool isActive)
    {
        viewModel.SetPageActive(isActive);
    }

    /// <summary>等待异步页面条件成立并提供明确超时。</summary>
    internal static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "等待 LogKit 页面异步状态超时。");
    }

    /// <summary>创建一个存在但内容为空的日志文件元数据。</summary>
    private static WorkbenchLogKitFileMetadata CreateFile(string kind, string fileName, string directory)
    {
        return new WorkbenchLogKitFileMetadata(
            kind,
            directory.TrimEnd('/', '\\') + "/" + fileName,
            fileName,
            true,
            128L,
            "2026-07-15T08:00:00Z");
    }

    /// <summary>通过已对测试程序集可见的内部记录构造数据源。</summary>
    /// <param name="engineId">宿主标识。</param>
    /// <param name="sessionId">会话标识。</param>
    /// <param name="generation">宿主代际。</param>
    /// <param name="source">数据来源。</param>
    /// <returns>可供状态构造使用的数据源。</returns>
    private static WorkbenchLogKitDataSource CreateDataSource(string engineId, string sessionId, long generation, string source)
    {
        return new WorkbenchLogKitDataSource(
            engineId,
            sessionId,
            generation,
            "PlayMode",
            DateTimeOffset.Parse("2026-07-15T08:00:00Z"),
            source,
            string.Empty,
            new[] { "Global/YokiFrame.LogKit" },
            string.Empty,
            "{}");
    }
}
