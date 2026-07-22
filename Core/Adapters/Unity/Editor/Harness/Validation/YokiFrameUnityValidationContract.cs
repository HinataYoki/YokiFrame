#if UNITY_EDITOR

namespace YokiFrame
{
    /// <summary>定义 YokiFrame 自有的两个 Unity 只读诊断命令。</summary>
    internal static class YokiFrameUnityValidationContract
    {
        /// <summary>Unity 诊断 Kit 标识。</summary>
        public const string KIT_NAME = "Validation";

        /// <summary>查询当前 Unity 编译状态。</summary>
        public const string INSPECT_STATUS_ACTION = "inspect_status";

        /// <summary>查询当前 Unity Console Error。</summary>
        public const string GET_CONSOLE_ERRORS_ACTION = "get_console_errors";
    }
}

#endif
