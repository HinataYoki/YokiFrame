#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FsmKit 响应式 ViewModel
    /// 管理 FSM 列表、选中 FSM、状态矩阵和转换历史的响应式数据
    /// </summary>
    public sealed class FsmKitViewModel : IDisposable
    {
        #region 响应式属性

        /// <summary>
        /// FSM 实例列表
        /// </summary>
        public ReactiveCollection<IFSM> Fsms { get; } = new(16);

        /// <summary>
        /// 当前选中的 FSM
        /// </summary>
        public ReactiveProperty<IFSM> SelectedFsm { get; } = new();

        /// <summary>
        /// 转换历史列表（当前选中 FSM 的）
        /// </summary>
        public ReactiveCollection<FsmDebugger.TransitionEntry> TransitionHistory { get; } = new(128);

        #endregion

        #region 统计数据

        /// <summary>
        /// 当前状态名称
        /// </summary>
        public ReactiveProperty<string> CurrentStateName { get; } = new(string.Empty);

        /// <summary>
        /// 上一状态名称
        /// </summary>
        public ReactiveProperty<string> PreviousStateName { get; } = new(string.Empty);

        /// <summary>
        /// 当前状态持续时间
        /// </summary>
        public ReactiveProperty<float> StateDuration { get; } = new(0f);

        /// <summary>
        /// 机器状态
        /// </summary>
        public ReactiveProperty<MachineState> MachineStateValue { get; } = new(MachineState.End);

        /// <summary>
        /// 状态总数
        /// </summary>
        public ReactiveProperty<int> StateCount { get; } = new(0);

        #endregion

        #region 内部字段

        private readonly List<IFSM> mTempFsms = new(16);
        private readonly List<FsmDebugger.TransitionEntry> mTempHistory = new(128);
        private bool mIsDisposed;

        #endregion

        #region 数据刷新

        /// <summary>
        /// 刷新 FSM 列表数据
        /// </summary>
        public void RefreshFsms()
        {
            if (mIsDisposed) return;

            FsmDebugger.GetActiveFsms(mTempFsms);

            // 检查是否有变化
            bool hasChanges = mTempFsms.Count != Fsms.Count;
            if (!hasChanges)
            {
                for (int i = 0; i < mTempFsms.Count; i++)
                {
                    if (i >= Fsms.Count || !ReferenceEquals(mTempFsms[i], Fsms[i]))
                    {
                        hasChanges = true;
                        break;
                    }
                }
            }

            if (hasChanges)
            {
                Fsms.ReplaceAll(mTempFsms);
            }

            // 如果选中的 FSM 已不存在，清除选择
            if (SelectedFsm.Value != null && !mTempFsms.Contains(SelectedFsm.Value))
            {
                SelectedFsm.Value = null;
            }
        }

        /// <summary>
        /// 刷新选中 FSM 的详情数据
        /// </summary>
        public void RefreshSelectedFsmDetails()
        {
            if (mIsDisposed) return;

            var fsm = SelectedFsm.Value;
            if (fsm == null)
            {
                CurrentStateName.Value = string.Empty;
                PreviousStateName.Value = string.Empty;
                StateDuration.Value = 0f;
                MachineStateValue.Value = MachineState.End;
                StateCount.Value = 0;
                TransitionHistory.Clear();
                return;
            }

            // 更新当前状态名称
            CurrentStateName.Value = GetCurrentStateName(fsm);

            // 更新机器状态
            MachineStateValue.Value = fsm.MachineState;

            // 更新状态数量
            StateCount.Value = fsm.GetAllStates().Count;

            // 更新统计数据
            var stats = FsmDebugger.GetStats(fsm.Name);
            PreviousStateName.Value = stats.PreviousState ?? string.Empty;
            StateDuration.Value = FsmDebugger.GetStateDuration(fsm.Name);

            // 刷新转换历史
            RefreshTransitionHistory(fsm);
        }

        /// <summary>
        /// 刷新转换历史
        /// </summary>
        private void RefreshTransitionHistory(IFSM fsm)
        {
            mTempHistory.Clear();

            var history = FsmDebugger.TransitionHistory;
            var filterName = fsm.Name;

            // 倒序添加（最新的在前）
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var entry = history[i];
                if (entry.FsmName == filterName)
                {
                    mTempHistory.Add(entry);
                }
            }

            // 检查是否有变化
            if (mTempHistory.Count != TransitionHistory.Count)
            {
                TransitionHistory.ReplaceAll(mTempHistory);
            }
        }

        /// <summary>
        /// 获取当前状态名称
        /// </summary>
        private static string GetCurrentStateName(IFSM fsm)
        {
            if (fsm.CurrentStateId < 0) return "None";
            return Enum.GetName(fsm.EnumType, fsm.CurrentStateId) ?? fsm.CurrentStateId.ToString();
        }

        #endregion

        #region 操作方法

        /// <summary>
        /// 选择 FSM
        /// </summary>
        public void SelectFsm(IFSM fsm)
        {
            if (mIsDisposed) return;
            SelectedFsm.Value = fsm;
            RefreshSelectedFsmDetails();
        }

        /// <summary>
        /// 清除选择
        /// </summary>
        public void ClearSelection()
        {
            if (mIsDisposed) return;
            SelectedFsm.Value = null;
            RefreshSelectedFsmDetails();
        }

        /// <summary>
        /// 清空转换历史
        /// </summary>
        public void ClearHistory()
        {
            if (mIsDisposed) return;
            FsmDebugger.ClearHistory();
            TransitionHistory.Clear();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (mIsDisposed) return;
            mIsDisposed = true;

            Fsms.Dispose();
            SelectedFsm.Dispose();
            TransitionHistory.Dispose();
            CurrentStateName.Dispose();
            PreviousStateName.Dispose();
            StateDuration.Dispose();
            MachineStateValue.Dispose();
            StateCount.Dispose();

            mTempFsms.Clear();
            mTempHistory.Clear();
        }

        #endregion
    }
}
#endif
