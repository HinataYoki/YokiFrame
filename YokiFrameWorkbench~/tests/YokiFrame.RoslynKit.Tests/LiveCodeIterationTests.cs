using System.Text.Json.Nodes;
using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

public sealed partial class LiveCodeTests
{
    [Fact]
    /// <summary>批量挂接先全部准备再激活，并报告精确预算。两项都成功且事件顺序固定。</summary>
    public void Batch_prepares_all_members_before_activation_and_reports_exact_budget()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            bed.Host.OnActivate = () => Assert.Equal(2, bed.Manager.List().Count);
            var result = await bed.Manager.AttachMany(new[]
            {
                new LiveAttachmentRequest("First", new object(), "First", SnapshotSource),
                new LiveAttachmentRequest("Second", new object(), "Second", SnapshotSource)
            }, bed.Guard, default);
            Assert.True(result.Success, result.Error);
            Assert.Equal(new[] { "prepare", "prepare", "activate", "activate" }, bed.Host.Events);
            Assert.Equal(2, bed.Budget.LoadedCount);
            Assert.Equal(result.RequiredBytes, bed.Budget.LoadedBytes);
            Assert.All(result.Items, item => Assert.Equal("attached", item.Status));
            Assert.All(result.Items, item => Assert.Equal(2, item.Handle.Budget.LoadedAssemblies));
        });
    }

    [Theory]
    [InlineData("compile")]
    [InlineData("duplicate")]
    [InlineData("count")]
    [InlineData("bytes")]
    [InlineData("cancel")]
    [InlineData("session")]
    /// <summary>预检失败时不加载、不准备。列表和宿主事件保持为空。</summary>
    /// <param name="failure">compile、duplicate、count、bytes、cancel 或 session。</param>
    public void Batch_preflight_does_not_load_or_prepare_on_failure(string failure)
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            if (failure == "count")
                while (bed.Budget.LoadedCount < RoslynLoadBudget.MaxAssemblies - 1) bed.Budget.Reserve(0);
            if (failure == "bytes") bed.Budget.Reserve(RoslynLoadBudget.MaxLoadedBytes - 1);
            int loaded = bed.Budget.LoadedCount;
            using var cancel = new CancellationTokenSource();
            if (failure == "cancel") cancel.Cancel();
            var pending = bed.Manager.AttachMany(new[]
            {
                new LiveAttachmentRequest("First", new object(), "First", SnapshotSource),
                new LiveAttachmentRequest(failure == "duplicate" ? "First" : "Second", new object(), "Second",
                    failure == "compile" ? "bad code" : SnapshotSource)
            }, bed.Guard, cancel.Token);
            if (failure == "session") bed.State.SessionId = "changed";
            var result = await pending;
            Assert.False(result.Success);
            Assert.Equal(2, result.Items.Count);
            Assert.Equal(loaded, bed.Budget.LoadedCount);
            Assert.Empty(bed.Host.Events);
            Assert.Empty(bed.Manager.List());
        });
    }

    [Fact]
    /// <summary>批量替换在激活失败时回滚并保留字段。只替换成功的一项会推进修订。</summary>
    public void Batch_replacement_preserves_fields_and_rolls_back_failed_activation()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            var target = new object();
            await bed.Manager.Attach("First", target, "First", SnapshotSource, bed.Guard, default);
            bed.Manager.SetField("First", "Value", 37, bed.Guard);
            var requests = new[]
            {
                new LiveAttachmentRequest("First", target, "First", "public int Value = 90;"),
                new LiveAttachmentRequest("Second", new object(), "Second", "public int Value; public bool Fail = true;")
            };
            var failed = await bed.Manager.AttachMany(requests, bed.Guard, default);
            Assert.False(failed.Success);
            Assert.Equal("activate", failed.Stage);
            Assert.Equal(37, bed.Manager.ReadField("First", "Value", bed.Guard));
            Assert.Equal(1, Assert.Single(bed.Manager.List()).Revision);
            var success = await bed.Manager.AttachMany(new[] { requests[0] }, bed.Guard, default);
            Assert.True(success.Success, success.Error);
            Assert.Equal(2, Assert.Single(bed.Manager.List()).Revision);
            Assert.Equal(37, bed.Manager.ReadField("First", "Value", bed.Guard));
        });
    }

    [Fact]
    /// <summary>预算警告分别报告字节和程序集阈值。不加载用户程序集，恢复提示包含 live_snapshot。</summary>
    public void Budget_warning_reports_count_and_byte_thresholds_without_loading()
    {
        var budget = new RoslynLoadBudget();
        Assert.Equal("", budget.ReadStatus().BudgetWarning);
        budget.Reserve(RoslynLoadBudget.MaxLoadedBytes - RoslynLoadBudget.WarningRemainingBytes);
        Assert.Equal("bytes-low", budget.ReadStatus().BudgetWarning);
        while (budget.LoadedCount < RoslynLoadBudget.MaxAssemblies - 3) budget.Reserve(0);
        Assert.Equal("assemblies-and-bytes-low", budget.ReadStatus().BudgetWarning);
        Assert.Equal(3, budget.ReadStatus().RemainingAssemblies);
        Assert.Contains("live_snapshot", budget.ReadStatus().RecoveryHint);
    }

    [Fact]
    /// <summary>字段更新不编译也不调用 getter。非法字段名被拒绝后原值保持不变。</summary>
    public void Field_updates_are_zero_compile_and_do_not_call_getters()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach("public int Value = 7; public int Explode { get { throw new System.Exception(); } }");
            int count = bed.Budget.LoadedCount;
            var command = new LiveCodeOperation(bed.Manager, "live_set_fields");
            Assert.Equal(YokiFrameCommandKind.Dangerous, command.Descriptor.Kind);
            var response = command.Execute(new YokiFrameCommandRequest("cli", "RoslynKit", "live_set_fields",
                FieldPayload(53), 30000, 0));
            Assert.True(response.IsSuccess, response.ErrorMessage);
            Assert.Equal(53, bed.Manager.ReadField("Probe", "Value", bed.Guard));
            Assert.Equal(count, bed.Budget.LoadedCount);
            var malformed = JsonNode.Parse(FieldPayload(90))!;
            malformed["updates"]![0]!["fields"]!.AsArray().Add(
                new JsonObject { ["name"] = "Explode", ["type"] = "System.Int32", ["value"] = 1 });
            Assert.Throws<ArgumentException>(() => bed.Manager.SetFields(LiveFieldRequest.Parse(malformed.ToJsonString()), bed.Guard));
            Assert.Equal(53, bed.Manager.ReadField("Probe", "Value", bed.Guard));
        });
    }

    [Theory]
    [InlineData("type")]
    [InlineData("fraction")]
    [InlineData("overflow")]
    [InlineData("session")]
    [InlineData("generation")]
    [InlineData("revision")]
    [InlineData("target")]
    [InlineData("confirmed")]
    [InlineData("duplicate")]
    [InlineData("permission")]
    /// <summary>类型、范围、会话、修订、目标、确认、重复或许可不合法时拒绝整批。字段保持初值。</summary>
    /// <param name="failure">失败种类。</param>
    public void Field_batch_validation_rejects_without_mutation(string failure)
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach(SnapshotSource);
            var payload = JsonNode.Parse(FieldPayload(99))!;
            var field = payload["updates"]![0]!["fields"]![0]!;
            if (failure == "type") field["type"] = "System.Single";
            if (failure == "fraction") field["value"] = 1.5;
            if (failure == "overflow") field["value"] = long.MaxValue;
            if (failure == "session") payload["sessionId"] = "other";
            if (failure == "generation") payload["generation"] = 2;
            if (failure == "revision") payload["updates"]![0]!["revision"] = 2;
            if (failure == "target") payload["target"] = "editor";
            if (failure == "confirmed") payload["confirmed"] = false;
            if (failure == "duplicate") payload["updates"]![0]!["fields"]!.AsArray().Add(field.DeepClone());
            if (failure == "permission") bed.Permitted = false;
            Assert.ThrowsAny<Exception>(() => bed.Manager.SetFields(LiveFieldRequest.Parse(payload.ToJsonString()), bed.Guard));
            bed.Permitted = true;
            Assert.Equal(7, bed.Manager.ReadField("Probe", "Value", bed.Guard));
        });
    }

    [Fact]
    /// <summary>调参文件去抖后才应用，状态读取不产生额外应用。非法字段失败后恢复合法值，且不增加加载。</summary>
    public void Tuning_file_debounces_changes_keeps_status_pure_and_rejects_scope_changes()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach(SnapshotSource);
            var binder = new LiveTuningBinder(bed.Root, bed.Manager, () => bed.Permitted, () => bed.State);
            string relative = ".yokiframe/tuning/probe.json";
            string path = Path.Combine(bed.Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, FieldPayload(10));
            var status = binder.Bind("ProbeTuning", relative);
            int count = bed.Budget.LoadedCount;
            Assert.Equal(1, status.ApplyCount);
            File.WriteAllText(path, FieldPayload(123));
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2));
            binder.Tick(1);
            binder.Tick(1.25);
            Assert.Equal(10, bed.Manager.ReadField("Probe", "Value", bed.Guard));
            Assert.Equal(1, Assert.Single(binder.List()).ApplyCount);
            binder.Tick(1.75);
            Assert.Equal(123, bed.Manager.ReadField("Probe", "Value", bed.Guard));
            Assert.Equal(2, status.ApplyCount);
            binder.Tick(10);
            Assert.Equal(2, status.ApplyCount);
            var invalid = JsonNode.Parse(FieldPayload(456))!;
            invalid["updates"]![0]!["fields"]![0]!["name"] = "Other";
            File.WriteAllText(path, invalid.ToJsonString());
            binder.Refresh("ProbeTuning");
            Assert.Equal("error", status.State);
            Assert.Equal(123, bed.Manager.ReadField("Probe", "Value", bed.Guard));
            File.WriteAllText(path, FieldPayload(789));
            binder.Refresh("ProbeTuning");
            Assert.Equal("watching", status.State);
            Assert.Equal(789, bed.Manager.ReadField("Probe", "Value", bed.Guard));
            Assert.Equal(count, bed.Budget.LoadedCount);
            Assert.True(binder.Unbind("ProbeTuning"));
            Assert.Empty(binder.List());
        });
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("session")]
    [InlineData("revision")]
    [InlineData("reattach")]
    /// <summary>许可、会话、修订或重新挂接会使调参停止，之后不会自动恢复。Refresh 抛出 InvalidOperationException。</summary>
    /// <param name="change">permission、session、revision 或 reattach。</param>
    public void Tuning_stops_on_context_change_and_never_resumes_automatically(string change)
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach(SnapshotSource);
            var binder = new LiveTuningBinder(bed.Root, bed.Manager, () => bed.Permitted, () => bed.State);
            string path = Path.Combine(bed.Root, ".yokiframe/tuning/probe.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, FieldPayload(10));
            var status = binder.Bind("ProbeTuning", ".yokiframe/tuning/probe.json");
            if (change == "permission") bed.Permitted = false;
            if (change == "session") bed.State.SessionId = "changed";
            if (change == "revision") await bed.Attach(SnapshotSource);
            if (change == "reattach")
            {
                bed.Manager.Remove("Probe");
                var replacement = await bed.Attach(SnapshotSource);
                Assert.Equal(2, replacement.Revision);
            }
            binder.Tick(1);
            Assert.Equal("stopped", status.State);
            bed.Permitted = true;
            bed.State.SessionId = "session-1";
            File.WriteAllText(path, FieldPayload(999));
            binder.Tick(5);
            Assert.Equal("stopped", status.State);
            Assert.Equal(change == "reattach" ? 7 : 10, bed.Manager.ReadField("Probe", "Value", bed.Guard));
            Assert.Throws<InvalidOperationException>(() => binder.Refresh("ProbeTuning"));
        });
    }

    [Theory]
    [InlineData("Assets/tuning.json")]
    [InlineData(".yokiframe/tuning/../../outside.json")]
    [InlineData(".yokiframe/tuning/code.cs")]
    [InlineData("C:/outside.json")]
    /// <summary>不受控的调参路径被拒绝。不创建绑定。</summary>
    /// <param name="path">被拒绝的路径。</param>
    public void Tuning_rejects_uncontrolled_paths(string path)
    {
        using var bed = new LiveBed();
        var binder = new LiveTuningBinder(bed.Root, bed.Manager, () => true, () => bed.State);
        Assert.ThrowsAny<Exception>(() => binder.Bind("Probe", path));
    }

    /// <summary>构造一条确认过的 play 字段更新。不发送。</summary>
    /// <param name="value">Value 字段的新整数。</param>
    /// <returns>请求 JSON。</returns>
    private static string FieldPayload(int value) => new JsonObject
    {
        ["target"] = "play", ["sessionId"] = "session-1", ["generation"] = 1, ["confirmed"] = true,
        ["updates"] = new JsonArray(new JsonObject
        {
            ["id"] = "Probe", ["revision"] = 1,
            ["fields"] = new JsonArray(new JsonObject { ["name"] = "Value", ["type"] = "System.Int32", ["value"] = value })
        })
    }.ToJsonString();

    [Fact]
    /// <summary>集合批量替换不加载程序集。后面的非法元素会让整批失败并保留原集合实例。</summary>
    public void Collection_batch_replaces_without_loading_and_invalid_later_element_keeps_originals()
    {
        using var bed = new LiveBed();
        bed.Run(async () =>
        {
            await bed.Attach("public int Value = 7; public int[] Values = new int[] { 5 }; public System.Collections.Generic.List<int> Items = new System.Collections.Generic.List<int>();");
            int count = bed.Budget.LoadedCount;
            var payload = JsonNode.Parse(FieldPayload(80))!;
            var changes = payload["updates"]![0]!["fields"]!.AsArray();
            changes.Add(new JsonObject { ["name"] = "Values", ["type"] = "System.Int32[]", ["value"] = new JsonArray(1, 2) });
            changes.Add(new JsonObject { ["name"] = "Items", ["type"] = "System.Collections.Generic.List<System.Int32>", ["value"] = new JsonArray(3, 4) });
            Assert.Equal(3, bed.Manager.SetFields(LiveFieldRequest.Parse(payload.ToJsonString()), bed.Guard));
            var array = bed.Manager.ReadField("Probe", "Values", bed.Guard);
            var list = bed.Manager.ReadField("Probe", "Items", bed.Guard);
            Assert.Equal(new[] { 1, 2 }, (int[])array);
            Assert.Equal(new[] { 3, 4 }, (List<int>)list);
            changes[0]!["value"] = 99;
            changes[1]!["value"] = new JsonArray(10, 20);
            changes[2]!["value"] = new JsonArray(30, "invalid");
            Assert.ThrowsAny<Exception>(() => bed.Manager.SetFields(LiveFieldRequest.Parse(payload.ToJsonString()), bed.Guard));
            Assert.Same(array, bed.Manager.ReadField("Probe", "Values", bed.Guard));
            Assert.Same(list, bed.Manager.ReadField("Probe", "Items", bed.Guard));
            Assert.Equal(80, bed.Manager.ReadField("Probe", "Value", bed.Guard));
            Assert.Equal(count, bed.Budget.LoadedCount);
        });
    }
}
