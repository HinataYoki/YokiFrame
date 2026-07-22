#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 将 Architecture 诊断注册表写成稳定 JSON，供统一 Snapshot 与只读命令复用。
    /// </summary>
    internal static class ArchitectureJsonWriter
    {
        /// <summary>
        /// 写入 Workbench 一次刷新需要的统计、实例和注册服务。
        /// </summary>
        /// <param name="architectures">Architecture 注册表副本。</param>
        /// <param name="diagnosticVersion">注册表诊断版本。</param>
        /// <returns>Architecture 工作台 payload。</returns>
        internal static string WriteWorkbench(
            IReadOnlyList<ArchitectureDebugInfo> architectures,
            long diagnosticVersion)
        {
            var builder = new StringBuilder(1024);
            CountState(architectures, out var aliveCount, out var serviceCount);
            builder.Append("{\"stats\":{");
            AppendLongProperty(builder, "diagnosticVersion", diagnosticVersion, false);
            AppendIntProperty(builder, "architectureCount", architectures.Count, true);
            AppendIntProperty(builder, "aliveCount", aliveCount, true);
            AppendIntProperty(builder, "serviceCount", serviceCount, true);
            builder.Append("},\"architectures\":[");
            AppendArchitectures(builder, architectures);
            builder.Append("],\"count\":");
            builder.Append(architectures.Count);
            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>
        /// 统计存活实例和全部服务数量，避免页面根据不完整列表重复推断。
        /// </summary>
        private static void CountState(
            IReadOnlyList<ArchitectureDebugInfo> architectures,
            out int aliveCount,
            out int serviceCount)
        {
            aliveCount = 0;
            serviceCount = 0;
            for (var index = 0; index < architectures.Count; index++)
            {
                ArchitectureDebugInfo architecture = architectures[index];
                if (architecture.IsAlive)
                {
                    aliveCount++;
                }

                serviceCount += architecture.ServiceCount;
            }
        }

        /// <summary>追加全部 Architecture 实例。</summary>
        private static void AppendArchitectures(
            StringBuilder builder,
            IReadOnlyList<ArchitectureDebugInfo> architectures)
        {
            for (var index = 0; index < architectures.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                AppendArchitecture(builder, architectures[index]);
            }
        }

        /// <summary>追加单个 Architecture 实例及其服务。</summary>
        private static void AppendArchitecture(StringBuilder builder, ArchitectureDebugInfo architecture)
        {
            builder.Append('{');
            AppendStringProperty(builder, "typeName", architecture.TypeName, false);
            AppendStringProperty(builder, "fullName", architecture.FullName, true);
            AppendStringProperty(builder, "createdAtUtc", architecture.CreatedAtUtc, true);
            AppendIntProperty(builder, "instanceHash", architecture.InstanceHash, true);
            AppendBoolProperty(builder, "isAlive", architecture.IsAlive, true);
            AppendBoolProperty(builder, "initialized", architecture.Initialized, true);
            AppendIntProperty(builder, "serviceCount", architecture.ServiceCount, true);
            builder.Append(",\"services\":[");
            AppendServices(builder, architecture.Services);
            builder.Append("]}");
        }

        /// <summary>追加当前 Architecture 的全部注册服务。</summary>
        private static void AppendServices(
            StringBuilder builder,
            IReadOnlyList<ArchitectureServiceDebugInfo> services)
        {
            for (var index = 0; index < services.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                AppendService(builder, services[index]);
            }
        }

        /// <summary>追加一个服务契约与实现快照。</summary>
        private static void AppendService(StringBuilder builder, ArchitectureServiceDebugInfo service)
        {
            builder.Append('{');
            AppendStringProperty(builder, "typeName", service.TypeName, false);
            AppendStringProperty(builder, "fullName", service.FullName, true);
            AppendStringProperty(builder, "implementationTypeName", service.ImplementationTypeName, true);
            AppendStringProperty(builder, "implementationFullName", service.ImplementationFullName, true);
            AppendBoolProperty(builder, "initialized", service.Initialized, true);
            AppendIntProperty(builder, "instanceHash", service.InstanceHash, true);
            builder.Append('}');
        }

        /// <summary>追加字符串属性并执行 JSON 转义。</summary>
        private static void AppendStringProperty(
            StringBuilder builder,
            string name,
            string value,
            bool prependComma)
        {
            AppendPropertyPrefix(builder, name, prependComma);
            builder.Append('"');
            builder.Append(JsonHelper.EscapeString(value));
            builder.Append('"');
        }

        /// <summary>追加整数属性。</summary>
        private static void AppendIntProperty(
            StringBuilder builder,
            string name,
            int value,
            bool prependComma)
        {
            AppendPropertyPrefix(builder, name, prependComma);
            builder.Append(value);
        }

        /// <summary>追加长整数属性。</summary>
        private static void AppendLongProperty(
            StringBuilder builder,
            string name,
            long value,
            bool prependComma)
        {
            AppendPropertyPrefix(builder, name, prependComma);
            builder.Append(value);
        }

        /// <summary>追加布尔属性。</summary>
        private static void AppendBoolProperty(
            StringBuilder builder,
            string name,
            bool value,
            bool prependComma)
        {
            AppendPropertyPrefix(builder, name, prependComma);
            builder.Append(value ? "true" : "false");
        }

        /// <summary>追加统一 JSON 属性前缀。</summary>
        private static void AppendPropertyPrefix(StringBuilder builder, string name, bool prependComma)
        {
            if (prependComma)
            {
                builder.Append(',');
            }

            builder.Append('"');
            builder.Append(name);
            builder.Append("\":");
        }
    }
}
#endif
