#if GODOT && TOOLS
using System;

namespace YokiFrame
{
    /// <summary>
    /// 把 Godot Editor 的三个 System 只读命令接入共享 dispatcher。
    /// </summary>
    internal sealed class GodotEditorSystemCommandHandler : YokiFrameKitCommandHandler
    {
        private readonly Func<string> mCreateBridgeStatusJson;
        private readonly Func<string> mCreateCommandCatalogJson;
        private readonly Func<string> mCreatePingJson;

        /// <summary>
        /// 创建 Godot Editor 的 System action handler。
        /// </summary>
        /// <param name="createPingJson">创建 ping JSON 的回调。</param>
        /// <param name="createBridgeStatusJson">创建 bridge_status JSON 的回调。</param>
        /// <param name="createCommandCatalogJson">创建 list_commands JSON 的回调。</param>
        public GodotEditorSystemCommandHandler(
            Func<string> createPingJson,
            Func<string> createBridgeStatusJson,
            Func<string> createCommandCatalogJson)
            : base("System", new[] { "ping", "bridge_status", "list_commands" })
        {
            mCreatePingJson = createPingJson ?? throw new ArgumentNullException(nameof(createPingJson));
            mCreateBridgeStatusJson = createBridgeStatusJson
                ?? throw new ArgumentNullException(nameof(createBridgeStatusJson));
            mCreateCommandCatalogJson = createCommandCatalogJson
                ?? throw new ArgumentNullException(nameof(createCommandCatalogJson));
        }

        /// <summary>
        /// 执行已通过 Editor policy 与 action allowlist 的 System 命令。
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

            return YokiFrameCommandResult.Error("UnknownCommand", "Unsupported Godot Editor System command.");
        }
    }
}
#endif
