using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// SingletonKit 命令处理器：查询已注册单例的生命周期快照。
    /// Base 层不会反射扫描全部静态泛型实例，只读取各宿主登记到 SingletonRegistry 的实例。
    /// </summary>
    public sealed class SingletonKitCommandHandler : IKitCommandHandler
    {
        /// <inheritdoc />
        public string KitName => "SingletonKit";

        /// <inheritdoc />
        public string[] SupportedActions => new[] { "stats", "get_workbench_snapshot", "list_singletons", "get_singleton_detail" };

        /// <inheritdoc />
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "stats":
                    return GetStats();
                case "get_workbench_snapshot":
                    return GetWorkbenchSnapshot();
                case "list_singletons":
                    return ListSingletons();
                case "get_singleton_detail":
                    return GetSingletonDetail(payloadJson);
                default:
                    throw new NotSupportedException($"Unknown SingletonKit action '{action}'");
            }
        }

        private static string GetStats()
        {
            var infos = new List<SingletonDebugInfo>();
            SingletonRegistry.GetAll(infos);

            var aliveCount = 0;
            var disposedCount = 0;
            for (var i = 0; i < infos.Count; i++)
            {
                if (infos[i].IsAlive)
                    aliveCount++;
                else
                    disposedCount++;
            }

            var sb = new StringBuilder(128);
            sb.Append("{\"count\":");
            sb.Append(infos.Count);
            sb.Append(",\"aliveCount\":");
            sb.Append(aliveCount);
            sb.Append(",\"disposedCount\":");
            sb.Append(disposedCount);
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetWorkbenchSnapshot()
        {
            var stats = GetStats();
            var list = ListSingletons();

            var sb = new StringBuilder(stats.Length + list.Length + 32);
            sb.Append("{\"stats\":");
            sb.Append(stats);
            sb.Append(",\"list\":");
            sb.Append(list);
            sb.Append('}');
            return sb.ToString();
        }

        private static string ListSingletons()
        {
            var infos = new List<SingletonDebugInfo>();
            SingletonRegistry.GetAll(infos);

            var sb = new StringBuilder(256);
            sb.Append("{\"singletons\":[");
            for (int i = 0; i < infos.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendSingleton(sb, infos[i]);
            }
            sb.Append("],\"count\":");
            sb.Append(infos.Count);
            sb.Append('}');
            return sb.ToString();
        }

        private static string GetSingletonDetail(string payloadJson)
        {
            var fullName = JsonHelper.ExtractString(payloadJson, "fullName");
            var typeName = JsonHelper.ExtractString(payloadJson, "typeName");
            if (string.IsNullOrEmpty(fullName) && string.IsNullOrEmpty(typeName))
                throw new ArgumentException("Missing 'fullName' or 'typeName' in payload");

            var infos = new List<SingletonDebugInfo>();
            SingletonRegistry.GetAll(infos);
            for (var i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                if (!string.IsNullOrEmpty(fullName) && info.FullName != fullName)
                    continue;

                if (string.IsNullOrEmpty(fullName) && !string.IsNullOrEmpty(typeName) && info.TypeName != typeName)
                    continue;

                var sb = new StringBuilder(128);
                AppendSingleton(sb, info);
                return sb.ToString();
            }

            throw new KeyNotFoundException("Singleton '" + (fullName ?? typeName) + "' not found");
        }

        private static void AppendSingleton(StringBuilder sb, SingletonDebugInfo info)
        {
            sb.Append("{\"typeName\":\"");
            sb.Append(EscapeJson(info.TypeName));
            sb.Append("\",\"fullName\":\"");
            sb.Append(EscapeJson(info.FullName));
            sb.Append("\",\"backend\":\"");
            sb.Append(EscapeJson(info.Backend));
            sb.Append("\",\"source\":\"");
            sb.Append(EscapeJson(info.Source));
            sb.Append("\",\"createdAtUtc\":\"");
            sb.Append(EscapeJson(info.CreatedAtUtc));
            sb.Append("\",\"instanceHash\":");
            sb.Append(info.InstanceHash);
            sb.Append(",\"isAlive\":");
            sb.Append(info.IsAlive ? "true" : "false");
            sb.Append('}');
        }

        private static string EscapeJson(string s)
        {
            return JsonHelper.EscapeString(s);
        }
    }
}
