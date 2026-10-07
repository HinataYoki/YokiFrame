using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

public sealed partial class LiveCodeTests
{
    [Fact]
    /// <summary>替换行为时迁移字段，并先停用旧实例再激活新实例。移除后列表为空。</summary>
    public void Behaviour_swap_migrates_fields_and_deactivates_old_before_activating_new()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach("public int Value = 7;");
            bed.Manager.SetField("Probe", "Value", 19, bed.Guard);
            bed.Host.Events.Clear();
            var second = await bed.Attach("public int Value = 8;");
            Assert.Equal(2, second.Revision);
            Assert.Equal(19, bed.Manager.ReadField("Probe", "Value", bed.Guard));
            Assert.Equal(new[] { "prepare", "suspend", "activate", "dispose" }, bed.Host.Events);
            Assert.True(bed.Manager.Remove("Probe"));
            Assert.Empty(bed.Manager.List());
            Assert.False(bed.Manager.Remove("Probe"));
        });
    }

    [Fact]
    /// <summary>编译失败或激活失败都保留上一修订。激活失败会回到旧实例的字段和 active 状态。</summary>
    public void Failed_compilation_and_failed_activation_preserve_previous_revision()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach("public int Value = 7;");
            await Assert.ThrowsAsync<ArgumentException>(() => bed.Attach("invalid source;"));
            Assert.Equal(1, Assert.Single(bed.Manager.List()).Revision);
            bed.Host.Events.Clear();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                bed.Attach("public int Value = 8; public bool Fail = true;"));
            Assert.Equal(new[] { "prepare", "suspend", "activate", "dispose", "activate" }, bed.Host.Events);
            Assert.Equal(7, bed.Manager.ReadField("Probe", "Value", bed.Guard));
            Assert.Equal("active", Assert.Single(bed.Manager.List()).Status);
        });
    }

    [Fact]
    /// <summary>编译期间清空会释放上一实例并取消挂起版本。加载计数不增加，列表为空。</summary>
    public void Clearing_during_compilation_disposes_previous_and_invalidates_pending_version()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach("public int Value = 7;");
            int loaded = bed.Budget.LoadedCount;
            var pending = bed.Attach("public int Value = 8;");
            bed.Manager.Clear(_ => Assert.Fail("Clear should not skip pending IDs."));
            await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
            Assert.Equal(loaded, bed.Budget.LoadedCount);
            Assert.Empty(bed.Manager.List());
            Assert.Contains("dispose", bed.Host.Events);
        });
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("session")]
    [InlineData("target")]
    /// <summary>许可、会话或目标变化会使挂起编译在加载前取消。不产生宿主事件。</summary>
    /// <param name="change">permission、session 或 target。</param>
    public void Stale_compilation_is_rejected_before_loading_or_attaching(string change)
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            var pending = bed.Attach("public int Value = 7;");
            if (change == "permission") bed.Permitted = false;
            if (change == "session") bed.State.SessionId = "session-2";
            if (change == "target") bed.State.ActiveTarget = "editor";
            await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
            Assert.Equal(0, bed.Budget.LoadedCount);
            Assert.Empty(bed.Host.Events);
        });
    }

    [Fact]
    /// <summary>导出校验不额外加载程序集。附件失效后状态为 unavailable，再次导出抛出 InvalidOperationException。</summary>
    public void Export_validation_does_not_load_another_assembly_and_unavailable_hosts_are_reported()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach("public int Value = 7;");
            int loaded = bed.Budget.LoadedCount;
            Assert.Equal("export-record", await bed.Manager.Export("Probe", "Assets/Probe.cs", bed.Guard, default));
            Assert.Equal(loaded, bed.Budget.LoadedCount);
            bed.Host.Latest!.Alive = false;
            Assert.Equal("unavailable", Assert.Single(bed.Manager.List()).Status);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                bed.Manager.Export("Probe", "Assets/Probe.cs", bed.Guard, default));
        });
    }

    [Fact]
    /// <summary>同一标识并发更新被拒绝。列表查询必须在主同步上下文上执行。</summary>
    public void Concurrent_id_update_is_rejected_and_main_thread_is_required()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            var first = bed.Attach("public int Value = 7;");
            await Assert.ThrowsAsync<InvalidOperationException>(() => bed.Attach("public int Value = 8;"));
            await first;
            await Task.Run(() => Assert.Throws<InvalidOperationException>(() => bed.Manager.List()));
        });
    }

    [Fact]
    /// <summary>失效附件可被替换，且不读取或恢复旧实例。事件顺序不含 suspend。</summary>
    public void Faulted_attachment_can_be_replaced_without_reading_or_resuming_it()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach("public int Value = 7;");
            bed.Host.Latest!.Alive = false;
            bed.Host.Events.Clear();
            var next = await bed.Attach("public int Value = 12;");
            Assert.Equal(2, next.Revision);
            Assert.Equal(12, bed.Manager.ReadField("Probe", "Value", bed.Guard));
            Assert.Equal(new[] { "prepare", "activate", "dispose" }, bed.Host.Events);
        });
    }

    [Fact]
    /// <summary>按标识调用跟随最新替换。移除后再次调用抛出 ArgumentException。</summary>
    public void Invocation_by_id_follows_replacement_and_rejects_removed_ids()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach("public int Value = 7; public int Hit(int amount) { return Value -= amount; }");
            Assert.Equal(5, bed.Manager.Invoke("Probe", "Hit", new object[] { 2 }, bed.Guard));
            await bed.Attach("public int Value; public int Hit(int amount) { return Value -= amount * 2; }");
            Assert.Equal(1, bed.Manager.Invoke("Probe", "Hit", new object[] { 2 }, bed.Guard));
            bed.Manager.Remove("Probe");
            Assert.Throws<ArgumentException>(() => bed.Manager.Invoke("Probe", "Hit", new object[] { 1 }, bed.Guard));
        });
    }

    [Fact]
    /// <summary>同一标识重编译会再加载一个程序集。改字段和移除都不增加加载计数。</summary>
    public void Same_id_recompile_uses_another_assembly_but_field_edits_and_removal_do_not()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            int initial = bed.Budget.LoadedCount;
            await bed.Attach("public int Value = 7;");
            Assert.Equal(initial + 1, bed.Budget.LoadedCount);
            await bed.Attach("public int Value = 8;");
            Assert.Equal(initial + 2, bed.Budget.LoadedCount);
            for (int i = 0; i < 50; i++) bed.Manager.SetField("Probe", "Value", i, bed.Guard);
            Assert.Equal(49, bed.Manager.ReadField("Probe", "Value", bed.Guard));
            Assert.Equal(initial + 2, bed.Budget.LoadedCount);
            bed.Manager.Remove("Probe");
            Assert.Equal(initial + 2, bed.Budget.LoadedCount);
        });
    }

    [Fact]
    /// <summary>补丁更新生效，编译失败保留上一补丁，移除后恢复原方法返回值。</summary>
    public void Harmony_patch_update_failed_compile_and_removal_restore_original()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            var builder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("GamePatchProbe"), AssemblyBuilderAccess.Run);
            var type = builder.DefineDynamicModule("GamePatchProbe").DefineType("GamePatchProbe", TypeAttributes.Public);
            var method = type.DefineMethod("Read", MethodAttributes.Public | MethodAttributes.Static, typeof(int), Type.EmptyTypes);
            method.SetImplementationFlags(MethodImplAttributes.NoInlining);
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldc_I4_7);
            il.Emit(OpCodes.Ret);
            var original = type.CreateType()!.GetMethod("Read")!;
            var call = (Func<int>)original.CreateDelegate(typeof(Func<int>));
            string source = "public static class ProbePatch { public static bool Prefix(ref int __result)"
                + " { __result = 42; return false; } }";
            try
            {
                await bed.Manager.Patch("PatchProbe", original, source, "ProbePatch", "Prefix", "replace", bed.Guard, default);
                Assert.Equal(42, call());
                await bed.Manager.Patch("PatchProbe", original, source.Replace("42", "51"),
                    "ProbePatch", "Prefix", "replace", bed.Guard, default);
                Assert.Equal(51, call());
                await Assert.ThrowsAsync<ArgumentException>(() => bed.Manager.Patch("PatchProbe", original,
                    "broken", "ProbePatch", "Prefix", "replace", bed.Guard, default));
                Assert.Equal(51, call());
            }
            finally { bed.Manager.Remove("PatchProbe"); }
            Assert.Equal(7, call());
        });
    }

    [Theory]
    [InlineData("../Probe.cs")]
    [InlineData("Assets/../../Probe.cs")]
    [InlineData("Assets/../Probe.cs")]
    [InlineData("Assets/Wrong.cs")]
    [InlineData("Packages/Probe.cs")]
    /// <summary>导出路径拒绝逃逸、错误文件名和非 Assets 位置。不创建文件。</summary>
    /// <param name="path">被拒绝的相对路径。</param>
    public void Export_paths_reject_escape_wrong_filename_and_non_asset_locations(string path)
    {
        using var bed = new LiveBed();
        Assert.ThrowsAny<Exception>(() => LiveCodePaths.ExportScript(bed.Root, path, "Probe", "Assets"));
    }

    [Fact]
    /// <summary>已有源码不被覆盖，逃逸记录号被拒绝，非方法补丁目标抛出 NotSupportedException。</summary>
    public void Export_never_overwrites_existing_source_and_manifest_ids_are_validated()
    {
        using var bed = new LiveBed();
        Directory.CreateDirectory(Path.Combine(bed.Root, "Assets"));
        File.WriteAllText(Path.Combine(bed.Root, "Assets", "Probe.cs"), "existing");
        Assert.Throws<IOException>(() => LiveCodePaths.ExportScript(bed.Root, "Assets/Probe.cs", "Probe", "Assets"));
        Assert.Throws<ArgumentException>(() => LiveCodePaths.Record(bed.Root, "../escape"));
        Assert.Throws<NotSupportedException>(() => LiveCodeManager.ValidatePatchTarget(
            typeof(LiveCodeManager).GetMethod(nameof(LiveCodeManager.Hash))!));
    }

    [Fact]
    /// <summary>宿主选定的根和扩展名可以导出。根不匹配时抛出 ArgumentException。</summary>
    public void Source_paths_accept_host_selected_roots_and_extensions()
    {
        using var bed = new LiveBed();
        Assert.EndsWith(Path.Combine("Scripts", "Probe.cs"),
            LiveCodePaths.ExportScript(bed.Root, "Scripts/Probe.cs", "Probe", "Scripts"));
        Assert.EndsWith(Path.Combine("Scripts", "Probe.gd"),
            LiveCodePaths.ExportScript(bed.Root, "Scripts/Probe.gd", "Probe", "Scripts", ".gd"));
        Assert.Throws<ArgumentException>(() =>
            LiveCodePaths.ExportScript(bed.Root, "Assets/Probe.cs", "Probe", "Scripts"));
    }

    private sealed class LiveBed : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "yokiframe-live-tests", Guid.NewGuid().ToString("N"));
        public RoslynLoadBudget Budget { get; } = new();
        public FakeHost Host { get; }
        public bool Permitted { get; set; } = true;
        public RoslynDomainState State { get; } = new()
        {
            ActiveTarget = "play", SessionIdentityAvailable = true, SessionId = "session-1", Generation = 1
        };
        public LiveCodeManager Manager { get; }
        public RoslynCompilerLoader Compiler { get; }
        public Action Guard { get; } = () => { };
        private readonly object mTarget = new();

        /// <summary>准备临时编译器目录、宿主和管理器。exports 为 true 时使用可提交的导出宿主。</summary>
        /// <param name="exports">是否启用导出宿主。</param>
        public LiveBed(bool exports = false)
        {
            Host = exports ? new ExportFakeHost(Root) : new FakeHost();
            string directory = Path.Combine(Root, RoslynCompilerLoader.RELATIVE_PATH);
            Directory.CreateDirectory(directory);
            foreach (string file in Directory.GetFiles(AppContext.BaseDirectory, "*.dll"))
                File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            Compiler = new RoslynCompilerLoader(Root);
            Manager = new LiveCodeManager(Compiler, Budget,
                new MethodPatchBackend(AppContext.BaseDirectory), Host, () => State, () => Permitted, Root);
        }

        /// <summary>把 Probe 源码挂到固定目标。不改变许可和会话。</summary>
        /// <param name="source">类型成员源码。</param>
        /// <returns>挂接句柄。</returns>
        public Task<LiveCodeHandle> Attach(string source) =>
            Manager.Attach("Probe", mTarget, "Probe", source, Guard, default);

        /// <summary>在可泵送的同步上下文中跑完异步动作。超时或失败时断言，并恢复原上下文。</summary>
        /// <param name="action">要执行的异步动作。</param>
        public void Run(Func<Task> action)
        {
            var previous = SynchronizationContext.Current;
            var context = new PumpContext();
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                Task task = action();
                var timeout = DateTime.UtcNow.AddSeconds(30);
                while (!task.IsCompleted && DateTime.UtcNow < timeout)
                {
                    if (context.Queue.TryDequeue(out var item)) item.Callback(item.State);
                    else Thread.Sleep(1);
                }
                Assert.True(task.IsCompleted, "Live operation timed out.");
                task.GetAwaiter().GetResult();
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }

        /// <summary>清空管理器、释放编译器并删除临时根目录。</summary>
        public void Dispose()
        {
            Manager.Clear(_ => { });
            Compiler.Dispose();
            Directory.Delete(Root, true);
        }
    }

    private sealed class PumpContext : SynchronizationContext
    {
        public ConcurrentQueue<(SendOrPostCallback Callback, object? State)> Queue { get; } = new();
        /// <summary>把回调排入队列，供测试循环手动泵送。不立刻执行。</summary>
        /// <param name="callback">待执行回调。</param>
        /// <param name="state">回调状态。</param>
        public override void Post(SendOrPostCallback callback, object? state) => Queue.Enqueue((callback, state));
    }

    private sealed class FakeAttachment : IDisposable
    {
        private readonly List<string> mEvents;
        public object Instance { get; }
        public bool Alive { get; set; } = true;
        /// <summary>创建脚本实例并记下事件列表。不激活。</summary>
        /// <param name="type">已编译类型。</param>
        /// <param name="events">共享事件记录。</param>
        public FakeAttachment(Type type, List<string> events) { Instance = Activator.CreateInstance(type)!; mEvents = events; }
        /// <summary>标记附件失效并记录 dispose。不销毁其他对象。</summary>
        public void Dispose() { Alive = false; mEvents.Add("dispose"); }
    }

    private class FakeHost : ILiveCodeHost, ILiveSnapshotHost, ILiveFieldHost
    {
        public List<string> Events { get; } = new();
        public FakeAttachment? Latest { get; private set; }
        public bool FailActivation { get; set; }
        public bool FailFields { get; set; }
        public bool FailResolve { get; set; }
        public string SnapshotFields { get; set; } = "";
        public Action? OnActivate { get; set; }
        public Action<IReadOnlyDictionary<string, object>>? OnRestoreFields { get; set; }
        /// <summary>把成员包进公开类。不区分持久化标记。</summary>
        /// <param name="className">类名。</param>
        /// <param name="members">类成员源码。</param>
        /// <param name="persistent">未使用的持久化标记。</param>
        /// <returns>完整类源码。</returns>
        public string WrapBehaviour(string className, string members, bool persistent) =>
            "public class " + className + " {" + members + "}";
        /// <summary>准备附件并按状态写 Value。记录 prepare，不激活。</summary>
        /// <param name="id">未使用的标识。</param>
        /// <param name="target">未使用的目标。</param>
        /// <param name="type">已编译类型。</param>
        /// <param name="state">要写入 Value 的文本，空则跳过。</param>
        /// <returns>新建附件。</returns>
        public IDisposable Prepare(string id, object target, Type type, string state)
        {
            Events.Add("prepare");
            Latest = new FakeAttachment(type, Events);
            if (state.Length > 0) type.GetField("Value")!.SetValue(Latest.Instance, int.Parse(state));
            return Latest;
        }
        /// <summary>记录 activate，并在失败开关或实例字段 Fail 为真时抛出 InvalidOperationException。</summary>
        /// <param name="attachment">待激活附件。</param>
        public void Activate(IDisposable attachment)
        {
            Events.Add("activate");
            OnActivate?.Invoke();
            if (FailActivation) throw new InvalidOperationException("activation");
            var instance = ((FakeAttachment)attachment).Instance;
            if (instance.GetType().GetField("Fail")?.GetValue(instance) is true) throw new InvalidOperationException("activation");
        }
        /// <summary>记录 suspend。不改变附件存活状态。</summary>
        /// <param name="attachment">未使用的附件。</param>
        public void Suspend(IDisposable attachment) => Events.Add("suspend");
        /// <summary>返回附件是否仍存活。不改状态。</summary>
        /// <param name="attachment">待查询附件。</param>
        /// <returns>存活标记。</returns>
        public bool IsAlive(IDisposable attachment) => ((FakeAttachment)attachment).Alive;
        /// <summary>把 Value 字段读成状态文本。不写回。</summary>
        /// <param name="attachment">活动附件。</param>
        /// <returns>字段文本。</returns>
        public string CaptureState(IDisposable attachment) => ReadField(attachment, "Value").ToString()!;
        /// <summary>按名称读取实例字段。字段必须存在。</summary>
        /// <param name="attachment">活动附件。</param>
        /// <param name="name">字段名。</param>
        /// <returns>字段值。</returns>
        public object ReadField(IDisposable attachment, string name)
        {
            object instance = ((FakeAttachment)attachment).Instance;
            return instance.GetType().GetField(name)!.GetValue(instance)!;
        }
        /// <summary>按名称写实例字段。不编译、不激活。</summary>
        /// <param name="attachment">活动附件。</param>
        /// <param name="name">字段名。</param>
        /// <param name="value">新值。</param>
        public void SetField(IDisposable attachment, string name, object value)
        {
            object instance = ((FakeAttachment)attachment).Instance;
            instance.GetType().GetField(name)!.SetValue(instance, value);
        }
        /// <summary>通过 LiveMethodInvoker 调用实例方法。异常原样抛出。</summary>
        /// <param name="attachment">活动附件。</param>
        /// <param name="method">方法名。</param>
        /// <param name="arguments">参数。</param>
        /// <returns>调用结果。</returns>
        public object Invoke(IDisposable attachment, string method, object[] arguments) =>
            new LiveMethodInvoker(((FakeAttachment)attachment).Instance).Invoke(method, arguments);
        /// <summary>返回固定导出记录文本。不写文件。</summary>
        /// <param name="id">未使用的标识。</param>
        /// <param name="className">未使用的类名。</param>
        /// <param name="source">未使用的源码。</param>
        /// <param name="sourceHash">未使用的哈希。</param>
        /// <param name="attachment">未使用的附件。</param>
        /// <param name="path">未使用的路径。</param>
        /// <returns>固定文本 export-record。</returns>
        public string Export(string id, string className, string source, string sourceHash, IDisposable attachment, string path) =>
            "export-record";
        /// <summary>返回固定绑定标记。不关联目标。</summary>
        /// <param name="exportId">未使用的导出号。</param>
        /// <param name="target">未使用的目标。</param>
        /// <returns>固定文本 bound。</returns>
        public string Bind(string exportId, object target) => "bound";
        /// <summary>捕获目标身份和字段文本。预设字段优先于当前 Value。</summary>
        /// <param name="id">快照标识。</param>
        /// <param name="attachment">活动附件。</param>
        /// <param name="keyForObject">未使用的引用键函数。</param>
        /// <returns>不含引用的快照状态。</returns>
        public LiveSnapshotState CaptureSnapshot(string id, IDisposable attachment, Func<object, string, string> keyForObject) =>
            new(new LiveObjectIdentity(id + ":target", "", "", "System.Object", id),
                SnapshotFields.Length > 0 ? SnapshotFields : CaptureState(attachment), Array.Empty<LiveSnapshotReference>());
        /// <summary>记录 resolve。失败开关打开时抛出 InvalidOperationException，否则交给解析器。</summary>
        /// <param name="identity">对象身份。</param>
        /// <param name="resolver">实际解析函数。</param>
        /// <returns>解析出的对象。</returns>
        public object ResolveSnapshotObject(LiveObjectIdentity identity, Func<LiveObjectIdentity, object> resolver)
        {
            Events.Add("resolve");
            if (FailResolve) throw new InvalidOperationException("missing target");
            return resolver(identity);
        }
        /// <summary>记录 fields 并写回 Value。失败开关打开时先抛异常，不写字段。</summary>
        /// <param name="attachment">活动附件。</param>
        /// <param name="fields">整数字段文本。</param>
        /// <param name="references">交给回调的引用表。</param>
        public void RestoreSnapshotFields(IDisposable attachment, string fields, IReadOnlyDictionary<string, object> references)
        {
            Events.Add("fields");
            OnRestoreFields?.Invoke(references);
            if (FailFields) throw new InvalidOperationException("fields");
            SetField(attachment, "Value", int.Parse(fields));
        }
        /// <summary>要求字段文本可解析为整数。不修改状态。</summary>
        /// <param name="state">待校验快照。</param>
        public void ValidateSnapshotState(LiveSnapshotState state) => int.Parse(state.Fields);
        /// <summary>绑定公开实例字段。非公开或不存在时抛出 ArgumentException。</summary>
        /// <param name="attachment">活动附件。</param>
        /// <param name="name">字段名。</param>
        /// <returns>字段绑定。</returns>
        public LiveFieldBinding BindTunableField(IDisposable attachment, string name)
        {
            var instance = ((FakeAttachment)attachment).Instance;
            var field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException("Only public fields.");
            return new LiveFieldBinding(instance, field);
        }
        /// <summary>按公开实例字段解码可调值。不执行用户代码。</summary>
        /// <param name="type">目标类型。</param>
        /// <param name="value">JSON 值。</param>
        /// <returns>解码结果。</returns>
        public object DecodeTunableValue(Type type, YokiFrame.Json.JsonElement value)
        {
            return LiveFieldValues.Decode(type, value,
                t => t.GetFields(BindingFlags.Public | BindingFlags.Instance));
        }
    }
}
