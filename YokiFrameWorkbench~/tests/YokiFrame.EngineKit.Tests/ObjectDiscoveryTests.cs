using System.Collections;
using System.Text;
using System.Text.Json;
using YokiFrame;

namespace YokiFrame.EngineKit.Tests;

[CollectionDefinition("ObjectDiscovery", DisableParallelization = true)]
public sealed class ObjectDiscoveryCollection { }

[Collection("ObjectDiscovery")]
public sealed class ObjectDiscoveryTests : IDisposable
{
    private readonly IArchitecture mArchitecture = CatalogArchitecture.Interface;
    private readonly StubEngineOperationProvider mEngine = new("Unity",
        YokiFrameEngineExecutionTarget.Editor | YokiFrameEngineExecutionTarget.Play);
    private readonly YokiFrameEngineKitProvider mProvider;
    public ObjectDiscoveryTests()
    {
        mEngine.State.SessionId = "object-session";
        mEngine.State.Generation = 4;
        mEngine.State.SessionIdentityAvailable = true;
        var settings = new StubEngineSettingsSource(YokiFrameEngineSettingsSnapshot.InvalidConfig("broken json"));
        mProvider = new YokiFrameEngineKitProvider(YokiFrameEngineGate.CreateDefault(settings), mEngine, settings);
        ProbeAttribute.Calls = 0;
        ProbeService.Calls = 0;
    }

    [Fact]
    public void Discovery_is_available_when_disabled_and_never_executes_user_code()
    {
        var list = Query("object_list", Payload());
        var item = Assert.Single(list.GetProperty("objects").EnumerateArray());
        Assert.Equal(typeof(ProbeService).FullName, item.GetProperty("type").GetString());
        string id = item.GetProperty("objectId").GetString()!;
        var description = Query("object_describe", Payload(new { objectId = id, limit = 100 }));
        var members = description.GetProperty("members").EnumerateArray().ToArray();
        var methods = members.Where(m => m.GetProperty("name").GetString() == "Read").ToArray();
        Assert.Equal(2, methods.Length);
        Assert.NotEqual(methods[0].GetProperty("memberId").GetString(), methods[1].GetProperty("memberId").GetString());
        var optional = Assert.Single(members, m => m.GetProperty("name").GetString() == "Change");
        var parameters = optional.GetProperty("parameters");
        Assert.Equal("System.Int32", parameters[0].GetProperty("type").GetString());
        Assert.Equal("7", parameters[0].GetProperty("defaultValue").GetString());
        Assert.Equal("hello", parameters[1].GetProperty("defaultValue").GetString());
        Assert.Equal("null", parameters[2].GetProperty("defaultKind").GetString());
        Assert.True(Assert.Single(members, m => m.GetProperty("name").GetString() == "Later")
            .GetProperty("async").GetBoolean());
        Assert.False(Assert.Single(members, m => m.GetProperty("name").GetString() == "Generic")
            .GetProperty("supported").GetBoolean());
        Assert.False(Assert.Single(members, m => m.GetProperty("name").GetString() == "ByRef")
            .GetProperty("supported").GetBoolean());
        Assert.Contains(members, m => m.GetProperty("name").GetString() == "ExplosiveGetter"
            && m.GetProperty("requiresExecution").GetBoolean());
        Assert.DoesNotContain(members, m => m.GetProperty("name").GetString() == "PrivateMethod");
        Assert.Equal(0, ProbeService.Calls);
        Assert.Equal(0, ProbeAttribute.Calls);
    }

    [Fact]
    public void Id_is_stable_for_same_registration_but_invalid_after_replacement_or_disposal()
    {
        string id = FirstId();
        var current = mArchitecture.GetService<ProbeService>();
        Register(current);
        Assert.Equal(id, FirstId());
        var replacement = new ProbeService();
        Register(replacement);
        Assert.NotEqual(id, FirstId());
        Assert.Equal("ObjectHandleExpired", Send("object_describe", Payload(new { objectId = id })).ErrorCode);
        string replacedId = FirstId();
        mArchitecture.Dispose();
        Assert.Equal("ObjectHandleExpired", Send("object_describe", Payload(new { objectId = replacedId })).ErrorCode);
        GC.KeepAlive(replacement);
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("session")]
    [InlineData("target")]
    [InlineData("lifecycle")]
    public void Handles_expire_at_identity_or_target_boundaries(string change)
    {
        string id = FirstId();
        if (change == "generation") mEngine.State.Generation++;
        if (change == "session") mEngine.State.SessionId = "next-session";
        if (change == "target")
        {
            mEngine.State.ActiveTarget = "play";
            Query("object_list", Payload(target: "play"));
            mEngine.State.ActiveTarget = "editor";
        }
        if (change == "lifecycle") mProvider.InvalidateObjectHandles();
        Assert.Equal("ObjectHandleExpired", Send("object_describe", Payload(new { objectId = id })).ErrorCode);
    }

