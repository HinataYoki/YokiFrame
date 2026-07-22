#if UNITY_EDITOR

using System;

namespace YokiFrame
{
    /// <summary>表示当前 Unity Console Error 的有界且可判定完整性快照。</summary>
    [Serializable]
    internal sealed class YokiFrameUnityConsoleErrorObservation : YokiFrameUnityHarnessResult
    {
        /// <summary>Ready 表示扫描完整，Partial 表示 Console 超过固定扫描上限。</summary>
        public string status = "Partial";

        /// <summary>采集时间 UTC。</summary>
        public string observedAtUtc = string.Empty;

        /// <summary>Unity Console 当前全部条目数量。</summary>
        public int totalEntryCount;

        /// <summary>本次实际扫描的 Console 条目数量。</summary>
        public int scannedEntryCount;

        /// <summary>是否扫描了 Console 当前全部条目。</summary>
        public bool scanComplete;

        /// <summary>已扫描范围内的 Error 数量；scanComplete=false 时不能作为全局零错误证据。</summary>
        public int errorCount;

        /// <summary>实际返回的 Error 明细数量。</summary>
        public int returnedCount;

        /// <summary>扫描范围或返回明细是否被固定上限裁剪。</summary>
        public bool truncated;

        /// <summary>按 Console 顺序保留的最后若干 Error 明细。</summary>
        public YokiFrameUnityConsoleErrorEntry[] errors = Array.Empty<YokiFrameUnityConsoleErrorEntry>();

        /// <summary>Console 事实来源。</summary>
        public string source = "UnityEditor.LogEntries";
    }

    /// <summary>表示一条经过裁剪的 Unity Console Error 证据。</summary>
    [Serializable]
    internal sealed class YokiFrameUnityConsoleErrorEntry
    {
        /// <summary>条目在当前 Console 快照中的索引。</summary>
        public int index;

        /// <summary>固定归一化为 Error。</summary>
        public string type = "Error";

        /// <summary>经过长度上限裁剪的错误消息。</summary>
        public string message = string.Empty;
    }

    /// <summary>表示 Console 反射事实源返回的有界扫描结果。</summary>
    internal sealed class YokiFrameUnityConsoleProbe
    {
        /// <summary>Unity Console 当前全部条目数量。</summary>
        public int TotalEntryCount { get; set; }

        /// <summary>是否从索引 0 开始扫描了全部条目。</summary>
        public bool ScanComplete { get; set; }

        /// <summary>实际扫描的条目事实。</summary>
        public YokiFrameUnityConsoleEntryFact[] Entries { get; set; } = Array.Empty<YokiFrameUnityConsoleEntryFact>();
    }

    /// <summary>表示一条只含错误分类所需字段的 Console 事实。</summary>
    internal sealed class YokiFrameUnityConsoleEntryFact
    {
        /// <summary>Console 条目索引。</summary>
        public int Index { get; set; }

        /// <summary>该条目是否属于 Error/Assert/Exception/Compile Error。</summary>
        public bool IsError { get; set; }

        /// <summary>Console 消息。</summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>抽象 Unity Console 事实源，便于测试覆盖完整、裁剪与不支持路径。</summary>
    internal interface IYokiFrameUnityConsoleProbeProvider
    {
        /// <summary>读取不超过固定上限的 Console 条目。</summary>
        /// <param name="maxEntries">最多扫描条目数。</param>
        /// <returns>Console 扫描事实。</returns>
        YokiFrameUnityConsoleProbe Read(int maxEntries);
    }
}

#endif
