using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// Architecture 命令处理器：查询当前存活架构实例和每个实例注册的服务。
    /// </summary>
    public sealed class ArchitectureCommandHandler : IKitCommandHandler
    {
        /// <inheritdoc />
        public string KitName => "Architecture";

        /// <inheritdoc />
        public string[] SupportedActions => new[] { "stats", "get_workbench_snapshot", "list_architectures", "get_architecture_detail" };

        /// <inheritdoc />
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "stats":
                    return GetStats();
                case "get_workbench_snapshot":
                    return GetWorkbenchSnapshot();
                case "list_architectures":
                    return ListArchitectures();
                case "get_architecture_detail":
                    return GetArchitectureDetail(payloadJson);
                default:
                    throw new NotSupportedException($"Unknown Architecture action '{action}'");
            }
        }

        private static string GetStats()
        {
            var infos = new List<ArchitectureDebugInfo>();
            ArchitectureRegistry.GetAll(infos);

            var aliveCount = 0;
            var initializedCount = 0;
            var serviceCount = 0;
            for (var i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                if (info.IsAlive)
                    aliveCount++;
                if (info.Initialized)
                    initializedCount++;
                serviceCount += info.ServiceCount;
            }

            var sb = new StringBuilder(128);
            sb.Append("{\"architectureCount\":");
            sb.Append(infos.Count);
            sb.Append(",\"aliveCount\":");
            sb.Append(aliveCount);
            sb.Append(",\"initializedCount\":");
            sb.Append(initializedCount);
            sb.Append(",\"serviceCount\":");
            sb.Append(serviceCount);
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetWorkbenchSnapshot()
        {
            var stats = GetStats();
            var list = ListArchitectures();

            var sb = new StringBuilder(stats.Length + list.Length + 32);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"list\":");
            sb.Append(list);
            sb.Append('}');
            return sb.ToString();
        }

        private static string ListArchitectures()
        {
            var infos = new List<ArchitectureDebugInfo>();
            ArchitectureRegistry.GetAll(infos);

            var sb = new StringBuilder(256);
            sb.Append("{\"architectures\":[");
            for (var i = 0; i < infos.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendArchitecture(sb, infos[i]);
            }
            sb.Append("],\"count\":");
            sb.Append(infos.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetArchitectureDetail(string payloadJson)
        {
            var fullName = JsonHelper.ExtractString(payloadJson, "fullName");
            var typeName = JsonHelper.ExtractString(payloadJson, "typeName");
            if (string.IsNullOrEmpty(fullName) && string.IsNullOrEmpty(typeName))
                throw new ArgumentException("Missing 'fullName' or 'typeName' in payload");

            var infos = new List<ArchitectureDebugInfo>();
            ArchitectureRegistry.GetAll(infos);
            for (var i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                if (!string.IsNullOrEmpty(fullName) && info.FullName != fullName)
                    continue;

                if (string.IsNullOrEmpty(fullName) && !string.IsNullOrEmpty(typeName) && info.TypeName != typeName)
                    continue;

                var sb = new StringBuilder(128);
                AppendArchitecture(sb, info);
                return sb.ToString();
            }

            throw new KeyNotFoundException("Architecture '" + (fullName ?? typeName) + "' not found");
        }

        private static void AppendArchitecture(StringBuilder sb, ArchitectureDebugInfo info)
        {
            sb.Append("{\"typeName\":\"");
            sb.Append(EscapeJson(info.TypeName));
            sb.Append("\",\"fullName\":\"");
            sb.Append(EscapeJson(info.FullName));
            sb.Append("\",\"createdAtUtc\":\"");
            sb.Append(EscapeJson(info.CreatedAtUtc));
            sb.Append("\",\"instanceHash\":");
            sb.Append(info.InstanceHash);
            sb.Append(",\"isAlive\":");
            sb.Append(info.IsAlive ? "true" : "false");
            sb.Append(",\"initialized\":");
            sb.Append(info.Initialized ? "true" : "false");
            sb.Append(",\"serviceCount\":");
            sb.Append(info.ServiceCount);
            sb.Append(",\"services\":[");
            for (var i = 0; i < info.Services.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendService(sb, info.Services[i]);
            }
            sb.Append("]}");
        }

        private static void AppendService(StringBuilder sb, ArchitectureServiceDebugInfo service)
        {
            sb.Append("{\"typeName\":\"");
            sb.Append(EscapeJson(service.TypeName));
            sb.Append("\",\"fullName\":\"");
            sb.Append(EscapeJson(service.FullName));
            sb.Append("\",\"implementationTypeName\":\"");
            sb.Append(EscapeJson(service.ImplementationTypeName));
            sb.Append("\",\"implementationFullName\":\"");
            sb.Append(EscapeJson(service.ImplementationFullName));
            sb.Append("\",\"instanceHash\":");
            sb.Append(service.InstanceHash);
            sb.Append(",\"initialized\":");
            sb.Append(service.Initialized ? "true" : "false");
            sb.Append('}');
        }

        private static string EscapeJson(string s)
        {
            return JsonHelper.EscapeString(s);
        }
    }
}
