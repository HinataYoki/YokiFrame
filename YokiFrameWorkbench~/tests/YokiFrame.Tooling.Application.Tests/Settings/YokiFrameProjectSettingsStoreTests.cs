using System.Text.Json;
using YokiFrame.Tooling.Application.Services.Settings;

namespace YokiFrame.Tooling.Application.Tests.Settings;

/// <summary>验证统一项目配置 Store 的合并、冲突和跨宿主文件提交。</summary>
public sealed class YokiFrameProjectSettingsStoreTests
{
    /// <summary>验证不同 Store 实例并发更新同一 Runtime 文件时不会丢失对方 Kit。</summary>
    [Fact]
    public async Task ConcurrentKitUpdatesPreserveBothOwners()
    {
        using TestProject project = new();
        YokiFrameProjectSettingsStore first = new(project.Root);
        YokiFrameProjectSettingsStore second = new(project.Root);

        Task<YokiFrameProjectSettingsWriteResult> firstWrite = first.WriteAsync(
            YokiFrameProjectSettingsUpdate.MergeLatest(
                YokiFrameProjectSettingsPatch.ReplaceOwner(
                    YokiFrameProjectSettingsTarget.UnityRuntime,
                    "LogKit",
                    new YokiFrameProjectSettingValue("enabled", "true"))),
            CancellationToken.None);
        Task<YokiFrameProjectSettingsWriteResult> secondWrite = second.WriteAsync(
            YokiFrameProjectSettingsUpdate.MergeLatest(
                YokiFrameProjectSettingsPatch.ReplaceOwner(
                    YokiFrameProjectSettingsTarget.UnityRuntime,
                    "SaveKit",
                    new YokiFrameProjectSettingValue("fileExtension", ".yoki"))),
            CancellationToken.None);

        await Task.WhenAll(firstWrite, secondWrite);

        YokiFrameProjectSettingsDocument document = first.Read(
            YokiFrameProjectSettingsTarget.UnityRuntime)
            .GetDocument(YokiFrameProjectSettingsTarget.UnityRuntime);
        Assert.Contains(document.Settings, static setting =>
            setting.Owner == "LogKit" && setting.Key == "enabled" && setting.Value == "true");
        Assert.Contains(document.Settings, static setting =>
            setting.Owner == "SaveKit" && setting.Key == "fileExtension" && setting.Value == ".yoki");
    }

    /// <summary>验证页面旧 revision 不能覆盖外部已经提交的配置。</summary>
    [Fact]
    public async Task RevisionConflictDoesNotOverwriteExternalChange()
    {
        using TestProject project = new();
        YokiFrameProjectSettingsStore store = new(project.Root);
        YokiFrameProjectSettingsSnapshot loaded = store.Read(
            YokiFrameProjectSettingsTarget.UnityRuntime);

        await store.WriteAsync(
            YokiFrameProjectSettingsUpdate.MergeLatest(
                YokiFrameProjectSettingsPatch.ReplaceOwner(
                    YokiFrameProjectSettingsTarget.UnityRuntime,
                    "EventKit",
                    new YokiFrameProjectSettingValue("enabled", "true"))),
            CancellationToken.None);

        YokiFrameProjectSettingsWriteResult result = await store.WriteAsync(
            YokiFrameProjectSettingsUpdate.RequireRevision(
                loaded.Revision,
                YokiFrameProjectSettingsPatch.ReplaceOwner(
                    YokiFrameProjectSettingsTarget.UnityRuntime,
                    "LogKit",
                    new YokiFrameProjectSettingValue("enabled", "true"))),
            CancellationToken.None);

        Assert.False(result.Saved);
        Assert.True(result.ConflictDetected);
        YokiFrameProjectSettingsDocument document = store.Read(
            YokiFrameProjectSettingsTarget.UnityRuntime)
            .GetDocument(YokiFrameProjectSettingsTarget.UnityRuntime);
        Assert.Contains(document.Settings, static setting => setting.Owner == "EventKit");
        Assert.DoesNotContain(document.Settings, static setting => setting.Owner == "LogKit");
    }

