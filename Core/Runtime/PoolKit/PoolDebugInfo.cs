using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 活跃对象信息，用于跟踪已借出的对象。
    /// </summary>
    public class ActiveObjectInfo
    {
        /// <summary>对象引用。</summary>
        public object Obj { get; set; }

        /// <summary>借出时间（等价于 realtimeSinceStartup）。</summary>
        public float SpawnTime { get; set; }

        /// <summary>调用堆栈（Debug 模式记录）。</summary>
        public string StackTrace { get; set; }

        /// <summary>借出调用位置文件。</summary>
        public string SourceFile { get; set; }

        /// <summary>借出调用位置行号。</summary>
        public int SourceLine { get; set; }
    }

    /// <summary>
    /// 池内可用对象信息，用于查看当前缓存成员。
    /// </summary>
    public class InactiveObjectInfo
    {
        /// <summary>对象引用。</summary>
        public object Obj { get; set; }
    }

    /// <summary>
    /// 对象池调试信息。
    /// </summary>
    public class PoolDebugInfo
    {
        /// <summary>对象池名称。</summary>
        public string Name { get; set; }

        /// <summary>对象池类型名。</summary>
        public string TypeName { get; set; }

        /// <summary>总容量（池内对象 + 已借出对象）。</summary>
        public int TotalCount { get; set; }

        /// <summary>已借出对象数量。</summary>
        public int ActiveCount { get; set; }

        /// <summary>历史峰值数量。</summary>
        public int PeakCount { get; set; }

        /// <summary>最大缓存容量（-1 表示不限）。</summary>
        public int MaxCacheCount { get; set; } = -1;

        /// <summary>活跃对象列表。</summary>
        public List<ActiveObjectInfo> ActiveObjects { get; } = new();

        /// <summary>池内可用对象列表。</summary>
        public List<InactiveObjectInfo> InactiveObjects { get; } = new();

        /// <summary>对象池引用（用于强制归还）。</summary>
        public object PoolRef { get; set; }

        /// <summary>非活跃对象数量（池内可用对象）。</summary>
        public int InactiveCount => TotalCount - ActiveCount;

        /// <summary>使用率（0-1），基于 active / total。</summary>
        public float UsageRate => TotalCount > 0 ? (float)ActiveCount / TotalCount : 0f;

        /// <summary>根据使用率推导出的健康状态。</summary>
        public PoolHealthStatus HealthStatus
        {
            get
            {
                if (UsageRate > 0.8f) return PoolHealthStatus.Busy;
                if (UsageRate < 0.5f) return PoolHealthStatus.Healthy;
                return PoolHealthStatus.Normal;
            }
        }
    }
}
