using System;

namespace YokiFrame
{
    /// <summary>
    /// Kit Adapter bridge 共用的 snapshot 发布包装器。
    /// </summary>
    public sealed class CommandBridgeSnapshotPublisher
    {
        private readonly string mEngineId;
        private readonly string mKit;
        private readonly string mSnapshot;
        private readonly Func<string> mPayloadFactory;

        /// <summary>
        /// 创建 snapshot 发布包装器。
        /// </summary>
        /// <param name="engineId">引擎实例标识。</param>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="snapshot">Snapshot 名称。</param>
        /// <param name="payloadFactory">用于生成 payload JSON 的工厂。</param>
        public CommandBridgeSnapshotPublisher(string engineId, string kit, string snapshot, Func<string> payloadFactory)
        {
            if (payloadFactory == null)
                throw new ArgumentNullException(nameof(payloadFactory));

            mEngineId = engineId;
            mKit = kit;
            mSnapshot = snapshot;
            mPayloadFactory = payloadFactory;
        }

        /// <summary>
        /// 使用 payload 工厂发布 snapshot。
        /// </summary>
        /// <param name="yokiframeRoot">.yokiframe 根目录。</param>
        /// <returns>写入的 snapshot 文件路径；根目录为空时返回 null。</returns>
        public string Publish(string yokiframeRoot)
        {
            return Publish(yokiframeRoot, mPayloadFactory());
        }

        /// <summary>
        /// 发布指定 payload JSON 的 snapshot。
        /// </summary>
        /// <param name="yokiframeRoot">.yokiframe 根目录。</param>
        /// <param name="payloadJson">Snapshot data 字段 JSON。</param>
        /// <returns>写入的 snapshot 文件路径；根目录为空时返回 null。</returns>
        public string Publish(string yokiframeRoot, string payloadJson)
        {
            if (string.IsNullOrEmpty(yokiframeRoot))
                return null;

            return FileBridgeSnapshotWriter.WriteSnapshot(
                yokiframeRoot,
                mEngineId,
                mKit,
                mSnapshot,
                payloadJson);
        }
    }
}
