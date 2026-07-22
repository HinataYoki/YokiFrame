#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示 CommandPolicy 评估所需的命令信封摘要。
    /// </summary>
    public sealed class YokiFrameCommandPolicyRequest
    {
        /// <summary>
        /// 创建策略评估请求；该对象只承载摘要，不解析业务 payload。
        /// </summary>
        /// <param name="source">命令来源。</param>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="action">action 标识。</param>
        /// <param name="payloadJson">payload JSON 文本。</param>
        /// <param name="timeoutMs">命令超时时间，单位毫秒。</param>
        /// <param name="commandFileBytes">命令文件字节数；未知时传 0。</param>
        public YokiFrameCommandPolicyRequest(
            string source,
            string kit,
            string action,
            string payloadJson,
            int timeoutMs,
            long commandFileBytes)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Kit = kit ?? throw new ArgumentNullException(nameof(kit));
            Action = action ?? throw new ArgumentNullException(nameof(action));
            PayloadJson = payloadJson ?? "{}";
            TimeoutMs = timeoutMs;
            CommandFileBytes = commandFileBytes;
        }

        /// <summary>
        /// 获取命令来源。
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// 获取 Kit 标识。
        /// </summary>
        public string Kit { get; }

        /// <summary>
        /// 获取 action 标识。
        /// </summary>
        public string Action { get; }

        /// <summary>
        /// 获取 payload JSON 文本。
        /// </summary>
        public string PayloadJson { get; }

        /// <summary>
        /// 获取命令超时时间，单位毫秒。
        /// </summary>
        public int TimeoutMs { get; }

        /// <summary>
        /// 获取命令文件字节数；为 0 表示调用侧无法提供。
        /// </summary>
        public long CommandFileBytes { get; }
    }
}
#endif
