using System;

namespace YokiFrame
{
    /// <summary>
    /// 共享内存遥测通道读取到的一帧最新状态。
    /// </summary>
    public readonly struct SharedMemoryTelemetrySample
    {
        /// <summary>
        /// 创建共享内存遥测样本。
        /// </summary>
        /// <param name="sequence">发布序号。</param>
        /// <param name="timestampUtc">发布时的 UTC 时间。</param>
        /// <param name="engineId">发布该帧的引擎实例标识。</param>
        /// <param name="kit">发布该帧的 Kit 名称。</param>
        /// <param name="name">遥测通道名称。</param>
        /// <param name="payloadJson">遥测载荷 JSON。</param>
        public SharedMemoryTelemetrySample(
            ulong sequence,
            DateTime timestampUtc,
            string engineId,
            string kit,
            string name,
            string payloadJson)
        {
            Sequence = sequence;
            TimestampUtc = timestampUtc;
            EngineId = engineId ?? string.Empty;
            Kit = kit ?? string.Empty;
            Name = name ?? string.Empty;
            PayloadJson = payloadJson ?? string.Empty;
        }

        /// <summary>
        /// 获取发布序号。
        /// </summary>
        public ulong Sequence { get; }

        /// <summary>
        /// 获取发布时的 UTC 时间。
        /// </summary>
        public DateTime TimestampUtc { get; }

        /// <summary>
        /// 获取发布该帧的引擎实例标识。
        /// </summary>
        public string EngineId { get; }

        /// <summary>
        /// 获取发布该帧的 Kit 名称。
        /// </summary>
        public string Kit { get; }

        /// <summary>
        /// 获取遥测通道名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 获取遥测载荷 JSON。
        /// </summary>
        public string PayloadJson { get; }
    }
}
