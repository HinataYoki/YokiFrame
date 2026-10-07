#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 定义 RoslynKit 操作面的稳定错误码。Kit 身份使用 Roslyn，不再使用 Engine。
    /// </summary>
    public static class RoslynErrorCodes
    {
        /// <summary>执行开关关闭、配置缺失或配置解析失败时拒绝执行类操作。</summary>
        public const string OPERATION_DISABLED = "RoslynOperationDisabled";

        /// <summary>当前来源不允许执行 Dangerous 类操作。</summary>
        public const string SOURCE_NOT_PERMITTED = "RoslynOperationSourceNotPermitted";

        /// <summary>当前引擎或宿主不支持该操作。</summary>
        public const string UNSUPPORTED = "RoslynOperationUnsupported";

        /// <summary>操作在当前状态下不可用。</summary>
        public const string UNAVAILABLE = "RoslynOperationUnavailable";

        /// <summary>payload 不满足操作要求。</summary>
        public const string INVALID_PAYLOAD = "RoslynOperationInvalidPayload";

        /// <summary>操作执行失败。</summary>
        public const string FAILED = "RoslynOperationFailed";

        /// <summary>inspect 找不到要读的对象（根/选择器没命中）。</summary>
        public const string INSPECT_TARGET_NOT_FOUND = "InspectTargetNotFound";

        /// <summary>inspect 找到了对象，但路径上的成员/键不存在。</summary>
        public const string INSPECT_MEMBER_NOT_FOUND = "InspectMemberNotFound";

        /// <summary>运行记录不存在（§10.1）。</summary>
        public const string RUN_NOT_FOUND = "RunNotFound";

        /// <summary>宿主正在域重载，结果未知。</summary>
        public const string ENGINE_RELOADING = "RoslynReloading";

        /// <summary>action 未在 RoslynKit 注册。</summary>
        public const string UNKNOWN_COMMAND = "UnknownCommand";
    }
}
#endif
