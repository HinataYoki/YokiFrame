#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>Metadata-only discovery over live registrations, not an object execution endpoint.</summary>
    public sealed partial class RoslynObjectCatalog
    {
        public const int MAX_CATALOG = 4096;
        public const int MAX_PAGE = 100;
        public const int MAX_RESPONSE_BYTES = 64 * 1024;
        private const int MAX_TEXT = 1024;
        private const int MAX_PARAMETERS = 32;
        private const int MAX_SNAPSHOT_OBJECTS = 20;
        private readonly IRoslynOperationProvider mProvider;
        private string mIdentity = string.Empty;
        /// <summary>当前目录作用域。刷新后更换，避免旧句柄继续命中已失效对象。</summary>
        private string mScope = Guid.NewGuid().ToString("N");

        /// <summary>创建对象目录，只保存提供者引用，不立即读取注册表。</summary>
        /// <param name="provider">提供域状态与宿主目标的操作入口。</param>
        public RoslynObjectCatalog(IRoslynOperationProvider provider) => mProvider = provider;

        /// <summary>清空身份缓存并更换 scope，使已发出的 objectId 失效。</summary>
        public void Invalidate()
        {
            mIdentity = string.Empty;
            mScope = Guid.NewGuid().ToString("N");
        }

        /// <summary>身份变化时更换 scope，并返回当前 scope。不读取服务注册。</summary>
        /// <param name="state">当前域状态。</param>
        /// <returns>本次发现使用的 scope。</returns>
        public string ObserveScope(RoslynDomainState state)
        {
            string identity = state.SessionId + ":" + state.Generation.ToString(CultureInfo.InvariantCulture) + ":" + state.ActiveTarget;
            if (mIdentity != identity) { mIdentity = identity; mScope = Guid.NewGuid().ToString("N"); }
            return mScope;
        }

        /// <summary>创建 object_list 与 object_describe。两者都回调本目录，不复制注册数据。</summary>
        /// <returns>两个只读诊断操作。</returns>
        public IRoslynOperation[] CreateOperations() => new IRoslynOperation[]
        {
            new Operation(this, "object_list"), new Operation(this, "object_describe")
        };

        /// <summary>刷新 scope 后把最多 20 个活动服务摘要写入 snapshot。身份不可用时不写对象。</summary>
        /// <param name="builder">snapshot JSON。</param>
        /// <param name="state">当前域状态。</param>
        public void WriteSnapshot(RoslynJsonBuilder builder, RoslynDomainState state)
        {
            ObserveScope(state);
            bool available = state.SessionIdentityAvailable;
            var catalog = ArchitectureRegistry.ReadLiveServiceCatalog(MAX_CATALOG, out bool capped);
            builder.Property("objectCatalogAvailable", available)
                .Property("objectCatalogRoot", "service").Property("objectCatalogRevision", ArchitectureRegistry.DiagnosticVersion)
                .Property("objectCount", available ? catalog.Count : 0)
                .Property("truncatedObjects", capped || catalog.Count > MAX_SNAPSHOT_OBJECTS).Name("objects").StartArray();
            if (available)
                for (int i = 0; i < catalog.Count && i < MAX_SNAPSHOT_OBJECTS; i++) WriteObject(builder, catalog[i]);
            builder.EndArray();
        }

        /// <summary>用当前 scope 和注册标识组成 objectId。不检查该注册是否仍存活。</summary>
        /// <param name="info">活动服务信息。</param>
        /// <returns>scope 前缀的对象标识。</returns>
        private string ObjectId(ArchitectureLiveServiceInfo info) => mScope + ":" + info.RegistrationId;

        /// <summary>执行对象目录查询。只读元数据，不调用 getter。</summary>
        /// <param name="action">object_list 或 object_describe。</param>
        /// <param name="request">命令请求。</param>
        /// <returns>分页后的目录结果。</returns>
        private YokiFrameCommandResult Execute(string action, YokiFrameCommandRequest request)
        {
            try
            {
                if (!TryReadRequest(request, out var query, out var error)) return error;
                var rows = new List<Action<RoslynJsonBuilder>>();
                string describedObjectId = string.Empty;
                bool capped;
                if (action == "object_list") CollectObjects(query, rows, out capped);
                else if (!TryCollectMembers(query, rows, out describedObjectId, out capped, out error)) return error;
                if (query.Revision != ArchitectureRegistry.DiagnosticVersion)
                    return YokiFrameCommandResult.Error("ObjectCatalogChanged", "Registration changed during discovery; restart from offset 0.");
                return Page(action, query, describedObjectId, rows, capped);
            }
            catch (JsonException exception) { return Invalid(exception.Message); }
            catch (ArgumentException exception) { return Invalid(exception.Message); }
        }

        /// <summary>校验目标、会话和分页令牌。</summary>
        /// <param name="request">命令请求。</param>
        /// <param name="query">解析后的查询。</param>
        /// <param name="error">失败结果。</param>
        /// <returns>可以继续查询时返回 true。</returns>
        private bool TryReadRequest(YokiFrameCommandRequest request, out DiscoveryQuery query, out YokiFrameCommandResult error)
        {
            query = null;
            error = null;
            if (request.PayloadJson == null || request.PayloadJson.Length > 8192)
            {
                error = Invalid("Discovery payload must be a JSON object of at most 8192 characters.");
                return false;
            }

            using (var document = JsonDocument.Parse(request.PayloadJson))
            {
                return TryReadDiscoveryQuery(document.RootElement, out query, out error);
            }
        }

        /// <summary>按架构和类型过滤活动服务。</summary>
        /// <param name="query">查询条件。</param>
        /// <param name="rows">结果行。</param>
        /// <param name="capped">目录是否被上限截断。</param>
        private void CollectObjects(DiscoveryQuery query, List<Action<RoslynJsonBuilder>> rows, out bool capped)
        {
            var catalog = ReadCatalog(out capped);
            string architecture = ReadString(query.Root, "architecture");
            string type = ReadString(query.Root, "type");
            foreach (var item in catalog)
            {
                if (architecture.Length > 0 && item.ArchitectureType.FullName != architecture) continue;
                if (type.Length > 0 && !MatchesType(item, type)) continue;
                ArchitectureLiveServiceInfo captured = item;
                rows.Add(builder => WriteObject(builder, captured));
            }
        }

        /// <summary>读取一个仍存活对象的公开成员元数据。</summary>
        /// <param name="query">查询条件。</param>
        /// <param name="rows">成员行。</param>
        /// <param name="objectId">请求的对象标识。</param>
        /// <param name="capped">成员是否被上限截断。</param>
        /// <param name="error">对象失效时的错误。</param>
        /// <returns>对象仍注册时返回 true。</returns>
        private bool TryCollectMembers(
            DiscoveryQuery query, List<Action<RoslynJsonBuilder>> rows, out string objectId,
            out bool capped, out YokiFrameCommandResult error)
        {
            objectId = ReadString(query.Root, "objectId", required: true);
            error = null;
            capped = false;
            var catalog = ReadCatalog(out bool catalogCapped);
            ArchitectureLiveServiceInfo selected = null;
            foreach (var item in catalog)
                if (ObjectId(item) == objectId) { selected = item; break; }
            if (selected == null || !ArchitectureRegistry.TryResolveServiceRegistration(selected.RegistrationId, out _))
            {
                error = YokiFrameCommandResult.Error("ObjectHandleExpired",
                    "The object is no longer registered in this session/target. Run object_list again.");
                return false;
            }

            var members = PublicMembers(selected.ImplementationType);
            capped = catalogCapped || members.Count > MAX_CATALOG;
            if (members.Count > MAX_CATALOG) members.RemoveRange(MAX_CATALOG, members.Count - MAX_CATALOG);
            foreach (var member in members)
            {
                MemberInfo captured = member;
                string id = objectId;
                rows.Add(builder => WriteMember(builder, captured, id));
            }

            return true;
        }

        /// <summary>读取并排序活动服务目录。</summary>
        /// <param name="capped">是否达到目录上限。</param>
        /// <returns>按注册标识排序的目录。</returns>
        private static List<ArchitectureLiveServiceInfo> ReadCatalog(out bool capped)
        {
            var catalog = new List<ArchitectureLiveServiceInfo>(
                ArchitectureRegistry.ReadLiveServiceCatalog(MAX_CATALOG, out capped));
            catalog.Sort(static (left, right) => string.CompareOrdinal(left.RegistrationId, right.RegistrationId));
            return catalog;
        }

        /// <summary>收集可描述的公开方法、属性和字段。</summary>
        /// <param name="type">实现类型。</param>
        /// <returns>按成员标识排序的成员。</returns>
        private static List<MemberInfo> PublicMembers(Type type)
        {
            var members = new List<MemberInfo>();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
            foreach (var member in type.GetMembers(flags))
            {
                if (member is MethodInfo method && !method.IsSpecialName
                    || member is PropertyInfo || member is FieldInfo)
                    members.Add(member);
            }

            members.Sort(static (left, right) => string.CompareOrdinal(MemberId(left), MemberId(right)));
            return members;
        }

        /// <summary>按响应字节和页大小截取结果。</summary>
        /// <param name="action">操作名。</param>
        /// <param name="query">查询条件。</param>
        /// <param name="objectId">描述操作的对象标识。</param>
        /// <param name="rows">全部结果行。</param>
        /// <param name="capped">目录是否已截断。</param>
        /// <returns>成功结果。</returns>
        private static YokiFrameCommandResult Page(
            string action, DiscoveryQuery query, string objectId,
            List<Action<RoslynJsonBuilder>> rows, bool capped)
        {
            var output = new RoslynJsonBuilder().StartObject()
                .Property("operation", action).Property("root", "service").Property("target", query.Target)
                .Property("sessionId", query.State.SessionId).Property("generation", query.State.Generation)
                .Property("catalogRevision", query.RevisionToken).Property("offset", query.Offset)
                .Property("objectId", objectId)
                .Property("total", rows.Count).Property("catalogTruncated", capped)
                .Property("execution", "trustedCSharp");
            var accepted = new List<Action<RoslynJsonBuilder>>();
            int headerBytes = Encoding.UTF8.GetByteCount(output.ToString()) + 256;
            int bytes = headerBytes;
            int next = Math.Min(query.Offset, rows.Count);
            while (next < rows.Count && accepted.Count < query.Limit)
            {
                Action<RoslynJsonBuilder> row = rows[next];
                var probe = new RoslynJsonBuilder();
                row(probe);
                int size = Encoding.UTF8.GetByteCount(probe.ToString()) + 1;
                if (size > MAX_RESPONSE_BYTES - headerBytes)
                {
                    int rowIndex = next;
                    row = builder => builder.StartObject().Property("index", rowIndex)
                        .Property("supported", false).Property("reason", "Row metadata exceeds the response limit.")
                        .EndObject();
                    size = 256;
                }

                if (bytes + size > MAX_RESPONSE_BYTES) break;
                bytes += size;
                accepted.Add(row);
                next++;
            }

            output.Property("truncated", next < rows.Count || capped)
                .Property("nextOffset", next < rows.Count ? next : -1)
                .Name(action == "object_list" ? "objects" : "members").StartArray();
            foreach (var row in accepted) row(output);
            return YokiFrameCommandResult.Success(output.EndArray().EndObject().ToString());
        }

        /// <summary>一次对象目录查询的已校验参数。</summary>
        private sealed class DiscoveryQuery
        {
            /// <summary>创建查询参数。</summary>
            /// <param name="root">原始 JSON。</param>
            /// <param name="state">当前宿主状态。</param>
            /// <param name="target">单一目标。</param>
            /// <param name="offset">分页偏移。</param>
            /// <param name="limit">页大小。</param>
            /// <param name="revision">目录版本。</param>
            /// <param name="revisionToken">带作用域的目录令牌。</param>
            internal DiscoveryQuery(
                JsonElement root, RoslynDomainState state, string target,
                int offset, int limit, long revision, string revisionToken)
            {
                Root = root;
                State = state;
                Target = target;
                Offset = offset;
                Limit = limit;
                Revision = revision;
                RevisionToken = revisionToken;
            }

            /// <summary>原始 JSON。</summary>
            internal JsonElement Root { get; }

            /// <summary>当前宿主状态。</summary>
            internal RoslynDomainState State { get; }

            /// <summary>单一目标。</summary>
            internal string Target { get; }

            /// <summary>分页偏移。</summary>
            internal int Offset { get; }

            /// <summary>页大小。</summary>
            internal int Limit { get; }

            /// <summary>目录版本。</summary>
            internal long Revision { get; }

            /// <summary>带作用域的目录令牌。</summary>
            internal string RevisionToken { get; }
        }

        /// <summary>实现类型或任一契约类型的 FullName 与过滤值相同即命中。</summary>
        /// <param name="item">活动服务。</param>
        /// <param name="type">要匹配的类型全名。</param>
        /// <returns>命中时返回 true。</returns>
        private static bool MatchesType(ArchitectureLiveServiceInfo item, string type)
        {
            if (item.ImplementationType.FullName == type) return true;
            foreach (var contract in item.ContractTypes) if (contract.FullName == type) return true;
            return false;
        }

        /// <summary>写一条服务摘要。过长或不可见类型标为不支持，契约最多写 32 个。</summary>
        /// <param name="builder">JSON 构建器。</param>
        /// <param name="info">活动服务。</param>
        private void WriteObject(RoslynJsonBuilder builder, ArchitectureLiveServiceInfo info)
        {
            string type = info.ImplementationType.FullName ?? info.ImplementationType.Name;
            string architecture = info.ArchitectureType.FullName ?? info.ArchitectureType.Name;
            bool supported = type.Length <= MAX_TEXT && architecture.Length <= MAX_TEXT && info.ImplementationType.IsVisible;
            builder.StartObject().Property("objectId", ObjectId(info))
                .Property("type", Bound(type)).Property("assembly", Bound(info.ImplementationType.Assembly.GetName().Name))
                .Property("architecture", Bound(architecture)).Property("supported", supported)
                .Property("reason", supported ? "" : "Type is not public or metadata exceeds the text limit.")
                .Name("contracts").StartArray();
            for (int i = 0; i < info.ContractTypes.Count && i < MAX_PARAMETERS; i++) builder.String(Bound(info.ContractTypes[i].FullName));
            builder.EndArray().Property("contractsTruncated", info.ContractTypes.Count > MAX_PARAMETERS).EndObject();
        }

        /// <summary>用声明类型、模块和元数据记号组成稳定成员键。</summary>
        /// <param name="member">成员。</param>
        /// <returns>供哈希使用的成员键。</returns>
        private static string MemberId(MemberInfo member) =>
            (member.DeclaringType.AssemblyQualifiedName ?? "") + ":" + member.Module.ModuleVersionId.ToString("N")
            + ":" + member.MetadataToken.ToString(CultureInfo.InvariantCulture);

        /// <summary>写成员元数据。不调用 getter 或方法，只反映可见性和签名限制。</summary>
        /// <param name="builder">JSON 构建器。</param>
        /// <param name="member">方法、属性或字段。</param>
        /// <param name="objectId">所属对象标识，参与 memberId 哈希。</param>
        private static void WriteMember(RoslynJsonBuilder builder, MemberInfo member, string objectId)
        {
            string id = RoslynEvalRequest.ComputeSha256(objectId + ":" + MemberId(member));
            var method = member as MethodInfo;
            var property = member as PropertyInfo;
            var field = member as FieldInfo;
            Type valueType = method != null ? method.ReturnType : property != null ? property.PropertyType : field.FieldType;
            ParameterInfo[] parameters = method != null ? method.GetParameters()
                : property != null ? property.GetIndexParameters() : Array.Empty<ParameterInfo>();
            string reason = DescribeMemberSupport(member, method, valueType, parameters);
            string signature = BuildMemberSignature(member, valueType, parameters, ref reason);
            WriteMemberMetadata(builder, member, method, property, field, valueType, parameters, id, signature, reason);
        }

        /// <summary>按类型全名判断返回值是否为 Task、ValueTask 或 UniTask。</summary>
        /// <param name="type">返回值或属性类型。</param>
        /// <returns>是已知异步类型时返回 true。</returns>
        private static bool IsAsync(Type type)
        {
            string name = type.IsGenericType ? type.GetGenericTypeDefinition().FullName : type.FullName;
            return name == "System.Threading.Tasks.Task" || name == "System.Threading.Tasks.Task`1"
                || name == "System.Threading.Tasks.ValueTask" || name == "System.Threading.Tasks.ValueTask`1"
                || name == "Cysharp.Threading.Tasks.UniTask" || name == "Cysharp.Threading.Tasks.UniTask`1";
        }

        /// <summary>读取参数默认值。只保留 null、基元、decimal 和字符串，超长文本不写出。</summary>
        /// <param name="parameter">参数。</param>
        /// <param name="kind">none、null、invariant、unavailable 或 metadataLimit。</param>
        /// <param name="text">可展示的默认值文本。</param>
        private static void ReadDefault(ParameterInfo parameter, out string kind, out string text)
        {
            kind = "none";
            text = "";
            if (!parameter.HasDefaultValue) return;
            object value = parameter.RawDefaultValue;
            if (value == null) { kind = "null"; return; }
            Type type = value.GetType();
            if (!(type.IsPrimitive || value is decimal || value is string)) { kind = "unavailable"; return; }
            kind = "invariant";
            text = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (text.Length > MAX_TEXT) { text = ""; kind = "metadataLimit"; }
        }

        /// <summary>把文本限制在 MAX_TEXT。null 变为空，超长变为固定占位。</summary>
        /// <param name="value">原始文本。</param>
        /// <returns>可写入响应的文本。</returns>
        private static string Bound(string value) => value == null ? "" : value.Length <= MAX_TEXT ? value : "[metadata limit]";

        /// <summary>构造无效载荷错误，错误码为 INVALID_PAYLOAD。</summary>
        /// <param name="message">错误说明。</param>
        /// <returns>失败的命令结果。</returns>
        private static YokiFrameCommandResult Invalid(string message) =>
            YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, message);

        /// <summary>读取必填或可选字符串。类型不对或超过 1024 字符时抛出 ArgumentException。</summary>
        /// <param name="root">JSON 对象。</param>
        /// <param name="name">属性名。</param>
        /// <param name="required">缺失时是否抛出。</param>
        /// <returns>字符串值。</returns>
        private static string ReadString(JsonElement root, string name, bool required = false)
        {
            if (!root.TryGetProperty(name, out var value))
            {
                if (required) throw new ArgumentException(name + " is required.");
                return "";
            }
            if (value.ValueKind != JsonValueKind.String || value.GetString().Length > MAX_TEXT)
                throw new ArgumentException(name + " must be a string of at most 1024 characters.");
            return value.GetString();
        }
        /// <summary>读取整数。缺失时用默认值；超出闭区间时抛出 ArgumentException。</summary>
        /// <param name="root">JSON 对象。</param>
        /// <param name="name">属性名。</param>
        /// <param name="fallback">缺失时的值。</param>
        /// <param name="min">允许的最小值。</param>
        /// <param name="max">允许的最大值。</param>
        /// <returns>区间内的整数。</returns>
        private static int ReadNumber(JsonElement root, string name, int fallback, int min, int max)
        {
            if (!root.TryGetProperty(name, out var value)) return fallback;
            if (!value.TryGetInt64(out long number) || number < min || number > max)
                throw new ArgumentException(name + " is outside the allowed range.");
            return (int)number;
        }

        private sealed class Operation : IRoslynOperation
        {
            private readonly RoslynObjectCatalog mOwner;

            /// <summary>绑定目录和动作名，并标记为全目标只读诊断。</summary>
            /// <param name="owner">执行查询的目录。</param>
            /// <param name="action">object_list 或 object_describe。</param>
            internal Operation(RoslynObjectCatalog owner, string action)
            {
                mOwner = owner;
                Descriptor = new RoslynOperationDescriptor(action, YokiFrameCommandKind.ReadOnly,
                    isDiagnostic: true, targets: RoslynExecutionTargets.ALL);
            }
            public RoslynOperationDescriptor Descriptor { get; }

            /// <summary>把请求交给目录执行，不在操作实例上保存结果。</summary>
            /// <param name="request">命令请求。</param>
            /// <returns>目录查询结果。</returns>
            public YokiFrameCommandResult Execute(YokiFrameCommandRequest request) => mOwner.Execute(Descriptor.Action, request);
        }
    }
}
#endif
