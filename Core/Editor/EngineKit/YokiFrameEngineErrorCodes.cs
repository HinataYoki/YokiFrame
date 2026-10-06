#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 定义 Engine Kit 操作面的稳定错误码；错误码属于对外契约，改名等同于破坏性变更。
    /// </summary>
    public static class YokiFrameEngineErrorCodes
    {
        /// <summary>执行开关关闭、配置缺失或配置解析失败时拒绝执行类操作。</summary>
        public const string OPERATION_DISABLED = "EngineOperationDisabled";

        /// <summary>当前来源不允许执行 Dangerous 类操作。</summary>
        public const string SOURCE_NOT_PERMITTED = "EngineOperationSourceNotPermitted";

        /// <summary>当前引擎或宿主不支持该操作。</summary>
        public const string UNSUPPORTED = "EngineOperationUnsupported";

        /// <summary>操作在当前状态下不可用。</summary>
        public const string UNAVAILABLE = "EngineOperationUnavailable";

        /// <summary>payload 不满足操作要求。</summary>
        public const string INVALID_PAYLOAD = "EngineOperationInvalidPayload";

        /// <summary>操作执行失败。</summary>
        public const string FAILED = "EngineOperationFailed";

        /// <summary>inspect 找不到要读的对象（根/选择器没命中）。</summary>
        public const string INSPECT_TARGET_NOT_FOUND = "InspectTargetNotFound";

        /// <summary>inspect 找到了对象，但路径上的成员/键不存在。</summary>
        public const string INSPECT_MEMBER_NOT_FOUND = "InspectMemberNotFound";

        /// <summary>运行记录不存在（§10.1）。</summary>
        public const string RUN_NOT_FOUND = "RunNotFound";

        /// <summary>宿主正在域重载，结果未知（§12）。</summary>
        public const string ENGINE_RELOADING = "EngineReloading";

        /// <summary>action 未在 Engine Kit 注册。</summary>
        public const string UNKNOWN_COMMAND = "UnknownCommand";
    }
}
#endif
