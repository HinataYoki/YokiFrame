#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 响应式 ViewModel
    /// 管理池列表、选中池、活跃对象和事件日志的响应式数据
    /// </summary>
    public sealed class PoolKitViewModel : IDisposable
    {
        #region 响应式属性

        /// <summary>
        /// 池列表
        /// </summary>
        public ReactiveCollection<PoolDebugInfo> Pools { get; } = new(16);

        /// <summary>
        /// 当前选中的池
        /// </summary>
        public ReactiveProperty<PoolDebugInfo> SelectedPool { get; } = new();

        /// <summary>
        /// 活跃对象列表（当前选中池的）
        /// </summary>
        public ReactiveCollection<ActiveObjectInfo> ActiveObjects { get; } = new(64);

        /// <summary>
        /// 事件日志列表（当前选中池的）
        /// </summary>
        public ReactiveCollection<PoolEvent> EventLogs { get; } = new(128);

        #endregion

        #region 过滤状态

        /// <summary>
        /// 事件类型过滤（null = 全部）
        /// </summary>
        public ReactiveProperty<PoolEventType?> EventFilter { get; } = new();

        #endregion

        #region 统计数据

        /// <summary>
        /// 总对象数
        /// </summary>
        public ReactiveProperty<int> TotalCount { get; } = new(0);

        /// <summary>
        /// 活跃对象数
        /// </summary>
        public ReactiveProperty<int> ActiveCount { get; } = new(0);

        /// <summary>
        /// 空闲对象数
        /// </summary>
        public ReactiveProperty<int> InactiveCount { get; } = new(0);

        /// <summary>
        /// 峰值对象数
        /// </summary>
        public ReactiveProperty<int> PeakCount { get; } = new(0);

        #endregion

        #region 内部字段

        private readonly List<PoolDebugInfo> mTempPools = new(16);
        private readonly List<ActiveObjectInfo> mTempActiveObjects = new(64);
        private readonly List<PoolEvent> mTempEvents = new(128);
        private bool mIsDisposed;

        #endregion

        #region 数据刷新

        /// <summary>
        /// 刷新池列表数据
        /// </summary>
        public void RefreshPools()
        {
            if (mIsDisposed) return;

            PoolDebugger.GetAllPools(mTempPools);
            
            // 检查是否有变化
            bool hasChanges = mTempPools.Count != Pools.Count;
            if (!hasChanges)
            {
                for (int i = 0; i < mTempPools.Count; i++)
                {
                    if (i >= Pools.Count || !ReferenceEquals(mTempPools[i], Pools[i]))
                    {
                        hasChanges = true;
                        break;
                    }
                }
            }

            if (hasChanges)
            {
                Pools.ReplaceAll(mTempPools);
            }

            // 如果选中的池已不存在，清除选择
            if (SelectedPool.Value != null && !mTempPools.Contains(SelectedPool.Value))
            {
                SelectedPool.Value = null;
            }
        }

        /// <summary>
        /// 刷新选中池的详情数据
        /// </summary>
        public void RefreshSelectedPoolDetails()
        {
            if (mIsDisposed) return;

            var pool = SelectedPool.Value;
            if (pool == null)
            {
                TotalCount.Value = 0;
                ActiveCount.Value = 0;
                InactiveCount.Value = 0;
                PeakCount.Value = 0;
                ActiveObjects.Clear();
                EventLogs.Clear();
                return;
            }

            // 更新统计数据
            TotalCount.Value = pool.TotalCount;
            ActiveCount.Value = pool.ActiveCount;
            InactiveCount.Value = pool.InactiveCount;
            PeakCount.Value = pool.PeakCount;

            // 刷新活跃对象列表
            RefreshActiveObjects(pool);

            // 刷新事件日志
            RefreshEventLogs(pool);
        }

        /// <summary>
        /// 刷新活跃对象列表
        /// </summary>
        private void RefreshActiveObjects(PoolDebugInfo pool)
        {
            mTempActiveObjects.Clear();

            for (int i = 0; i < pool.ActiveObjects.Count; i++)
            {
                mTempActiveObjects.Add(pool.ActiveObjects[i]);
            }

            // 检查是否有变化
            bool hasChanges = mTempActiveObjects.Count != ActiveObjects.Count;
            if (!hasChanges)
            {
                for (int i = 0; i < mTempActiveObjects.Count; i++)
                {
                    if (i >= ActiveObjects.Count || !ReferenceEquals(mTempActiveObjects[i].Obj, ActiveObjects[i].Obj))
                    {
                        hasChanges = true;
                        break;
                    }
                }
            }

            if (hasChanges)
            {
                ActiveObjects.ReplaceAll(mTempActiveObjects);
            }
        }

        /// <summary>
        /// 刷新事件日志
        /// </summary>
        private void RefreshEventLogs(PoolDebugInfo pool)
        {
            PoolDebugger.GetEventHistory(mTempEvents, EventFilter.Value, pool.Name);

            // 检查是否有变化（简单比较数量）
            if (mTempEvents.Count != EventLogs.Count)
            {
                EventLogs.ReplaceAll(mTempEvents);
            }
        }

        #endregion

        #region 操作方法

        /// <summary>
        /// 选择池
        /// </summary>
        public void SelectPool(PoolDebugInfo pool)
        {
            if (mIsDisposed) return;
            SelectedPool.Value = pool;
            RefreshSelectedPoolDetails();
        }

        /// <summary>
        /// 清除选择
        /// </summary>
        public void ClearSelection()
        {
            if (mIsDisposed) return;
            SelectedPool.Value = null;
            RefreshSelectedPoolDetails();
        }

        /// <summary>
        /// 设置事件过滤
        /// </summary>
        public void SetEventFilter(PoolEventType? filter)
        {
            if (mIsDisposed) return;
            EventFilter.Value = filter;
            if (SelectedPool.Value != null)
            {
                RefreshEventLogs(SelectedPool.Value);
            }
        }

        /// <summary>
        /// 清空事件日志
        /// </summary>
        public void ClearEventLogs()
        {
            if (mIsDisposed) return;
            PoolDebugger.ClearEventHistory();
            EventLogs.Clear();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            Pools.Dispose();
            SelectedPool.Dispose();
            ActiveObjects.Dispose();
            EventLogs.Dispose();
            EventFilter.Dispose();
            TotalCount.Dispose();
            ActiveCount.Dispose();
            InactiveCount.Dispose();
            PeakCount.Dispose();

            mTempPools.Clear();
            mTempActiveObjects.Clear();
            mTempEvents.Clear();
        }

        #endregion
    }
}
#endif
