#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 解析 CommandBridge payload 中用于危险命令的顶层 confirmed 布尔确认。
    /// </summary>
    internal static class YokiFrameCommandPayloadConfirmation
    {
        private const string CONFIRMED_PROPERTY = "confirmed";
        private const string TRUE_LITERAL = "true";

        /// <summary>
        /// 判断 payload 是否包含顶层 JSON 布尔字段 confirmed=true。
        /// </summary>
        /// <param name="payloadJson">payload JSON 文本。</param>
        /// <returns>存在顶层 confirmed 布尔 true 时返回 true。</returns>
        public static bool HasConfirmedTrue(string payloadJson)
        {
            if (string.IsNullOrEmpty(payloadJson))
            {
                return false;
            }

            return JsonHelper.TryFindTopLevelValue(payloadJson, CONFIRMED_PROPERTY, out int valueStart)
                && TryReadTrueLiteral(payloadJson, valueStart);
        }

        /// <summary>
        /// 读取 JSON 布尔 true，并要求后续字符能结束当前值。
        /// </summary>
        /// <param name="json">payload JSON 文本。</param>
        /// <param name="index">当前扫描位置。</param>
        /// <returns>当前位置是布尔 true 时返回 true。</returns>
        private static bool TryReadTrueLiteral(string json, int index)
        {
            if (index + TRUE_LITERAL.Length > json.Length)
            {
                return false;
            }

            if (string.CompareOrdinal(json, index, TRUE_LITERAL, 0, TRUE_LITERAL.Length) != 0)
            {
                return false;
            }

            var nextIndex = index + TRUE_LITERAL.Length;
            return nextIndex >= json.Length || JsonHelper.IsValueTerminator(json[nextIndex]);
        }
    }
}
#endif