    [Fact]
    public void Paging_is_bounded_and_requires_the_same_catalog_revision()
    {
        string id = FirstId();
        var first = Query("object_describe", Payload(new { objectId = id, limit = 1 }));
        Assert.Single(first.GetProperty("members").EnumerateArray());
        int offset = first.GetProperty("nextOffset").GetInt32();
        string revision = first.GetProperty("catalogRevision").GetString()!;
        var next = Query("object_describe", Payload(new { objectId = id, offset, limit = 1, catalogRevision = revision }));
        Assert.NotEqual(first.GetProperty("members")[0].GetProperty("memberId").GetString(),
            next.GetProperty("members")[0].GetProperty("memberId").GetString());
        Assert.Equal("ObjectCatalogChanged", Send("object_describe", Payload(new { objectId = id, offset })).ErrorCode);
        Register(mArchitecture.GetService<ProbeService>());
        Assert.Equal("ObjectCatalogChanged", Send("object_describe",
            Payload(new { objectId = id, offset, catalogRevision = revision })).ErrorCode);
        Assert.True(Encoding.UTF8.GetByteCount(first.GetRawText()) <= YokiFrameEngineObjectCatalog.MAX_RESPONSE_BYTES);
    }

    [Theory]
    [InlineData("""{"target":"editor","root":"scene"}""")]
    [InlineData("""{"target":"editor","limit":101}""")]
    [InlineData("""{"target":"editor","offset":-1}""")]
    [InlineData("""{"target":"editor","limit":"1"}""")]
    [InlineData("""{"target":"editor|play"}""")]
    [InlineData("{}")]
    [InlineData("[]")]
    public void Invalid_payloads_are_rejected(string payload) =>
        Assert.Equal(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, Send("object_list", payload).ErrorCode);

    [Fact]
    public void Inactive_targets_and_unpublished_sessions_are_unavailable()
    {
        Assert.Equal(YokiFrameEngineErrorCodes.UNAVAILABLE, Send("object_list", Payload(target: "play")).ErrorCode);
        mEngine.State.SessionIdentityAvailable = false;
        Assert.Equal(YokiFrameEngineErrorCodes.UNAVAILABLE, Send("object_list", Payload()).ErrorCode);
    }

    [Fact]
    public void Uninitialized_architecture_and_inspect_never_trigger_lazy_initialization()
    {
        Assert.False(new ArchitectureInspectRoot().TryResolve("UncreatedService", out _, out _));
        Assert.Equal(0, UncreatedArchitecture.InitCalls);
        ArchitectureRegistry.Register(typeof(CatalogArchitecture), mArchitecture, false,
            new[] { new KeyValuePair<Type, IService>(typeof(ProbeService), mArchitecture.GetService<ProbeService>()) });
        Assert.Empty(Query("object_list", Payload()).GetProperty("objects").EnumerateArray());
    }

    [Fact]
    public void Duplicate_contracts_do_not_duplicate_instances_and_snapshot_version_tracks_registration()
    {
        long before = mProvider.StateVersion;
        var service = mArchitecture.GetService<ProbeService>();
        Register(service, twoContracts: true);
        var item = Assert.Single(Query("object_list", Payload()).GetProperty("objects").EnumerateArray());
        Assert.Equal(2, item.GetProperty("contracts").GetArrayLength());
        Assert.True(mProvider.StateVersion > before);
        using var snapshot = JsonDocument.Parse(mProvider.CreateSnapshot("state"));
        Assert.True(snapshot.RootElement.GetProperty("objectCatalogAvailable").GetBoolean());
        Assert.False(snapshot.RootElement.TryGetProperty("entries", out _));
    }

