#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
// 宿主侧 FastChannel Hello 校验的唯一实现：
// Unity 与 Godot 宿主的 listener 都必须经过本类型完成消息类型、身份 SafeId 与会话一致性校验，
// 避免某个宿主漏掉 SafeId 检查或错误码漂移。JSON 解析使用 Core JsonHelper，不依赖具体 JSON 库。
// 注意：本文件是宿主行为类型而非纯 wire 契约，因此不进入 YokiFrame.Protocol 的源码链接清单。

using System;

namespace YokiFrame
{
    /// <summary>
    /// 提供 FastChannel Host 侧的 Hello 握手校验：确认 frame 类型、engine/session/generation 三项身份
    /// 均为安全标识且与当前宿主会话完全一致。校验失败时输出稳定的 wire 错误码，由宿主包装为 Error frame。
    /// </summary>
    public static class YokiFrameFastChannelHostHandshake
    {
        /// <summary>
        /// 校验 Client Hello frame 是否匹配当前宿主会话。
        /// </summary>
        /// <param name="hello">已完成 framing 校验的 Hello frame。</param>
        /// <param name="expectedEngineId">当前宿主 engine 标识。</param>
        /// <param name="expectedSessionId">当前宿主进程会话标识。</param>
        /// <param name="expectedGeneration">当前宿主 generation。</param>
        /// <param name="errorCode">校验失败时的稳定 wire 错误码；成功时为空。</param>
        /// <param name="errorMessage">校验失败时面向 Client 的错误说明；成功时为空。</param>
        /// <returns>Hello 与当前会话完全匹配时返回 true。</returns>
        public static bool TryValidateHello(
            YokiFrameFastChannelFrame hello,
            string expectedEngineId,
            string expectedSessionId,
            long expectedGeneration,
            out string errorCode,
            out string errorMessage)
        {
            errorCode = null;
            errorMessage = null;
            if (hello == null)
            {
                errorCode = "FastChannelHandshakeInvalidJson";
                errorMessage = "FastChannel Hello payload is missing.";
                return false;
            }

            if (hello.MessageKind != YokiFrameFastChannelMessageKind.Hello)
            {
                // 沿用 Godot 宿主既有 wire 错误码，保持错误路径协议兼容。
                errorCode = "FastChannelHandshakeKindMismatch";
                errorMessage = "FastChannel connection must begin with a Hello frame.";
                return false;
            }

            var payload = hello.PayloadJson ?? string.Empty;
            var engineId = JsonHelper.ExtractString(payload, "engineId");
            var sessionId = JsonHelper.ExtractString(payload, "sessionId");
            if (!YokiFrameSafeIdContract.IsSafeId(engineId)
                || !YokiFrameSafeIdContract.IsSafeId(sessionId)
                || !JsonHelper.TryExtractLong(payload, "generation", out var generation)
                || generation <= 0L)
            {
                errorCode = "FastChannelHandshakeInvalidIdentity";
                errorMessage = "FastChannel Hello contains an invalid engine, session, or generation.";
                return false;
            }

            if (!string.Equals(engineId, expectedEngineId, StringComparison.Ordinal)
                || !string.Equals(sessionId, expectedSessionId, StringComparison.Ordinal)
                || generation != expectedGeneration)
            {
                errorCode = "FastChannelHandshakeMismatch";
                errorMessage = "FastChannel Hello does not match the active host session.";
                return false;
            }

            return true;
        }
    }
}
#endif
