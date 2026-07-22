#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示 Runtime dispatcher 返回给宿主桥接层的命令终态结果。
    /// </summary>
    public sealed class YokiFrameCommandResult
    {
        /// <summary>
        /// 创建命令结果；外部应优先使用 <see cref="Success"/> 或 <see cref="Error"/>。
        /// </summary>
        /// <param name="isSuccess">命令是否成功。</param>
        /// <param name="resultJson">成功结果 JSON。</param>
        /// <param name="errorCode">失败错误码。</param>
        /// <param name="errorMessage">失败说明。</param>
        private YokiFrameCommandResult(bool isSuccess, string resultJson, string errorCode, string errorMessage)
        {
            IsSuccess = isSuccess;
            ResultJson = resultJson;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// 获取命令是否成功。
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 获取成功结果 JSON；失败时为空对象。
        /// </summary>
        public string ResultJson { get; }

        /// <summary>
        /// 获取失败错误码；成功时为空字符串。
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// 获取失败说明；成功时为空字符串。
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 创建成功命令结果。
        /// </summary>
        /// <param name="resultJson">成功结果 JSON。</param>
        /// <returns>成功结果。</returns>
        public static YokiFrameCommandResult Success(string resultJson)
        {
            return new YokiFrameCommandResult(true, resultJson ?? "{}", string.Empty, string.Empty);
        }

        /// <summary>
        /// 创建失败命令结果，确保调用侧能写入 terminal response。
        /// </summary>
        /// <param name="errorCode">失败错误码。</param>
        /// <param name="errorMessage">失败说明。</param>
        /// <returns>失败结果。</returns>
        public static YokiFrameCommandResult Error(string errorCode, string errorMessage)
        {
            return new YokiFrameCommandResult(
                false,
                "{}",
                errorCode ?? "CommandFailed",
                errorMessage ?? "Command execution failed.");
        }
    }
}
#endif
