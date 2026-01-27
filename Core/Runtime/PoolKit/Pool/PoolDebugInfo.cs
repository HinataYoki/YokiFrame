using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 活跃对象信息 - 追踪借出的对象
    /// </summary>
    public class ActiveObjectInfo
    {
        /// <summary>
        /// 对象引用
        /// </summary>
        public object Obj { get; set; }
        
        /// <summary>
        /// 借出时间 (Time.realtimeSinceStartup)
        /// </summary>
        public float SpawnTime { get; set; }
        
        /// <summary>
        /// 调用堆栈 (Debug 模式下记录)
        /// </summary>
        public string StackTrace { get; set; }
    }

    /// <summary>
    /// 对象池调试信息
    /// </summary>
    public class PoolDebugInfo
    {
        /// <summary>
        /// 池名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 池类型名称
        /// </summary>
        public string TypeName { get; set; }
        
        /// <summary>
        /// 总容量（池内 + 借出）
        /// </summary>
        public int TotalCount { get; set; }
        
        /// <summary>
        /// 借出中数量
        /// </summary>
        public int ActiveCount { get; set; }
        
        /// <summary>
        /// 历史峰值
        /// </summary>
        public int PeakCount { get; set; }
        
        /// <summary>
        /// 最大缓存容量（池的容量上限，-1 表示无限制）
        /// </summary>
        public int MaxCacheCount { get; set; } = -1;
        
        /// <summary>
        /// 活跃对象列表
        /// </summary>
        public List<ActiveObjectInfo> ActiveObjects { get; } = new();
        
        /// <summary>
        /// 池引用（用于强制归还）
        /// </summary>
        public object PoolRef { get; set; }

        /// <summary>
        /// 待机中数量（池内可用对象）
        /// </summary>
        public int InactiveCount => TotalCount - ActiveCount;

        /// <summary>
        /// 使用率 (0-1)，基于活跃对象数 / 总对象数
        /// </summary>
        public float UsageRate => TotalCount > 0 ? (float)ActiveCount / TotalCount : 0f;

        /// <summary>
        /// 健康状态
        /// </summary>
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
