#if GODOT && TOOLS
using System;

namespace YokiFrame
{
    /// <summary>
    /// 把 Godot Runtime 的 System 只读诊断命令接入共享 Runtime dispatcher。
    /// </summary>
    internal sealed class GodotSystemCommandHandler : YokiFrameKitCommandHandler
    {
        /// <summary>Godot Runtime System 命令面的唯一声明；宿主策略由此聚合，禁止另建清单。</summary>
        public static readonly YokiFrameCommandDescriptor[] CommandDescriptors =
        {
            new("System", "ping", YokiFrameCommandKind.ReadOnly),
            new("System", "bridge_status", YokiFrameCommandKind.ReadOnly),
            new("System", "list_commands", YokiFrameCommandKind.ReadOnly)
        };

        private readonly Func<string> mCreateBridgeStatusJson;
        private readonly Func<string> mCreateCommandCatalogJson;
        private readonly Func<string> mCreatePingJson;

        /// <summary>
        /// 创建 Godot Runtime 的 System 只读 action handler。
        /// </summary>
        /// <param name="createPingJson">创建 ping 结果 JSON 的回调。</param>
        /// <param name="createBridgeStatusJson">创建 bridge_status 结果 JSON 的回调。</param>
        /// <param name="createCommandCatalogJson">创建 list_commands 结果 JSON 的回调。</param>
        public GodotSystemCommandHandler(
            Func<string> createPingJson,
            Func<string> createBridgeStatusJson,
            Func<string> createCommandCatalogJson)
            : base("System", CommandDescriptors)
        {
            mCreatePingJson = createPingJson ?? throw new ArgumentNullException(nameof(createPingJson));
            mCreateBridgeStatusJson = createBridgeStatusJson
                ?? throw new ArgumentNullException(nameof(createBridgeStatusJson));
            mCreateCommandCatalogJson = createCommandCatalogJson
                ?? throw new ArgumentNullException(nameof(createCommandCatalogJson));
        }

        /// <summary>
        /// 执行已通过策略与 action allowlist 的 System 命令。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <returns>可写入 terminal response 的结果。</returns>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            if (request.Action == "ping")
            {
                return YokiFrameCommandResult.Success(mCreatePingJson());
            }

            if (request.Action == "bridge_status")
            {
                return YokiFrameCommandResult.Success(mCreateBridgeStatusJson());
            }

            if (request.Action == "list_commands")
            {
                return YokiFrameCommandResult.Success(mCreateCommandCatalogJson());
            }

            return YokiFrameCommandResult.Error("UnknownCommand", "Unsupported Godot Runtime System command.");
        }
    }
}
#endif