    [Fact]
    public void Large_catalog_pages_are_byte_bounded_and_continue_without_duplicates()
    {
        var services = new ProbeService[120];
        var registrations = new List<KeyValuePair<Type, IService>>();
        for (int i = 0; i < services.Length; i++)
        {
            services[i] = new ProbeService();
            for (int contract = 0; contract < 16; contract++)
                registrations.Add(new(typeof(ProbeService), services[i]));
        }
        ArchitectureRegistry.Register(typeof(CatalogArchitecture), mArchitecture, true, registrations);
        var seen = new HashSet<string>();
        int offset = 0;
        string? revision = null;
        do
        {
            var extra = new System.Text.Json.Nodes.JsonObject { ["offset"] = offset, ["limit"] = 100 };
            if (revision != null) extra["catalogRevision"] = revision;
            var response = Query("object_list", Payload(extra));
            Assert.True(Encoding.UTF8.GetByteCount(response.GetRawText()) <= YokiFrameEngineObjectCatalog.MAX_RESPONSE_BYTES);
            foreach (var item in response.GetProperty("objects").EnumerateArray())
                Assert.True(seen.Add(item.GetProperty("objectId").GetString()!));
            int next = response.GetProperty("nextOffset").GetInt32();
            Assert.True(next < 0 || next > offset);
            offset = next;
            revision = response.GetProperty("catalogRevision").GetString();
        } while (offset >= 0);
        Assert.Equal(services.Length, seen.Count);
        GC.KeepAlive(services);
    }

    [Fact]
    public void A_new_provider_never_accepts_another_host_instances_handle()
    {
        string id = FirstId();
        var catalog = new YokiFrameEngineObjectCatalog(mEngine);
        var describe = catalog.CreateOperations()[1];
        var response = describe.Execute(EnginePipeline.CreateRequest("cli", "object_describe", Payload(new { objectId = id })));
        Assert.Equal("ObjectHandleExpired", response.ErrorCode);
    }

    private void Register(IService service, bool twoContracts = false)
    {
        var registrations = new List<KeyValuePair<Type, IService>> { new(typeof(ProbeService), service) };
        if (twoContracts) registrations.Add(new(typeof(IService), service));
        ArchitectureRegistry.Register(typeof(CatalogArchitecture), mArchitecture, true, registrations);
    }
    private string FirstId() => Query("object_list", Payload()).GetProperty("objects")[0].GetProperty("objectId").GetString()!;
    private static string Payload(object? extra = null, string target = "editor")
    {
        var node = extra == null ? new System.Text.Json.Nodes.JsonObject()
            : JsonSerializer.SerializeToNode(extra)!.AsObject();
        node["target"] = target;
        node["architecture"] = typeof(CatalogArchitecture).FullName;
        return node.ToJsonString();
    }
    private YokiFrameCommandResult Send(string action, string payload) =>
        mProvider.Handle(EnginePipeline.CreateRequest("cli", action, payload));
    private JsonElement Query(string action, string payload)
    {
        var response = Send(action, payload);
        Assert.True(response.IsSuccess, response.ErrorCode + " " + response.ErrorMessage);
        using var document = JsonDocument.Parse(response.ResultJson);
        return document.RootElement.Clone();
    }
    public void Dispose() => mArchitecture.Dispose();

    public sealed class CatalogArchitecture : Architecture<CatalogArchitecture>
    {
        protected override void OnInit() => Register(new ProbeService());
    }
    public sealed class UncreatedArchitecture : Architecture<UncreatedArchitecture>
    {
        public static int InitCalls;
        protected override void OnInit() => InitCalls++;
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ProbeAttribute : Attribute
    {
        public static int Calls;
        public ProbeAttribute() { Calls++; throw new InvalidOperationException("Attribute executed"); }
    }
    public sealed class ProbeService : AbstractService, IEnumerable
    {
        public static int Calls;
        protected override void OnInit() { }
        public int ExplosiveGetter { get { Calls++; throw new InvalidOperationException("Getter executed"); } }
        [Probe]
        public int Read(string name) { Calls++; return 1; }
        public int Read(int id) { Calls++; return 2; }
        public void Change(int amount = 7, string label = "hello", string? optional = null) { Calls++; }
        public Task<int> Later() { Calls++; return Task.FromResult(1); }
        public T Generic<T>(T value) { Calls++; return value; }
        public void ByRef(ref int value) { Calls++; }
        private void PrivateMethod() { Calls++; }
        public IEnumerator GetEnumerator() { Calls++; throw new InvalidOperationException("Enumerator executed"); }
        public override string ToString() { Calls++; throw new InvalidOperationException("ToString executed"); }
    }
}
