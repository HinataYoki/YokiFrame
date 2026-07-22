#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 已借出对象的诊断信息。
    /// </summary>
    public sealed class ActiveObjectInfo
    {
        /// <summary>
        /// 对象引用。
        /// </summary>
        public object Obj { get; set; }

        /// <summary>
        /// 借出时间，单位为秒。
        /// </summary>
        public float SpawnTime { get; set; }

        /// <summary>
        /// 借出调用堆栈。
        /// </summary>
        public string StackTrace { get; set; }

        /// <summary>
        /// 借出调用位置文件。
        /// </summary>
        public string SourceFile { get; set; }

        /// <summary>
        /// 借出调用位置行号。
        /// </summary>
        public int SourceLine { get; set; }
    }

    /// <summary>
    /// 当前缓存对象的诊断信息。
    /// </summary>
    public sealed class InactiveObjectInfo
    {
        /// <summary>
        /// 对象引用。
        /// </summary>
        public object Obj { get; set; }
    }

    /// <summary>
    /// 单个对象池的诊断快照。
    /// </summary>
    public sealed class PoolDebugInfo
    {
        /// <summary>
        /// 当前诊断会话内稳定且唯一的对象池标识。
        /// </summary>
        public string PoolId { get; set; }

        /// <summary>
        /// 对象池名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 对象池类型名。
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// 总数量，通常为缓存数量加已借出数量。
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// 当前已借出对象数量。
        /// </summary>
        public int ActiveCount { get; set; }

        /// <summary>
        /// 历史峰值数量。
        /// </summary>
        public int PeakCount { get; set; }

        /// <summary>
        /// 最大缓存数量，-1 表示不限。
        /// </summary>
        public int MaxCacheCount { get; set; } = -1;

        /// <summary>
        /// 已借出对象列表。
        /// </summary>
        public List<ActiveObjectInfo> ActiveObjects { get; } = new();

        /// <summary>
        /// 当前缓存对象列表。
        /// </summary>
        public List<InactiveObjectInfo> InactiveObjects { get; } = new();

        /// <summary>
        /// 当前缓存对象总数；当明细受到快照预算限制时仍保留真实总量。
        /// </summary>
        public int InactiveObjectTotal { get; set; }

        /// <summary>
        /// 对象池引用，用于诊断工具执行强制归还。
        /// </summary>
        public object PoolRef { get; set; }

        /// <summary>
        /// 当前缓存对象数量。
        /// </summary>
        public int InactiveCount
        {
            get { return InactiveObjectTotal; }
        }

        /// <summary>
        /// 使用率，基于 ActiveCount / TotalCount。
        /// </summary>
        public float UsageRate
        {
            get { return TotalCount > 0 ? (float)ActiveCount / TotalCount : 0f; }
        }

        /// <summary>
        /// 根据当前使用率推导健康状态。
        /// </summary>
        public PoolHealthStatus HealthStatus
        {
            get
            {
                if (UsageRate > 0.8f)
                {
                    return PoolHealthStatus.Busy;
                }

                if (UsageRate < 0.5f)
                {
                    return PoolHealthStatus.Healthy;
                }

                return PoolHealthStatus.Normal;
            }
        }
    }
}
#endif
