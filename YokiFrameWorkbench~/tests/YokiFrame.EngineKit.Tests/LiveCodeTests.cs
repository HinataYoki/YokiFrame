using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using YokiFrame;

namespace YokiFrame.EngineKit.Tests;

public sealed partial class LiveCodeTests
{
    [Fact]
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
    public void Export_paths_reject_escape_wrong_filename_and_non_asset_locations(string path)
    {
        using var bed = new LiveBed();
        Assert.ThrowsAny<Exception>(() => YokiFrameLiveCodePaths.ExportScript(bed.Root, path, "Probe", "Assets"));
    }

    [Fact]
    public void Export_never_overwrites_existing_source_and_manifest_ids_are_validated()
    {
        using var bed = new LiveBed();
        Directory.CreateDirectory(Path.Combine(bed.Root, "Assets"));
        File.WriteAllText(Path.Combine(bed.Root, "Assets", "Probe.cs"), "existing");
        Assert.Throws<IOException>(() => YokiFrameLiveCodePaths.ExportScript(bed.Root, "Assets/Probe.cs", "Probe", "Assets"));
        Assert.Throws<ArgumentException>(() => YokiFrameLiveCodePaths.Record(bed.Root, "../escape"));
        Assert.Throws<NotSupportedException>(() => YokiFrameLiveCodeManager.ValidatePatchTarget(
            typeof(YokiFrameLiveCodeManager).GetMethod(nameof(YokiFrameLiveCodeManager.Hash))!));
    }

    [Fact]
    public void Source_paths_accept_host_selected_roots_and_extensions()
    {
        using var bed = new LiveBed();
        Assert.EndsWith(Path.Combine("Scripts", "Probe.cs"),
            YokiFrameLiveCodePaths.ExportScript(bed.Root, "Scripts/Probe.cs", "Probe", "Scripts"));
        Assert.EndsWith(Path.Combine("Scripts", "Probe.gd"),
            YokiFrameLiveCodePaths.ExportScript(bed.Root, "Scripts/Probe.gd", "Probe", "Scripts", ".gd"));
        Assert.Throws<ArgumentException>(() =>
            YokiFrameLiveCodePaths.ExportScript(bed.Root, "Assets/Probe.cs", "Probe", "Scripts"));
    }

