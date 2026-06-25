namespace YokiFrame
{
    /// <summary>
    /// 分发器接收到的标准命令上下文。
    /// </summary>
    public sealed class CommandBridgeCommand
    {
        /// <summary>
        /// 创建标准命令上下文。
        /// </summary>
        /// <param name="requestId">请求标识符。</param>
        /// <param name="engineId">目标引擎实例标识符。</param>
        /// <param name="source">命令来源。</param>
        /// <param name="kit">目标 Kit 名称。</param>
        /// <param name="action">目标动作名称。</param>
        /// <param name="payloadJson">命令载荷 JSON。</param>
        public CommandBridgeCommand(string requestId, string engineId, string source, string kit, string action, string payloadJson)
        {
            RequestId = requestId ?? string.Empty;
            EngineId = engineId ?? string.Empty;
            Source = source ?? string.Empty;
            Kit = kit ?? string.Empty;
            Action = action ?? string.Empty;
            PayloadJson = string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson;
        }

        /// <summary>
        /// 获取请求标识符。
        /// </summary>
        public string RequestId { get; }

        /// <summary>
        /// 获取目标引擎实例标识符。
        /// </summary>
        public string EngineId { get; }

        /// <summary>
        /// 获取命令来源。
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// 获取目标 Kit 名称。
        /// </summary>
        public string Kit { get; }

        /// <summary>
        /// 获取目标动作名称。
        /// </summary>
        public string Action { get; }

        /// <summary>
        /// 获取命令载荷 JSON。
        /// </summary>
        public string PayloadJson { get; }
    }
}
