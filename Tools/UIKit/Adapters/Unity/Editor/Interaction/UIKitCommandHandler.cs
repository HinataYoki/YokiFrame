#if UNITY_EDITOR
using System;

namespace YokiFrame
{
    /// <summary>提供 UIKit Runtime 只读查询和显式 Unity Editor 生成操作。</summary>
    internal sealed class UIKitCommandHandler : YokiFrameKitCommandHandler
    {
        internal const string KIT = "UIKit";
        internal const string STATS = "stats";
        internal const string GET_WORKBENCH_SNAPSHOT = "get_workbench_snapshot";
        internal const string GET_EDITOR_CONTEXT = "get_editor_context";
        internal const string CREATE_PANEL_PREFAB = "create_panel_prefab";
        internal const string GENERATE_CODE_FOR_SELECTION = "generate_code_for_selection";
        internal const string ADD_BIND_TO_SELECTION = "add_bind_to_selection";
        internal const string REMOVE_BIND_FROM_SELECTION = "remove_bind_from_selection";
        private static readonly string[] sSupportedActions =
        {
            STATS,
            GET_WORKBENCH_SNAPSHOT,
            GET_EDITOR_CONTEXT,
            CREATE_PANEL_PREFAB,
            GENERATE_CODE_FOR_SELECTION,
            ADD_BIND_TO_SELECTION,
            REMOVE_BIND_FROM_SELECTION,
        };

        /// <summary>创建只允许已登记 UIKit 查询和 Editor UserAction 的 handler。</summary>
        internal UIKitCommandHandler() : base(KIT, sSupportedActions) { }

        /// <summary>创建当前 UIKit 完整有界 state，查询不会创建 UIRoot。</summary>
        /// <returns>包含面板、栈、Root、缓存与模态状态的 JSON。</returns>
        internal string CreateWorkbenchSnapshot() => UIKitSnapshotWriter.WriteWorkbench();

        /// <summary>执行匹配命令，并把 payload、生成或选择异常转换为终态结果。</summary>
        /// <param name="request">已通过 Kit/action 匹配的请求。</param>
        /// <returns>只读状态或终态错误。</returns>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            try
            {
                string payload = ExecuteAction(request.Action, request.PayloadJson);
                return YokiFrameCommandResult.Success(payload);
            }
            catch (ArgumentException exception)
            {
                return YokiFrameCommandResult.Error("InvalidPayload", exception.Message);
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("UIKitEditorActionFailed", exception.Message);
            }
        }

        /// <summary>按 action 路由只读查询或显式 Editor 操作。</summary>
        private string ExecuteAction(string action, string payloadJson)
        {
            switch (action)
            {
                case STATS:
                    UIKitPayloadValidator.RequireEmptyObject(payloadJson);
                    return UIKitSnapshotWriter.WriteStats();
                case GET_WORKBENCH_SNAPSHOT:
                    UIKitPayloadValidator.RequireEmptyObject(payloadJson);
                    return CreateWorkbenchSnapshot();
                case GET_EDITOR_CONTEXT:
                    UIKitPayloadValidator.RequireEmptyObject(payloadJson);
                    return UIKitEditorContextWriter.Write();
                case CREATE_PANEL_PREFAB:
                    UIKitPayloadValidator.RequirePanelGenerationRequest(payloadJson);
                    return UIKitPanelPrefabService.CreatePanelPrefab(payloadJson);
                case GENERATE_CODE_FOR_SELECTION:
                    UIKitPayloadValidator.RequirePanelGenerationRequest(payloadJson);
                    return UIKitPanelPrefabService.GenerateCodeForSelection(payloadJson);
                case ADD_BIND_TO_SELECTION:
                    UIKitPayloadValidator.RequireSelectionContext(payloadJson);
                    return UIKitPanelPrefabService.AddBindToSelection(payloadJson);
                case REMOVE_BIND_FROM_SELECTION:
                    UIKitPayloadValidator.RequireSelectionContext(payloadJson);
                    return UIKitPanelPrefabService.RemoveBindFromSelection(payloadJson);
                default:
                    throw new InvalidOperationException("Unsupported UIKit action: " + action);
            }
        }
    }
}
#endif
