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
    public sealed class YokiFrameEngineObjectCatalog
    {
        public const int MAX_CATALOG = 4096;
        public const int MAX_PAGE = 100;
        public const int MAX_RESPONSE_BYTES = 64 * 1024;
        private const int MAX_TEXT = 1024;
        private const int MAX_PARAMETERS = 32;
        private const int MAX_SNAPSHOT_OBJECTS = 20;
        private readonly IYokiFrameEngineOperationProvider mProvider;
        private string mIdentity = string.Empty;
        private string mScope = Guid.NewGuid().ToString("N");

        public YokiFrameEngineObjectCatalog(IYokiFrameEngineOperationProvider provider) => mProvider = provider;

        public void Invalidate()
        {
            mIdentity = string.Empty;
            mScope = Guid.NewGuid().ToString("N");
        }

        public string ObserveScope(YokiFrameEngineDomainState state)
        {
            string identity = state.SessionId + ":" + state.Generation.ToString(CultureInfo.InvariantCulture) + ":" + state.ActiveTarget;
            if (mIdentity != identity) { mIdentity = identity; mScope = Guid.NewGuid().ToString("N"); }
            return mScope;
        }

        public IYokiFrameEngineOperation[] CreateOperations() => new IYokiFrameEngineOperation[]
        {
            new Operation(this, "object_list"), new Operation(this, "object_describe")
        };

        public void WriteSnapshot(YokiFrameEngineJsonBuilder builder, YokiFrameEngineDomainState state)
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

        private string ObjectId(ArchitectureLiveServiceInfo info) => mScope + ":" + info.RegistrationId;

        private YokiFrameCommandResult Execute(string action, YokiFrameCommandRequest request)
        {
            try
            {
                if (request.PayloadJson == null || request.PayloadJson.Length > 8192)
                    return Invalid("Discovery payload must be a JSON object of at most 8192 characters.");
                using (var document = JsonDocument.Parse(request.PayloadJson))
                {
                    var root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) return Invalid("Expected an object payload.");
                    string target = ReadString(root, "target", required: true);
                    if (!YokiFrameEngineExecutionTargets.TryParse(target, out var parsed)
                        || !YokiFrameEngineExecutionTargets.IsSingleTarget(parsed)) return Invalid("A single target is required.");
                    target = YokiFrameEngineExecutionTargets.Format(parsed);
                    var state = mProvider.ReadDomainState();
                    ObserveScope(state);
                    if (!state.SessionIdentityAvailable || state.ActiveTarget != target
                        || (mProvider.HostTargets & parsed) == 0 || state.IsCompiling)
                        return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.UNAVAILABLE,
                            "Discovery requires the active host target and a published session identity.");
                    string rootName = ReadString(root, "root");
                    if (rootName.Length > 0 && rootName != "service")
                        return Invalid("Only root=service is supported. Use scene_query for scene objects.");
                    int offset = ReadNumber(root, "offset", 0, 0, MAX_CATALOG);
                    int limit = ReadNumber(root, "limit", 25, 1, MAX_PAGE);
                    long revision = ArchitectureRegistry.DiagnosticVersion;
                    string revisionToken = mScope + ":" + revision.ToString(CultureInfo.InvariantCulture);
                    string expected = ReadString(root, "catalogRevision");
                    if ((offset > 0 && expected.Length == 0) || (expected.Length > 0 && expected != revisionToken))
                        return YokiFrameCommandResult.Error("ObjectCatalogChanged", "Restart paging from offset 0.");

                    var catalog = new List<ArchitectureLiveServiceInfo>(
                        ArchitectureRegistry.ReadLiveServiceCatalog(MAX_CATALOG, out bool capped));
                    catalog.Sort((a, b) => string.CompareOrdinal(a.RegistrationId, b.RegistrationId));
                    var rows = new List<Action<YokiFrameEngineJsonBuilder>>();
                    string describedObjectId = string.Empty;
                    if (action == "object_list")
                    {
                        string architecture = ReadString(root, "architecture");
                        string type = ReadString(root, "type");
                        foreach (var item in catalog)
                        {
                            if (architecture.Length > 0 && item.ArchitectureType.FullName != architecture) continue;
                            if (type.Length > 0 && !MatchesType(item, type)) continue;
                            rows.Add(builder => WriteObject(builder, item));
                        }
                    }
                    else
                    {
                        string id = ReadString(root, "objectId", required: true);
                        describedObjectId = id;
                        ArchitectureLiveServiceInfo selected = null;
                        foreach (var item in catalog)
                            if (ObjectId(item) == id) { selected = item; break; }
                        if (selected == null || !ArchitectureRegistry.TryResolveServiceRegistration(selected.RegistrationId, out _))
                            return YokiFrameCommandResult.Error("ObjectHandleExpired",
                                "The object is no longer registered in this session/target. Run object_list again.");
                        var members = new List<MemberInfo>();
                        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
                        foreach (var member in selected.ImplementationType.GetMembers(flags))
                        {
                            if (member is MethodInfo method && !method.IsSpecialName
                                || member is PropertyInfo || member is FieldInfo)
                                members.Add(member);
                        }
                        members.Sort((a, b) => string.CompareOrdinal(MemberId(a), MemberId(b)));
                        if (members.Count > MAX_CATALOG) { members.RemoveRange(MAX_CATALOG, members.Count - MAX_CATALOG); capped = true; }
                        foreach (var member in members) rows.Add(builder => WriteMember(builder, member, id));
                    }

                    if (revision != ArchitectureRegistry.DiagnosticVersion)
                        return YokiFrameCommandResult.Error("ObjectCatalogChanged", "Registration changed during discovery; restart from offset 0.");
                    var output = new YokiFrameEngineJsonBuilder().StartObject()
                        .Property("operation", action).Property("root", "service").Property("target", target)
                        .Property("sessionId", state.SessionId).Property("generation", state.Generation)
                        .Property("catalogRevision", revisionToken).Property("offset", offset)
                        .Property("objectId", describedObjectId)
                        .Property("total", rows.Count).Property("catalogTruncated", capped)
                        .Property("execution", "trustedCSharp");
                    var accepted = new List<Action<YokiFrameEngineJsonBuilder>>();
                    int headerBytes = Encoding.UTF8.GetByteCount(output.ToString()) + 256;
                    int bytes = headerBytes;
                    int next = Math.Min(offset, rows.Count);
                    while (next < rows.Count && accepted.Count < limit)
                    {
                        Action<YokiFrameEngineJsonBuilder> row = rows[next];
                        var probe = new YokiFrameEngineJsonBuilder();
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
            }
            catch (JsonException exception) { return Invalid(exception.Message); }
            catch (ArgumentException exception) { return Invalid(exception.Message); }
        }

        private static bool MatchesType(ArchitectureLiveServiceInfo item, string type)
        {
            if (item.ImplementationType.FullName == type) return true;
            foreach (var contract in item.ContractTypes) if (contract.FullName == type) return true;
            return false;
        }

        private void WriteObject(YokiFrameEngineJsonBuilder builder, ArchitectureLiveServiceInfo info)
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

        private static string MemberId(MemberInfo member) =>
            (member.DeclaringType.AssemblyQualifiedName ?? "") + ":" + member.Module.ModuleVersionId.ToString("N")
            + ":" + member.MetadataToken.ToString(CultureInfo.InvariantCulture);

        private static void WriteMember(YokiFrameEngineJsonBuilder builder, MemberInfo member, string objectId)
        {
            string id = YokiFrameEngineEvalRequest.ComputeSha256(objectId + ":" + MemberId(member));
            var method = member as MethodInfo;
            var property = member as PropertyInfo;
            var field = member as FieldInfo;
            Type valueType = method != null ? method.ReturnType : property != null ? property.PropertyType : field.FieldType;
            ParameterInfo[] parameters = method != null ? method.GetParameters()
                : property != null ? property.GetIndexParameters() : Array.Empty<ParameterInfo>();
            string reason = "";
            if (method != null && method.ContainsGenericParameters) reason = "Open generic method requires type arguments.";
            if (valueType.IsPointer || valueType.IsByRef) reason = "Pointer/ref returns require specialized C# handling.";
            if (member.DeclaringType == null || !member.DeclaringType.IsVisible) reason = "Declaring type is not public.";
            if (parameters.Length > MAX_PARAMETERS) reason = "Parameter metadata limit exceeded.";
            var signature = new StringBuilder(Bound(valueType.FullName ?? valueType.Name))
                .Append(' ').Append(Bound(member.Name)).Append('(');
            for (int i = 0; i < parameters.Length && i < MAX_PARAMETERS; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                if (parameterType.IsByRef || parameterType.IsPointer) reason = "ref/out/pointer parameters require specialized C# handling.";
                if ((parameterType.FullName ?? parameterType.Name).Length > MAX_TEXT
                    || (parameters[i].Name ?? "").Length > MAX_TEXT) reason = "Parameter metadata exceeds the text limit.";
                signature.Append(i == 0 ? "" : ", ").Append(Bound(parameterType.FullName ?? parameterType.Name));
            }
            signature.Append(')');
            if (signature.Length > MAX_TEXT || (valueType.AssemblyQualifiedName ?? "").Length > MAX_TEXT)
                reason = "Signature metadata exceeds the text limit.";
            builder.StartObject().Property("memberId", id)
                .Property("kind", method != null ? "method" : property != null ? "property" : "field")
                .Property("name", Bound(member.Name)).Property("declaringType", Bound(member.DeclaringType.FullName))
                .Property("signature", Bound(signature.ToString())).Property("valueType", Bound(valueType.FullName ?? valueType.Name))
                .Property("valueAssembly", Bound(valueType.Assembly.GetName().Name))
                .Property("genericArity", method != null && method.IsGenericMethod ? method.GetGenericArguments().Length : 0)
                .Property("async", IsAsync(valueType)).Property("supported", reason.Length == 0)
                .Property("reason", reason).Property("requiresExecution", true)
                .Property("static", method != null ? method.IsStatic : field != null ? field.IsStatic
                    : (property.GetGetMethod() ?? property.GetSetMethod()).IsStatic)
                .Property("canRead", field != null || property != null && property.GetGetMethod() != null)
                .Property("canWrite", property != null ? property.GetSetMethod() != null : field != null && !field.IsInitOnly && !field.IsLiteral)
                .Name("parameters").StartArray();
            for (int i = 0; i < parameters.Length && i < MAX_PARAMETERS; i++)
            {
                var parameter = parameters[i];
                ReadDefault(parameter, out string defaultKind, out string defaultValue);
                builder.StartObject().Property("name", Bound(parameter.Name))
                    .Property("type", Bound(parameter.ParameterType.FullName ?? parameter.ParameterType.Name))
                    .Property("assembly", Bound(parameter.ParameterType.Assembly.GetName().Name))
                    .Property("optional", parameter.IsOptional).Property("out", parameter.IsOut)
                    .Property("modifier", parameter.IsOut ? "out" : parameter.ParameterType.IsByRef
                        ? (parameter.IsIn ? "in" : "ref") : "")
                    .Property("defaultKind", defaultKind).Property("defaultValue", defaultValue).EndObject();
            }
            builder.EndArray().EndObject();
        }

        private static bool IsAsync(Type type)
        {
            string name = type.IsGenericType ? type.GetGenericTypeDefinition().FullName : type.FullName;
            return name == "System.Threading.Tasks.Task" || name == "System.Threading.Tasks.Task`1"
                || name == "System.Threading.Tasks.ValueTask" || name == "System.Threading.Tasks.ValueTask`1"
                || name == "Cysharp.Threading.Tasks.UniTask" || name == "Cysharp.Threading.Tasks.UniTask`1";
        }

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

        private static string Bound(string value) => value == null ? "" : value.Length <= MAX_TEXT ? value : "[metadata limit]";
        private static YokiFrameCommandResult Invalid(string message) =>
            YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, message);
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
        private static int ReadNumber(JsonElement root, string name, int fallback, int min, int max)
        {
            if (!root.TryGetProperty(name, out var value)) return fallback;
            if (!value.TryGetInt64(out long number) || number < min || number > max)
                throw new ArgumentException(name + " is outside the allowed range.");
            return (int)number;
        }

        private sealed class Operation : IYokiFrameEngineOperation
        {
            private readonly YokiFrameEngineObjectCatalog mOwner;
            internal Operation(YokiFrameEngineObjectCatalog owner, string action)
            {
                mOwner = owner;
                Descriptor = new YokiFrameEngineOperationDescriptor(action, YokiFrameCommandKind.ReadOnly,
                    isDiagnostic: true, targets: YokiFrameEngineExecutionTargets.ALL);
            }
            public YokiFrameEngineOperationDescriptor Descriptor { get; }
            public YokiFrameCommandResult Execute(YokiFrameCommandRequest request) => mOwner.Execute(Descriptor.Action, request);
        }
    }
}
#endif
