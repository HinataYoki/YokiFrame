#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示进入 Runtime dispatcher 的跨宿主命令请求。
    /// </summary>
    public sealed class YokiFrameCommandRequest
    {
        /// <summary>
        /// 创建命令请求；该对象保留策略评估所需字段和业务 handler 所需 payload。
        /// </summary>
        /// <param name="source">命令来源。</param>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="action">action 标识。</param>
        /// <param name="payloadJson">payload JSON 文本。</param>
        /// <param name="timeoutMs">命令超时时间，单位毫秒。</param>
        /// <param name="commandFileBytes">命令文件字节数；未知时传 0。</param>
        public YokiFrameCommandRequest(
            string source,
            string kit,
            string action,
            string payloadJson,
            int timeoutMs,
            long commandFileBytes)
            : this(
                source,
                kit,
                action,
                payloadJson,
                timeoutMs,
                commandFileBytes,
                string.Empty,
                DateTimeOffset.MinValue)
        {
        }

        /// <summary>
        /// 创建包含 requestId 和创建时间的命令请求，供 FileBridge deadline、幂等和审计上下文复用。
        /// </summary>
        /// <param name="source">命令来源。</param>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="action">action 标识。</param>
        /// <param name="payloadJson">payload JSON 文本。</param>
        /// <param name="timeoutMs">命令超时时间，单位毫秒。</param>
        /// <param name="commandFileBytes">命令文件字节数；未知时传 0。</param>
        /// <param name="requestId">请求标识；非 FileBridge 调用可为空。</param>
        /// <param name="createdAtUtc">信封创建时间；未知时传 DateTimeOffset.MinValue。</param>
        public YokiFrameCommandRequest(
            string source,
            string kit,
            string action,
            string payloadJson,
            int timeoutMs,
            long commandFileBytes,
            string requestId,
            DateTimeOffset createdAtUtc)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Kit = kit ?? throw new ArgumentNullException(nameof(kit));
            Action = action ?? throw new ArgumentNullException(nameof(action));
            PayloadJson = payloadJson ?? "{}";
            TimeoutMs = timeoutMs;
            CommandFileBytes = commandFileBytes;
            RequestId = requestId ?? string.Empty;
            CreatedAtUtc = createdAtUtc;
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
        /// 获取业务 payload JSON 文本。
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

        /// <summary>
        /// 获取请求标识；非 FileBridge 调用可能为空。
        /// </summary>
        public string RequestId { get; }

        /// <summary>
        /// 获取命令信封创建时间；未知时为 DateTimeOffset.MinValue。
        /// </summary>
        public DateTimeOffset CreatedAtUtc { get; }

        /// <summary>
        /// 获取由创建时间和 timeout 计算出的执行截止时间；未知创建时间时为 MaxValue。
        /// </summary>
        public DateTimeOffset DeadlineUtc
        {
            get
            {
                if (CreatedAtUtc == DateTimeOffset.MinValue)
                {
                    return DateTimeOffset.MaxValue;
                }

                try
                {
                    return CreatedAtUtc.AddMilliseconds(TimeoutMs);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return DateTimeOffset.MinValue;
                }
            }
        }

        /// <summary>
        /// 判断请求是否已经超过执行截止时间；未知创建时间的内部调用不自动过期。
        /// </summary>
        /// <param name="nowUtc">用于比较的 UTC 时间。</param>
        /// <returns>已过期时返回 true。</returns>
        public bool IsExpired(DateTimeOffset nowUtc)
        {
            return CreatedAtUtc != DateTimeOffset.MinValue
                && nowUtc >= DeadlineUtc;
        }

        /// <summary>
        /// 转换为 CommandPolicy 使用的摘要请求，保证策略和执行层复用同一份输入。
        /// </summary>
        /// <returns>策略评估请求。</returns>
        public YokiFrameCommandPolicyRequest ToPolicyRequest()
        {
            return new YokiFrameCommandPolicyRequest(Source, Kit, Action, PayloadJson, TimeoutMs, CommandFileBytes);
        }
    }
}
#endif