    /// <summary>验证 Runtime 和 Editor 两个目标可由一个批次同时提交。</summary>
    [Fact]
    public async Task BatchUpdateCommitsRuntimeAndEditorDocuments()
    {
        using TestProject project = new();
        YokiFrameProjectSettingsStore store = new(project.Root);
        YokiFrameProjectSettingsSnapshot loaded = store.Read(
            YokiFrameProjectSettingsTarget.UnityRuntime,
            YokiFrameProjectSettingsTarget.UnityEditor);

        YokiFrameProjectSettingsWriteResult result = await store.WriteAsync(
            YokiFrameProjectSettingsUpdate.RequireRevision(
                loaded.Revision,
                YokiFrameProjectSettingsPatch.ReplaceOwner(
                    YokiFrameProjectSettingsTarget.UnityRuntime,
                    "LogKit",
                    new YokiFrameProjectSettingValue("enabled", "true")),
                YokiFrameProjectSettingsPatch.ReplaceOwner(
                    YokiFrameProjectSettingsTarget.UnityEditor,
                    "LogKit",
                    new YokiFrameProjectSettingValue("saveLogInEditor", "true"))),
            CancellationToken.None);

        Assert.True(result.Saved);
        Assert.True(File.Exists(store.GetPath(YokiFrameProjectSettingsTarget.UnityRuntime)));
        Assert.True(File.Exists(store.GetPath(YokiFrameProjectSettingsTarget.UnityEditor)));
        using JsonDocument runtime = JsonDocument.Parse(
            File.ReadAllText(store.GetPath(YokiFrameProjectSettingsTarget.UnityRuntime)));
        Assert.Contains(runtime.RootElement.GetProperty("settings").EnumerateArray(), static item =>
            item.GetProperty("kit").GetString() == "LogKit");
    }

    /// <summary>验证 Godot 只更新 YokiFrame section 并保留其它 ProjectSettings section。</summary>
    [Fact]
    public async Task GodotUpdatePreservesOtherProjectSettings()
    {
        using TestProject project = new();
        string path = Path.Combine(project.Root, "project.godot");
        File.WriteAllText(path, "[application]\nconfig/name=Demo\n\n[yokiframe/runtime]\nold/key=\"keep\"\n");
        YokiFrameProjectSettingsStore store = new(project.Root);

        await store.WriteAsync(
            YokiFrameProjectSettingsUpdate.MergeLatest(
                YokiFrameProjectSettingsPatch.ReplaceKeys(
                    YokiFrameProjectSettingsTarget.GodotRuntime,
                    "save_kit",
                    new[] { "storage_path" },
                    new YokiFrameProjectSettingValue("storage_path", "user://Saves"))),
            CancellationToken.None);

        string content = File.ReadAllText(path);
        Assert.Contains("[application]", content, StringComparison.Ordinal);
        Assert.Contains("config/name=Demo", content, StringComparison.Ordinal);
        Assert.Contains("old/key=\"keep\"", content, StringComparison.Ordinal);
        Assert.Contains("save_kit/storage_path=\"user://Saves\"", content, StringComparison.Ordinal);
    }

    /// <summary>验证未来 C# 引擎可以通过自定义目标和后端接入，不修改 Store 的目标分支。</summary>
    [Fact]
    public async Task CustomEngineBackendCanBeInjectedWithoutChangingStore()
    {
        using TestProject project = new();
        YokiFrameProjectSettingsTarget target = new("stride", YokiFrameProjectSettingsScope.Runtime);
        YokiFrameProjectSettingsStore store = new(
            project.Root,
            new IYokiFrameProjectSettingsBackend[] { new TestEngineSettingsBackend() });

        YokiFrameProjectSettingsWriteResult result = await store.WriteAsync(
            YokiFrameProjectSettingsUpdate.MergeLatest(
                YokiFrameProjectSettingsPatch.ReplaceOwner(
                    target,
                    "LogKit",
                    new YokiFrameProjectSettingValue("enabled", "true"))),
            CancellationToken.None);

        Assert.True(result.Saved);
        Assert.Equal(
            "LogKit/enabled=true",
            File.ReadAllText(store.GetPath(target)));
    }

    /// <summary>验证 Godot Editor Project/User 配置与随导出使用的 project.godot Runtime 文档分域。</summary>
    [Fact]
    public void GodotEditorTargetsUseDedicatedProjectAndUserDocuments()
    {
        using TestProject project = new();
        YokiFrameProjectSettingsStore store = new(project.Root);

        Assert.EndsWith("godot-editor-settings.json", store.GetPath(YokiFrameProjectSettingsTarget.GodotEditor));
        Assert.EndsWith("godot-user-settings.json", store.GetPath(YokiFrameProjectSettingsTarget.GodotEditorUser));
        Assert.EndsWith("project.godot", store.GetPath(YokiFrameProjectSettingsTarget.GodotRuntime));
    }

