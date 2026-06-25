namespace YokiFrame
{
    /// <summary>
    /// Kit 命令处理器接口（纯 C#，跨引擎）
    /// 注：输入输出均为 string（已序列化的 JSON），Base 层不依赖 JSON 库
    /// </summary>
    public interface IKitCommandHandler
    {
        string KitName { get; }
        string[] SupportedActions { get; }

        /// <summary>
        /// 处理命令
        /// </summary>
        /// <param name="action">动作名（如 "list_all", "get_state"）</param>
        /// <param name="payloadJson">JSON 格式的 payload 字符串</param>
        /// <returns>JSON 格式的结果字符串</returns>
        string HandleAction(string action, string payloadJson);
    }
}
