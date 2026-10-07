using System.Collections;
using System.Text;
using System.Text.Json;
using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

[CollectionDefinition("ObjectDiscovery", DisableParallelization = true)]
public sealed class ObjectDiscoveryCollection { }

[Collection("ObjectDiscovery")]
public sealed class ObjectDiscoveryTests : IDisposable
{
    private readonly IArchitecture mArchitecture = CatalogArchitecture.Interface;
    private readonly StubEngineOperationProvider mEngine = new("Unity",
        RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play);
    private readonly RoslynKitProvider mProvider;
    /// <summary>固定无效配置下的提供者，并清零探针计数。不执行用户代码。</summary>
    public ObjectDiscoveryTests()
    {
        mEngine.State.SessionId = "object-session";
        mEngine.State.Generation = 4;
        mEngine.State.SessionIdentityAvailable = true;
        var settings = new StubEngineSettingsSource(RoslynSettingsSnapshot.InvalidConfig("broken json"));
        mProvider = new RoslynKitProvider(RoslynGate.CreateDefault(settings), mEngine, settings);
        ProbeAttribute.Calls = 0;
        ProbeService.Calls = 0;
    }

    [Fact]
    /// <summary>配置无效时仍可发现对象。重载、默认值和异步标记被读出，用户代码调用次数为零。</summary>
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
    /// <summary>同一注册的标识稳定。替换或释放后旧标识返回 ObjectHandleExpired。</summary>
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
    /// <summary>代数、会话、目标或生命周期变化都会使句柄过期。</summary>
    /// <param name="change">generation、session、target 或 lifecycle。</param>
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
    /// <summary>分页有界，续页必须带同一目录修订。修订变化返回 ObjectCatalogChanged。</summary>
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
        Assert.True(Encoding.UTF8.GetByteCount(first.GetRawText()) <= RoslynObjectCatalog.MAX_RESPONSE_BYTES);
    }

    [Theory]
    [InlineData("""{"target":"editor","root":"scene"}""")]
    [InlineData("""{"target":"editor","limit":101}""")]
    [InlineData("""{"target":"editor","offset":-1}""")]
    [InlineData("""{"target":"editor","limit":"1"}""")]
    [InlineData("""{"target":"editor|play"}""")]
    [InlineData("{}")]
    [InlineData("[]")]
    /// <summary>非法根、越界分页、组合目标和空载荷都返回 INVALID_PAYLOAD。不查询对象。</summary>
    /// <param name="payload">被拒绝的请求 JSON。</param>
    public void Invalid_payloads_are_rejected(string payload) =>
        Assert.Equal(RoslynErrorCodes.INVALID_PAYLOAD, Send("object_list", payload).ErrorCode);

    [Fact]
    /// <summary>非活动目标或未发布会话返回 UNAVAILABLE。不列出对象。</summary>
    public void Inactive_targets_and_unpublished_sessions_are_unavailable()
    {
        Assert.Equal(RoslynErrorCodes.UNAVAILABLE, Send("object_list", Payload(target: "play")).ErrorCode);
        mEngine.State.SessionIdentityAvailable = false;
        Assert.Equal(RoslynErrorCodes.UNAVAILABLE, Send("object_list", Payload()).ErrorCode);
    }

    [Fact]
    /// <summary>未创建架构的检查和重复登记都不会触发惰性初始化。初始化计数保持为零。</summary>
    public void Uninitialized_architecture_and_inspect_never_trigger_lazy_initialization()
    {
        Assert.False(new ArchitectureInspectRoot().TryResolve("UncreatedService", out _, out _));
        Assert.Equal(0, UncreatedArchitecture.InitCalls);
        ArchitectureRegistry.Register(typeof(CatalogArchitecture), mArchitecture, false,
            new[] { new KeyValuePair<Type, IService>(typeof(ProbeService), mArchitecture.GetService<ProbeService>()) });
        Assert.Empty(Query("object_list", Payload()).GetProperty("objects").EnumerateArray());
    }

    [Fact]
    /// <summary>同一实例的两份契约不产生两个对象。注册会推进状态版本，快照不含 entries。</summary>
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
    /// <summary>大目录分页受字节上限约束，续页不重复对象，最终数量等于注册实例数。</summary>
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
            Assert.True(Encoding.UTF8.GetByteCount(response.GetRawText()) <= RoslynObjectCatalog.MAX_RESPONSE_BYTES);
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
    /// <summary>新目录不接受另一个宿主实例的句柄，描述结果为 ObjectHandleExpired。</summary>
    public void A_new_provider_never_accepts_another_host_instances_handle()
    {
        string id = FirstId();
        var catalog = new RoslynObjectCatalog(mEngine);
        var describe = catalog.CreateOperations()[1];
        var response = describe.Execute(EnginePipeline.CreateRequest("cli", "object_describe", Payload(new { objectId = id })));
        Assert.Equal("ObjectHandleExpired", response.ErrorCode);
    }

    /// <summary>按替换语义登记服务。twoContracts 为 true 时再登记 IService 契约。</summary>
    /// <param name="service">要登记的服务。</param>
    /// <param name="twoContracts">是否额外登记 IService。</param>
    private void Register(IService service, bool twoContracts = false)
    {
        var registrations = new List<KeyValuePair<Type, IService>> { new(typeof(ProbeService), service) };
        if (twoContracts) registrations.Add(new(typeof(IService), service));
        ArchitectureRegistry.Register(typeof(CatalogArchitecture), mArchitecture, true, registrations);
    }
    /// <summary>读取当前列表第一个对象标识。列表必须成功且非空。</summary>
    /// <returns>对象标识。</returns>
    private string FirstId() => Query("object_list", Payload()).GetProperty("objects")[0].GetProperty("objectId").GetString()!;
    /// <summary>拼出带目标和架构名的请求 JSON。不发送。</summary>
    /// <param name="extra">附加字段，为空时只写目标和架构。</param>
    /// <param name="target">请求目标。</param>
    /// <returns>请求 JSON。</returns>
    private static string Payload(object? extra = null, string target = "editor")
    {
        var node = extra == null ? new System.Text.Json.Nodes.JsonObject()
            : JsonSerializer.SerializeToNode(extra)!.AsObject();
        node["target"] = target;
        node["architecture"] = typeof(CatalogArchitecture).FullName;
        return node.ToJsonString();
    }
    /// <summary>经当前提供者发送命令。不额外校验成功。</summary>
    /// <param name="action">操作名。</param>
    /// <param name="payload">请求 JSON。</param>
    /// <returns>命令结果。</returns>
    private YokiFrameCommandResult Send(string action, string payload) =>
        mProvider.Handle(EnginePipeline.CreateRequest("cli", action, payload));
    /// <summary>发送命令并克隆成功结果。失败时断言中断。</summary>
    /// <param name="action">操作名。</param>
    /// <param name="payload">请求 JSON。</param>
    /// <returns>脱离文档生命周期的结果元素。</returns>
    private JsonElement Query(string action, string payload)
    {
        var response = Send(action, payload);
        Assert.True(response.IsSuccess, response.ErrorCode + " " + response.ErrorMessage);
        using var document = JsonDocument.Parse(response.ResultJson);
        return document.RootElement.Clone();
    }
    /// <summary>释放测试架构。不重置引擎状态。</summary>
    public void Dispose() => mArchitecture.Dispose();

    public sealed class CatalogArchitecture : Architecture<CatalogArchitecture>
    {
        /// <summary>初始化时登记一个探针服务。不登记其他契约。</summary>
        protected override void OnInit() => Register(new ProbeService());
    }
    public sealed class UncreatedArchitecture : Architecture<UncreatedArchitecture>
    {
        public static int InitCalls;
        /// <summary>记录初始化次数。不登记服务，供惰性初始化断言使用。</summary>
        protected override void OnInit() => InitCalls++;
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ProbeAttribute : Attribute
    {
        public static int Calls;
        /// <summary>构造即计数并抛异常。发现流程不得执行该特性。</summary>
        public ProbeAttribute() { Calls++; throw new InvalidOperationException("Attribute executed"); }
    }
    public sealed class ProbeService : AbstractService, IEnumerable
    {
        public static int Calls;
        /// <summary>探针服务初始化保持为空，避免发现期间产生副作用。</summary>
        protected override void OnInit() { }
        public int ExplosiveGetter { get { Calls++; throw new InvalidOperationException("Getter executed"); } }
        [Probe]
        /// <summary>字符串重载。若被执行会增加调用计数。</summary>
        /// <param name="name">未使用的名称。</param>
        /// <returns>固定值 1。</returns>
        public int Read(string name) { Calls++; return 1; }
        /// <summary>整数重载，用于和字符串重载区分成员。若被执行会增加调用计数。</summary>
        /// <param name="id">未使用的标识。</param>
        /// <returns>固定值 2。</returns>
        public int Read(int id) { Calls++; return 2; }
        /// <summary>带默认参数的方法。发现只读取签名，不调用它。</summary>
        /// <param name="amount">默认 7。</param>
        /// <param name="label">默认 hello。</param>
        /// <param name="optional">默认可空。</param>
        public void Change(int amount = 7, string label = "hello", string? optional = null) { Calls++; }
        /// <summary>异步方法。发现应标成 async，但不得执行。</summary>
        /// <returns>已完成的任务。</returns>
        public Task<int> Later() { Calls++; return Task.FromResult(1); }
        /// <summary>泛型方法，发现应标成不受支持。不得执行。</summary>
        /// <param name="value">原样返回的值。</param>
        /// <returns>传入值。</returns>
        public T Generic<T>(T value) { Calls++; return value; }
        /// <summary>ref 参数方法，发现应标成不受支持。不得执行。</summary>
        /// <param name="value">引用参数。</param>
        public void ByRef(ref int value) { Calls++; }
        /// <summary>私有方法，发现结果里不得出现。不得执行。</summary>
        private void PrivateMethod() { Calls++; }
        /// <summary>枚举器不得在发现期间执行。调用会计数并抛异常。</summary>
        /// <returns>不会返回；调用即抛异常。</returns>
        public IEnumerator GetEnumerator() { Calls++; throw new InvalidOperationException("Enumerator executed"); }
        /// <summary>ToString 不得在发现期间执行。调用会计数并抛异常。</summary>
        /// <returns>不会返回；调用即抛异常。</returns>
        public override string ToString() { Calls++; throw new InvalidOperationException("ToString executed"); }
    }
}
