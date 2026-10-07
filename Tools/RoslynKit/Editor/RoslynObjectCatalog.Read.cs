#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed partial class RoslynObjectCatalog
    {
        /// <summary>
        /// 校验发现请求的目标、根和分页令牌。目标格式合法后才刷新 scope。
        /// </summary>
        /// <param name="root">载荷对象。</param>
        /// <param name="query">成功时的查询；失败时为 null。</param>
        /// <param name="error">失败结果；成功时为 null。</param>
        /// <returns>可以继续枚举时返回 true。字符串或数字非法时仍抛出 ArgumentException。</returns>
        private bool TryReadDiscoveryQuery(JsonElement root, out DiscoveryQuery query, out YokiFrameCommandResult error)
        {
            query = null;
            error = null;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = Invalid("Expected an object payload.");
                return false;
            }

            string target = ReadString(root, "target", required: true);
            if (!RoslynExecutionTargets.TryParse(target, out var parsed)
                || !RoslynExecutionTargets.IsSingleTarget(parsed))
            {
                error = Invalid("A single target is required.");
                return false;
            }

            target = RoslynExecutionTargets.Format(parsed);
            var state = mProvider.ReadDomainState();
            ObserveScope(state);
            if (!state.SessionIdentityAvailable || state.ActiveTarget != target
                || (mProvider.HostTargets & parsed) == 0 || state.IsCompiling)
            {
                error = YokiFrameCommandResult.Error(RoslynErrorCodes.UNAVAILABLE,
                    "Discovery requires the active host target and a published session identity.");
                return false;
            }

            string rootName = ReadString(root, "root");
            if (rootName.Length > 0 && rootName != "service")
            {
                error = Invalid("Only root=service is supported. Use scene_query for scene objects.");
                return false;
            }

            if (!TryAcceptCatalogRevision(root, out int offset, out long revision, out string revisionToken, out error))
            {
                return false;
            }

            query = new DiscoveryQuery(root, state, target, offset, ReadNumber(root, "limit", 25, 1, MAX_PAGE), revision, revisionToken);
            return true;
        }

        /// <summary>校验 offset 与目录修订令牌。令牌不符时要求从偏移 0 重新分页。</summary>
        /// <param name="root">载荷对象。</param>
        /// <param name="offset">请求的偏移。</param>
        /// <param name="revision">当前目录版本。</param>
        /// <param name="revisionToken">带 scope 的修订令牌。</param>
        /// <param name="error">令牌失效时的错误；通过时为 null。</param>
        /// <returns>可以按该偏移继续时返回 true。</returns>
        private bool TryAcceptCatalogRevision(
            JsonElement root,
            out int offset,
            out long revision,
            out string revisionToken,
            out YokiFrameCommandResult error)
        {
            error = null;
            offset = ReadNumber(root, "offset", 0, 0, MAX_CATALOG);
            revision = ArchitectureRegistry.DiagnosticVersion;
            revisionToken = mScope + ":" + revision.ToString(CultureInfo.InvariantCulture);
            string expected = ReadString(root, "catalogRevision");
            if ((offset > 0 && expected.Length == 0) || (expected.Length > 0 && expected != revisionToken))
            {
                error = YokiFrameCommandResult.Error("ObjectCatalogChanged", "Restart paging from offset 0.");
                return false;
            }

            return true;
        }

        /// <summary>按固定覆盖顺序收集成员本身的不支持原因，不含参数签名限制。</summary>
        /// <param name="member">成员。</param>
        /// <param name="method">方法；不是方法时为 null。</param>
        /// <param name="valueType">返回值、属性或字段类型。</param>
        /// <param name="parameters">方法参数或属性索引参数。</param>
        /// <returns>最后一条命中的原因；没有限制时为空字符串。</returns>
        private static string DescribeMemberSupport(
            MemberInfo member,
            MethodInfo method,
            Type valueType,
            ParameterInfo[] parameters)
        {
            string reason = "";
            if (method != null && method.ContainsGenericParameters) reason = "Open generic method requires type arguments.";
            if (valueType.IsPointer || valueType.IsByRef) reason = "Pointer/ref returns require specialized C# handling.";
            if (member.DeclaringType == null || !member.DeclaringType.IsVisible) reason = "Declaring type is not public.";
            if (parameters.Length > MAX_PARAMETERS) reason = "Parameter metadata limit exceeded.";
            return reason;
        }

        /// <summary>拼接截断后的签名，并在参数或签名超限时覆盖原因。</summary>
        /// <param name="member">成员。</param>
        /// <param name="valueType">返回值、属性或字段类型。</param>
        /// <param name="parameters">方法参数或属性索引参数。</param>
        /// <param name="reason">已有原因；参数或签名超限时被覆盖。</param>
        /// <returns>包含返回类型、名称和参数类型的签名文本。</returns>
        private static string BuildMemberSignature(
            MemberInfo member,
            Type valueType,
            ParameterInfo[] parameters,
            ref string reason)
        {
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
            return signature.ToString();
        }

        /// <summary>写成员元数据和参数列表。不调用 getter、setter 或方法。</summary>
        /// <param name="builder">JSON 构建器。</param>
        /// <param name="member">成员。</param>
        /// <param name="method">方法；不是方法时为 null。</param>
        /// <param name="property">属性；不是属性时为 null。</param>
        /// <param name="field">字段；不是字段时为 null。</param>
        /// <param name="valueType">返回值、属性或字段类型。</param>
        /// <param name="parameters">方法参数或属性索引参数。</param>
        /// <param name="id">成员标识哈希。</param>
        /// <param name="signature">已拼接的签名。</param>
        /// <param name="reason">不支持原因；空表示可描述。</param>
        private static void WriteMemberMetadata(
            RoslynJsonBuilder builder,
            MemberInfo member,
            MethodInfo method,
            PropertyInfo property,
            FieldInfo field,
            Type valueType,
            ParameterInfo[] parameters,
            string id,
            string signature,
            string reason)
        {
            builder.StartObject().Property("memberId", id)
                .Property("kind", method != null ? "method" : property != null ? "property" : "field")
                .Property("name", Bound(member.Name)).Property("declaringType", Bound(member.DeclaringType.FullName))
                .Property("signature", Bound(signature)).Property("valueType", Bound(valueType.FullName ?? valueType.Name))
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
    }
}
#endif
