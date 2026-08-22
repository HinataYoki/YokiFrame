#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Globalization;

namespace YokiFrame
{
    /// <summary>
    /// 提供三宿主共用的 FileBridge 命令信封校验规则：协议版本、engine 归属、安全标识、
    /// 超时范围、创建时间与 payload JSON 语法。宿主反序列化各自 DTO 后把原始字段交给本类，
    /// 消除三份逐字段重复校验；返回失败原因文本，由宿主决定异常类型。
    /// </summary>
    internal static class YokiFrameCommandEnvelopeValidator
    {
        /// <summary>
        /// 校验命令信封全部共享协议字段。
        /// </summary>
        /// <param name="protocolVersion">信封协议版本。</param>
        /// <param name="engineId">信封目标 engine。</param>
        /// <param name="expectedEngineId">当前宿主 engine 标识。</param>
        /// <param name="source">命令来源。</param>
        /// <param name="requestId">请求标识。</param>
        /// <param name="kit">目标 Kit。</param>
        /// <param name="action">目标 action。</param>
        /// <param name="timeoutMs">等待超时毫秒数。</param>
        /// <param name="createdAtUtc">信封创建时间文本。</param>
        /// <param name="payloadJson">payload JSON 文本。</param>
        /// <returns>校验��过返回 null；失败返回稳定原因说明。</returns>
        public static string Validate(
            int protocolVersion,
            string engineId,
            string expectedEngineId,
            string source,
            string requestId,
            string kit,
            string action,
            int timeoutMs,
            string createdAtUtc,
            string payloadJson)
        {
            if (protocolVersion != YokiFrameFileBridgeContract.PROTOCOL_VERSION
                || !string.Equals(engineId, expectedEngineId, StringComparison.Ordinal))
            {
                return "Command envelope protocolVersion or engineId is invalid.";
            }

            if (!YokiFrameSafeIdContract.IsSafeId(source)
                || !YokiFrameSafeIdContract.IsSafeId(requestId)
                || !YokiFrameSafeIdContract.IsSafeId(kit)
                || !YokiFrameSafeIdContract.IsSafeId(action))
            {
                return "Command envelope contains unsafe requestId, kit or action.";
            }

            if (timeoutMs < YokiFrameFileBridgeContract.COMMAND_TIMEOUT_MIN_MS
                || timeoutMs > YokiFrameFileBridgeContract.COMMAND_TIMEOUT_MAX_MS
                || !DateTimeOffset.TryParse(
                    createdAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _))
            {
                return "Command envelope timeoutMs or createdAtUtc is invalid.";
            }

            try
            {
                JsonHelper.EnsureValidJson(payloadJson);
            }
            catch (FormatException exception)
            {
                return "Command envelope payloadJson is invalid: " + exception.Message;
            }

            return null;
        }
    }
}
#endif