    private sealed class LiveBed : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "yokiframe-live-tests", Guid.NewGuid().ToString("N"));
        public YokiFrameRoslynLoadBudget Budget { get; } = new();
        public FakeHost Host { get; }
        public bool Permitted { get; set; } = true;
        public YokiFrameEngineDomainState State { get; } = new()
        {
            ActiveTarget = "play", SessionIdentityAvailable = true, SessionId = "session-1", Generation = 1
        };
        public YokiFrameLiveCodeManager Manager { get; }
        public Action Guard { get; } = () => { };
        private readonly object mTarget = new();

        public LiveBed(bool exports = false)
        {
            Host = exports ? new ExportFakeHost(Root) : new FakeHost();
            string directory = Path.Combine(Root, YokiFrameRoslynCompilerLoader.RELATIVE_PATH);
            Directory.CreateDirectory(directory);
            foreach (string file in Directory.GetFiles(AppContext.BaseDirectory, "*.dll"))
                File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
            Manager = new YokiFrameLiveCodeManager(new YokiFrameRoslynCompilerLoader(Root), Budget,
                new YokiFrameMethodPatchBackend(AppContext.BaseDirectory), Host, () => State, () => Permitted, Root);
        }

        public Task<YokiFrameLiveCodeHandle> Attach(string source) =>
            Manager.Attach("Probe", mTarget, "Probe", source, Guard, default);

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

        public void Dispose()
        {
            Manager.Clear(_ => { });
            Directory.Delete(Root, true);
        }
    }

    private sealed class PumpContext : SynchronizationContext
    {
        public ConcurrentQueue<(SendOrPostCallback Callback, object? State)> Queue { get; } = new();
        public override void Post(SendOrPostCallback callback, object? state) => Queue.Enqueue((callback, state));
    }

    private sealed class FakeAttachment : IDisposable
    {
        private readonly List<string> mEvents;
        public object Instance { get; }
        public bool Alive { get; set; } = true;
        public FakeAttachment(Type type, List<string> events) { Instance = Activator.CreateInstance(type)!; mEvents = events; }
        public void Dispose() { Alive = false; mEvents.Add("dispose"); }
    }

    private class FakeHost : IYokiFrameLiveCodeHost, IYokiFrameLiveSnapshotHost, IYokiFrameLiveFieldHost
    {
        public List<string> Events { get; } = new();
        public FakeAttachment? Latest { get; private set; }
        public bool FailActivation { get; set; }
        public bool FailFields { get; set; }
        public bool FailResolve { get; set; }
        public string SnapshotFields { get; set; } = "";
        public Action? OnActivate { get; set; }
        public Action<IReadOnlyDictionary<string, object>>? OnRestoreFields { get; set; }
        public string WrapBehaviour(string className, string members, bool persistent) =>
            "public class " + className + " {" + members + "}";
        public IDisposable Prepare(string id, object target, Type type, string state)
        {
            Events.Add("prepare");
            Latest = new FakeAttachment(type, Events);
            if (state.Length > 0) type.GetField("Value")!.SetValue(Latest.Instance, int.Parse(state));
            return Latest;
        }
        public void Activate(IDisposable attachment)
        {
            Events.Add("activate");
            OnActivate?.Invoke();
            if (FailActivation) throw new InvalidOperationException("activation");
            var instance = ((FakeAttachment)attachment).Instance;
            if (instance.GetType().GetField("Fail")?.GetValue(instance) is true) throw new InvalidOperationException("activation");
        }
        public void Suspend(IDisposable attachment) => Events.Add("suspend");
        public bool IsAlive(IDisposable attachment) => ((FakeAttachment)attachment).Alive;
        public string CaptureState(IDisposable attachment) => ReadField(attachment, "Value").ToString()!;
        public object ReadField(IDisposable attachment, string name)
        {
            object instance = ((FakeAttachment)attachment).Instance;
            return instance.GetType().GetField(name)!.GetValue(instance)!;
        }
        public void SetField(IDisposable attachment, string name, object value)
        {
            object instance = ((FakeAttachment)attachment).Instance;
            instance.GetType().GetField(name)!.SetValue(instance, value);
        }
        public object Invoke(IDisposable attachment, string method, object[] arguments) =>
            new YokiFrameLiveMethodInvoker(((FakeAttachment)attachment).Instance).Invoke(method, arguments);
        public string Export(string id, string className, string source, string sourceHash, IDisposable attachment, string path) =>
            "export-record";
        public string Bind(string exportId, object target) => "bound";
        public YokiFrameLiveSnapshotState CaptureSnapshot(string id, IDisposable attachment, Func<object, string, string> keyForObject) =>
            new(new YokiFrameLiveObjectIdentity(id + ":target", "", "", "System.Object", id),
                SnapshotFields.Length > 0 ? SnapshotFields : CaptureState(attachment), Array.Empty<YokiFrameLiveSnapshotReference>());
        public object ResolveSnapshotObject(YokiFrameLiveObjectIdentity identity, Func<YokiFrameLiveObjectIdentity, object> resolver)
        {
            Events.Add("resolve");
            if (FailResolve) throw new InvalidOperationException("missing target");
            return resolver(identity);
        }
        public void RestoreSnapshotFields(IDisposable attachment, string fields, IReadOnlyDictionary<string, object> references)
        {
            Events.Add("fields");
            OnRestoreFields?.Invoke(references);
            if (FailFields) throw new InvalidOperationException("fields");
            SetField(attachment, "Value", int.Parse(fields));
        }
        public void ValidateSnapshotState(YokiFrameLiveSnapshotState state) => int.Parse(state.Fields);
        public YokiFrameLiveFieldBinding BindTunableField(IDisposable attachment, string name)
        {
            var instance = ((FakeAttachment)attachment).Instance;
            var field = instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new ArgumentException("Only public fields.");
            return new YokiFrameLiveFieldBinding(instance, field);
        }
        public object DecodeTunableValue(Type type, YokiFrame.Json.JsonElement value)
        {
            if (YokiFrameLiveFieldValues.TryDecodeScalar(type, value, out var scalar)) return scalar;
            throw new NotSupportedException("Unsupported field.");
        }
    }
}
