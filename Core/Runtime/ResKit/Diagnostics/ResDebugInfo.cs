#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>描述一次诊断读取中复制出的已加载资源状态。</summary>
    public sealed class ResDebugInfo
    {
        /// <summary>获取资源路径。</summary>
        public string Path { get; internal set; }

        /// <summary>获取资源的完整 CLR 类型名。</summary>
        public string TypeName { get; internal set; }

        /// <summary>获取当前全部活动 lease 的引用数。</summary>
        public int RefCount { get; internal set; }

        /// <summary>获取底层资源是否已经加载完成且仍有效。</summary>
        public bool IsDone { get; internal set; }

        /// <summary>获取创建底层资源的 Provider 名称。</summary>
        public string ProviderName { get; internal set; }

        /// <summary>获取创建底层资源时的 Provider 代次。</summary>
        public long ProviderGeneration { get; internal set; }

        /// <summary>获取第一条已跟踪来源的展示名，未跟踪时为空。</summary>
        public string Source { get; internal set; }

        /// <summary>获取第一条已跟踪来源的文件路径，未跟踪时为空。</summary>
        public string SourceFile { get; internal set; }

        /// <summary>获取第一条已跟踪来源的行号，未跟踪时为零。</summary>
        public int SourceLine { get; internal set; }

        /// <summary>获取当前资源有真实调用位置的 lease 数量。</summary>
        public int TrackedSourceCount { get; internal set; }

        /// <summary>获取当前资源的活动 lease 总数，可能大于已复制来源数量。</summary>
        public int SourceTotalCount { get; internal set; }

        /// <summary>获取当前资源全部活动 lease 的隔离来源副本。</summary>
        public IReadOnlyList<ResLoadSourceInfo> Sources { get; internal set; }
    }

    /// <summary>描述一个活动 lease 的来源与本地引用状态。</summary>
    public sealed class ResLoadSourceInfo
    {
        /// <summary>获取来源展示名。</summary>
        public string Display { get; internal set; }

        /// <summary>获取来源文件路径。</summary>
        public string FilePath { get; internal set; }

        /// <summary>获取来源行号。</summary>
        public int Line { get; internal set; }

        /// <summary>获取当前 lease 的本地引用数。</summary>
        public int RefCount { get; internal set; }

        /// <summary>获取该 lease 是否由只返回资源对象的 API 创建。</summary>
        public bool IsAnonymous { get; internal set; }

        /// <summary>获取该 lease 是否实际采集了调用位置。</summary>
        public bool IsTracked { get; internal set; }
    }
}
#endif