    /// <summary>验证等待跨进程 Mutex 时取消不会泄漏项目内锁，后续同项目读取仍能完成。</summary>
    [Fact]
    public async Task CancelledMutexWaitReleasesProjectLock()
    {
        using TestProject project = new();
        YokiFrameProjectSettingsStore store = new(project.Root);
        using ManualResetEventSlim mutexAcquired = new(false);
        using ManualResetEventSlim releaseMutex = new(false);
        Thread holder = StartMutexHolder(project.Root, mutexAcquired, releaseMutex);
        Assert.True(mutexAcquired.Wait(TimeSpan.FromSeconds(2)));
        try
        {
            using CancellationTokenSource cancellationSource = new(TimeSpan.FromMilliseconds(500));
            Task<YokiFrameProjectSettingsWriteResult> writeTask = store.WriteAsync(
                YokiFrameProjectSettingsUpdate.MergeLatest(
                    YokiFrameProjectSettingsPatch.ReplaceOwner(
                        YokiFrameProjectSettingsTarget.UnityRuntime,
                        "LogKit",
                        new YokiFrameProjectSettingValue("enabled", "true"))),
                cancellationSource.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writeTask);
        }
        finally
        {
            releaseMutex.Set();
            holder.Join(TimeSpan.FromSeconds(2));
        }

        var snapshot = await Task.Run(() => store.Read(YokiFrameProjectSettingsTarget.UnityRuntime))
            .WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotNull(snapshot);
    }

    /// <summary>在专用线程持有项目 Mutex，确保测试不会跨 await 在线程池线程释放 Mutex。</summary>
    /// <param name="projectRoot">测试项目根。</param>
    /// <param name="acquired">Mutex 已取得信号。</param>
    /// <param name="release">允许释放 Mutex 的信号。</param>
    /// <returns>已经启动的持锁线程。</returns>
    private static Thread StartMutexHolder(
        string projectRoot,
        ManualResetEventSlim acquired,
        ManualResetEventSlim release)
    {
        Thread holder = new(() =>
        {
            using Mutex mutex = new(false, YokiFrameProjectSettingsStore.CreateMutexName(projectRoot));
            mutex.WaitOne();
            acquired.Set();
            release.Wait();
            mutex.ReleaseMutex();
        })
        {
            IsBackground = true
        };
        holder.Start();
        return holder;
    }

    /// <summary>提供测试用 Stride 文本后端，证明 Store 不依赖内置引擎枚举。</summary>
    private sealed class TestEngineSettingsBackend : IYokiFrameProjectSettingsBackend
    {
        /// <summary>获取测试引擎标识。</summary>
        public string EngineId => "stride";

        /// <summary>仅接管 Stride Runtime settings 文档。</summary>
        public bool CanHandle(YokiFrameProjectSettingsTarget target) =>
            target.EngineId == EngineId && target.Scope == YokiFrameProjectSettingsScope.Runtime;

        /// <summary>读取测试文本，并保留原文供 revision 使用。</summary>
        public YokiFrameProjectSettingsBackendDocument Read(
            YokiFrameProjectSettingsTarget target,
            string path)
        {
            bool exists = File.Exists(path);
            string content = exists ? File.ReadAllText(path) : string.Empty;
            return new YokiFrameProjectSettingsBackendDocument(
                target, path, exists, content, Array.Empty<YokiFrameProjectSetting>());
        }

        /// <summary>把单项测试 patch 序列化为稳定 owner/key=value 文本。</summary>
        public string Serialize(
            YokiFrameProjectSettingsBackendDocument document,
            IReadOnlyList<YokiFrameProjectSettingsPatch> patches)
        {
            YokiFrameProjectSettingsPatch patch = Assert.Single(patches);
            YokiFrameProjectSettingValue value = Assert.Single(patch.Values);
            return patch.Owner + "/" + value.Key + "=" + value.Value;
        }

        /// <summary>返回测试引擎的项目内配置路径。</summary>
        public string GetRelativePath(YokiFrameProjectSettingsTarget target) =>
            "Config/stride-runtime-settings.txt";
    }

    /// <summary>创建带 Assets 目录的隔离测试项目。</summary>
    private sealed class TestProject : IDisposable
    {
        /// <summary>初始化唯一项目根。</summary>
        internal TestProject()
        {
            Root = Path.Combine(Path.GetTempPath(), "yokiframe-project-settings", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "Assets"));
        }

        /// <summary>获取项目根。</summary>
        internal string Root { get; }

        /// <summary>删除测试目录。</summary>
        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
