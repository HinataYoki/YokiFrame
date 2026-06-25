using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// System 级命令处理器：处理 ping、状态查询等基础命令。
    /// 为了效率，部分路径可由 KitCommandDispatcher 直接处理；本类保留为注册入口。
    /// </summary>
    public sealed class SystemCommandHandler : IKitCommandHandler
    {
        /// <inheritdoc />
        public string KitName => "System";

        /// <inheritdoc />
        public string[] SupportedActions => new[] { "ping", "status", "bridge_status", "list_commands", "open_code_location" };

        private readonly DateTime mStartTime = DateTime.UtcNow;
        private readonly Func<string> mBridgeStatusProvider;
        private readonly Func<string, int, string> mCodeLocationOpener;
        private readonly Func<string> mCommandCatalogProvider;

        /// <summary>
        /// 创建只处理基础 System 命令的处理器。
        /// </summary>
        public SystemCommandHandler()
        {
        }

        /// <summary>
        /// 创建带 bridge_status 提供者的 System 命令处理器。
        /// </summary>
        /// <param name="bridgeStatusProvider">返回 bridge_status 数据 JSON 的委托。</param>
        public SystemCommandHandler(Func<string> bridgeStatusProvider)
        {
            mBridgeStatusProvider = bridgeStatusProvider;
        }

        /// <summary>
        /// 创建带 bridge_status 和代码跳转能力的 System 命令处理器。
        /// </summary>
        /// <param name="bridgeStatusProvider">返回 bridge_status 数据 JSON 的委托。</param>
        /// <param name="codeLocationOpener">打开指定源码位置的委托。</param>
        public SystemCommandHandler(Func<string> bridgeStatusProvider, Func<string, int, string> codeLocationOpener)
        {
            mBridgeStatusProvider = bridgeStatusProvider;
            mCodeLocationOpener = codeLocationOpener;
        }

        /// <summary>
        /// 创建带 bridge_status、代码跳转和命令目录能力的 System 命令处理器。
        /// </summary>
        /// <param name="bridgeStatusProvider">返回 bridge_status 数据 JSON 的委托。</param>
        /// <param name="codeLocationOpener">打开指定源码位置的委托。</param>
        /// <param name="commandCatalogProvider">返回已注册命令目录 JSON 的委托。</param>
        public SystemCommandHandler(Func<string> bridgeStatusProvider, Func<string, int, string> codeLocationOpener, Func<string> commandCatalogProvider)
            : this(bridgeStatusProvider, codeLocationOpener)
        {
            mCommandCatalogProvider = commandCatalogProvider;
        }

        /// <inheritdoc />
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "ping":
                    return "{\"message\":\"pong\"}";
                case "status":
                    var uptime = (DateTime.UtcNow - mStartTime).TotalSeconds;
                    var sb = new StringBuilder(128);
                    sb.Append("{\"engine\":\"YokiFrame\",\"version\":\"2.0.0\",\"uptime\":");
                    sb.Append((long)uptime);
                    sb.Append('}');
                    return sb.ToString();
                case "bridge_status":
                    return mBridgeStatusProvider != null
                        ? mBridgeStatusProvider()
                        : "{\"available\":false,\"reason\":\"bridge status provider is not configured\"}";
                case "list_commands":
                    return mCommandCatalogProvider != null
                        ? mCommandCatalogProvider()
                        : "{\"kits\":[{\"kit\":\"System\",\"actions\":[{\"action\":\"ping\"},{\"action\":\"status\"},{\"action\":\"bridge_status\"},{\"action\":\"list_commands\"},{\"action\":\"open_code_location\"}]}]}";
                case "open_code_location":
                    return OpenCodeLocation(payloadJson);
                default:
                    throw new NotSupportedException($"Unknown System action '{action}'");
            }
        }

        private string OpenCodeLocation(string payloadJson)
        {
            if (mCodeLocationOpener == null)
                throw new InvalidOperationException("code location opener is not configured");

            var filePath = JsonHelper.ExtractString(payloadJson, "filePath");
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("Missing 'filePath' in payload");

            var line = 1;
            if (!JsonHelper.TryExtractInt(payloadJson, "line", out line))
            {
                var lineText = JsonHelper.ExtractString(payloadJson, "line");
                if (!string.IsNullOrEmpty(lineText))
                    throw new ArgumentException("Invalid 'line' in payload");

                line = 1;
            }

            if (line < 1)
                line = 1;

            return mCodeLocationOpener(filePath, line);
        }
    }
}
