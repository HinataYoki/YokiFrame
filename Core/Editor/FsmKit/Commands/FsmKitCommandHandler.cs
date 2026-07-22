#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 提供 FsmKit 实例注册、只读查询和 Workbench snapshot JSON 入口。
    /// </summary>
    public sealed class FsmKitCommandHandler : YokiFrameKitCommandHandler
    {
        private const string KIT_NAME = "FsmKit";
        private static readonly string[] sSupportedActions =
        {
            "list_all",
            "get_state",
            "get_history",
            "get_state_events",
            "get_workbench_snapshot"
        };

        /// <summary>可选历史 provider；返回空时使用 Core 内建记录。</summary>
        public static Func<string, string> HistoryProvider;

        /// <summary>可选状态生命周期 provider；返回空时使用 Core 内建记录。</summary>
        public static Func<string, string> StateLifecycleProvider;

        /// <summary>创建支持五个只读 action 的 FsmKit handler。</summary>
        public FsmKitCommandHandler() : base(KIT_NAME, sSupportedActions) { }

        /// <summary>获取稳定 Kit 名称。</summary>
        public string KitName => KIT_NAME;

        /// <summary>获取支持 action 的独立数组，调用方修改不会影响 handler。</summary>
        public string[] SupportedActions => (string[])sSupportedActions.Clone();

        /// <summary>
        /// 注册 FSM；重复注册同一实例时保留 instanceId，仅更新诊断名称。
        /// </summary>
        /// <param name="name">诊断名称。</param>
        /// <param name="fsm">状态机实例。</param>
        public static void RegisterFsm(string name, IFSM fsm)
        {
            FsmKitRegistry.Register(fsm, name);
        }

        /// <summary>
        /// 按兼容名称注销最后注册的同名 FSM。
        /// </summary>
        /// <param name="name">诊断名称。</param>
        public static void UnregisterFsm(string name)
        {
            FsmKitRegistry.UnregisterByName(name);
        }

        /// <summary>清空全部 FSM 诊断记录；provider 注入点保持不变。</summary>
        public static void ClearAll()
        {
            FsmKitRegistry.ClearAll();
        }

        /// <summary>
        /// 执行 2.0-pre 兼容的 action/payload 查询入口；错误继续通过异常表达。
        /// </summary>
        /// <param name="action">只读 action。</param>
        /// <param name="payloadJson">payload JSON。</param>
        /// <returns>结果 JSON。</returns>
        public string HandleAction(string action, string payloadJson)
        {
            switch (action)
            {
                case "list_all":
                    return ListAll();
                case "get_state":
                    return GetState(payloadJson);
                case "get_history":
                    return GetHistory(payloadJson);
                case "get_state_events":
                    return GetStateEvents(payloadJson);
                case "get_workbench_snapshot":
                    return GetWorkbenchSnapshot(payloadJson);
                default:
                    throw new NotSupportedException("Unknown FsmKit action '" + action + "'.");
            }
        }

        /// <summary>
        /// 按稳定 instanceId 创建 Workbench 聚合状态，供命名 Shared Memory Telemetry 复用同一事实源。
        /// </summary>
        /// <param name="instanceId">注册表生成的安全实例标识。</param>
        /// <returns>精确选择该实例的完整 Workbench payload。</returns>
        internal string CreateWorkbenchSnapshot(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                throw new ArgumentException("FsmKit instanceId is required.", nameof(instanceId));
            }

            string payloadJson = "{\"instanceId\":\"" + JsonHelper.EscapeString(instanceId) + "\"}";
            return GetWorkbenchSnapshot(payloadJson);
        }

        /// <summary>
        /// 执行新版 Runtime CommandBridge 请求，并把异常转换为 terminal error。
        /// </summary>
        /// <param name="request">已通过 handler 匹配的请求。</param>
        /// <returns>终态命令结果。</returns>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            try
            {
                return YokiFrameCommandResult.Success(HandleAction(request.Action, request.PayloadJson));
            }
            catch (ArgumentException exception)
            {
                return YokiFrameCommandResult.Error("InvalidPayload", exception.Message);
            }
            catch (KeyNotFoundException exception)
            {
                return YokiFrameCommandResult.Error("FsmNotFound", exception.Message);
            }
            catch (NotSupportedException exception)
            {
                return YokiFrameCommandResult.Error("UnknownCommand", exception.Message);
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("FsmQueryFailed", exception.Message);
            }
        }

        /// <summary>构建全部 FSM 摘要。</summary>
        private static string ListAll()
        {
            return FsmKitJsonWriter.WriteList(FsmKitRegistry.GetAllSnapshots());
        }

        /// <summary>构建指定 FSM 的状态树。</summary>
        private static string GetState(string payloadJson)
        {
            return FsmKitJsonWriter.WriteState(GetRequiredSnapshot(payloadJson));
        }

        /// <summary>读取 provider 覆盖或 Core 内建转换历史。</summary>
        private static string GetHistory(string payloadJson)
        {
            string identity = GetProviderIdentity(payloadJson);
            string providerJson = InvokeProvider(HistoryProvider, identity);
            if (!string.IsNullOrEmpty(providerJson))
            {
                return providerJson;
            }

            return FsmKitJsonWriter.WriteHistory(GetRequiredSnapshot(payloadJson).History);
        }

        /// <summary>读取 provider 覆盖或 Core 内建状态生命周期记录。</summary>
        private static string GetStateEvents(string payloadJson)
        {
            string identity = GetProviderIdentity(payloadJson);
            string providerJson = InvokeProvider(StateLifecycleProvider, identity);
            if (!string.IsNullOrEmpty(providerJson))
            {
                return providerJson;
            }

            return FsmKitJsonWriter.WriteStateEvents(GetRequiredSnapshot(payloadJson).StateEvents);
        }

        /// <summary>构建 Workbench 单次刷新需要的完整聚合对象。</summary>
        private static string GetWorkbenchSnapshot(string payloadJson)
        {
            FsmKitDiagnosticSnapshot[] snapshots = FsmKitRegistry.GetAllSnapshots();
            FsmKitDiagnosticSnapshot selected = SelectWorkbenchSnapshot(payloadJson, snapshots);
            string historyJson = null;
            string eventsJson = null;
            if (selected != null)
            {
                historyJson = InvokeProvider(HistoryProvider, selected.Name);
                eventsJson = InvokeProvider(StateLifecycleProvider, selected.Name);
            }

            historyJson = string.IsNullOrEmpty(historyJson)
                ? FsmKitJsonWriter.WriteHistory(selected?.History ?? Array.Empty<FsmKitTransitionRecord>())
                : historyJson;
            eventsJson = string.IsNullOrEmpty(eventsJson)
                ? FsmKitJsonWriter.WriteStateEvents(selected?.StateEvents ?? Array.Empty<FsmKitStateEventRecord>())
                : eventsJson;

            return FsmKitJsonWriter.WriteWorkbench(
                snapshots,
                selected,
                historyJson,
                eventsJson);
        }

        /// <summary>按 payload 选择 Workbench 目标；未指定时回落首个实例或空选择。</summary>
        private static FsmKitDiagnosticSnapshot SelectWorkbenchSnapshot(
            string payloadJson,
            FsmKitDiagnosticSnapshot[] snapshots)
        {
            string instanceId = JsonHelper.ExtractString(payloadJson, "instanceId");
            string name = JsonHelper.ExtractString(payloadJson, "fsmName");
            if (string.IsNullOrEmpty(instanceId) && string.IsNullOrEmpty(name))
            {
                return snapshots.Length == 0 ? null : snapshots[0];
            }

            FsmKitDiagnosticSnapshot selected = FsmKitRegistry.FindSnapshot(instanceId, name);
            if (selected != null)
            {
                return selected;
            }

            string identity = !string.IsNullOrEmpty(instanceId) ? instanceId : name;
            throw new KeyNotFoundException("FSM '" + identity + "' not found.");
        }

        /// <summary>从 payload 读取 instanceId 或兼容 fsmName，并要求匹配已注册实例。</summary>
        private static FsmKitDiagnosticSnapshot GetRequiredSnapshot(string payloadJson)
        {
            string instanceId = JsonHelper.ExtractString(payloadJson, "instanceId");
            string name = JsonHelper.ExtractString(payloadJson, "fsmName");
            if (string.IsNullOrEmpty(instanceId) && string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Missing 'fsmName' or 'instanceId' in payload.");
            }

            FsmKitDiagnosticSnapshot snapshot = FsmKitRegistry.FindSnapshot(instanceId, name);
            if (snapshot == null)
            {
                string identity = !string.IsNullOrEmpty(instanceId) ? instanceId : name;
                throw new KeyNotFoundException("FSM '" + identity + "' not found.");
            }

            return snapshot;
        }

        /// <summary>取得 provider 的兼容名称；instanceId 查询命中时转换为当前名称。</summary>
        private static string GetProviderIdentity(string payloadJson)
        {
            string name = JsonHelper.ExtractString(payloadJson, "fsmName");
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }

            string instanceId = JsonHelper.ExtractString(payloadJson, "instanceId");
            if (string.IsNullOrEmpty(instanceId))
            {
                throw new ArgumentException("Missing 'fsmName' or 'instanceId' in payload.");
            }

            FsmKitDiagnosticSnapshot snapshot = FsmKitRegistry.FindSnapshot(instanceId, null);
            return snapshot?.Name ?? instanceId;
        }

        /// <summary>调用可选 provider；provider 缺失时返回空。</summary>
        private static string InvokeProvider(Func<string, string> provider, string identity)
        {
            return provider == null ? null : provider(identity);
        }
    }
}
#endif
