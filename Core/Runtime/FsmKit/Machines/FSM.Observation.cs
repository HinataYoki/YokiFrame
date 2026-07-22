#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>收纳只在 Editor/Tools 构建中存在的 FSM 观察发布逻辑。</summary>
    public partial class FSM<TEnum> where TEnum : Enum
    {
        private readonly Dictionary<TEnum, int> mStateOrder = new();
        private string mName;
        private int mNextStateOrder;

        /// <summary>状态机诊断名称；修改后同步更新稳定实例注册表。</summary>
        public string Name
        {
            get => mName;
            set
            {
                mName = NormalizeName(value);
                if (!mIsDisposed) FsmKitRegistry.Rename(this, mName);
            }
        }

        /// <summary>获取状态标识使用的枚举类型。</summary>
        public Type EnumType => typeof(TEnum);

        /// <summary>通过非泛型诊断契约读取当前状态。</summary>
        public IState CurrentState => CurState;

        /// <summary>通过非泛型诊断契约读取当前状态整数标识。</summary>
        public int CurrentStateId => CurState == null ? -1 : Convert.ToInt32(CurEnum);

        /// <summary>获取状态字典的独立整数键快照。</summary>
        /// <returns>状态字典快照。</returns>
        public IReadOnlyDictionary<int, IState> GetAllStates()
        {
            Dictionary<int, IState> snapshot = new(mStateDic.Count);
            foreach (var pair in mStateDic) snapshot[Convert.ToInt32(pair.Key)] = pair.Value;
            return snapshot;
        }

        /// <summary>获取状态首次加入顺序。</summary>
        /// <param name="stateId">状态整数标识。</param>
        /// <returns>加入顺序；缺失时返回 stateId。</returns>
        public int GetStateOrderIndex(int stateId)
        {
            var id = (TEnum)Enum.ToObject(typeof(TEnum), stateId);
            return mStateOrder.TryGetValue(id, out var order) ? order : stateId;
        }

        /// <summary>把空诊断名规范化为稳定的泛型状态机名称。</summary>
        /// <param name="name">调用方提供的名称。</param>
        /// <returns>可用于注册表的非空名称。</returns>
        private static string NormalizeName(string name) =>
            string.IsNullOrEmpty(name) ? "FSM<" + typeof(TEnum).Name + ">" : name;

        /// <summary>记录状态首次加入顺序。</summary>
        /// <param name="id">状态标识。</param>
        protected void RecordStateOrder(TEnum id)
        {
            if (!mStateOrder.ContainsKey(id)) mStateOrder.Add(id, mNextStateOrder++);
        }

        /// <summary>移除状态顺序记录，重新加入时会获得新顺序。</summary>
        protected void RemoveStateOrder(TEnum id) => mStateOrder.Remove(id);

        /// <summary>清空全部状态顺序记录。</summary>
        protected void ClearStateOrder()
        {
            mStateOrder.Clear();
            mNextStateOrder = 0;
        }

        /// <summary>记录状态加入并通知观察订阅者。</summary>
        /// <param name="id">加入的状态标识。</param>
        private void PublishStateAdded(TEnum id)
        {
            string stateName = id.ToString();
            FsmKitRegistry.RecordStateEvent(this, "added", stateName);
            FsmEditorHook.RaiseStateAdded(this, stateName);
        }

        /// <summary>记录状态移除并通知观察订阅者。</summary>
        /// <param name="id">移除的状态标识。</param>
        private void PublishStateRemoved(TEnum id)
        {
            string stateName = id.ToString();
            FsmKitRegistry.RecordStateEvent(this, "removed", stateName);
            FsmEditorHook.RaiseStateRemoved(this, stateName);
        }

        /// <summary>记录状态机启动并通知观察订阅者。</summary>
        /// <param name="id">成功启动的状态标识。</param>
        private void PublishFsmStarted(TEnum id)
        {
            string stateName = id.ToString();
            FsmKitRegistry.RecordTransition(this, "Start", stateName);
            FsmEditorHook.RaiseFsmStarted(this, stateName);
        }

        /// <summary>记录普通状态切换并通知观察订阅者。</summary>
        /// <param name="previousId">来源状态标识。</param>
        /// <param name="currentId">目标状态标识。</param>
        private void PublishStateChanged(TEnum previousId, TEnum currentId)
        {
            string previousName = previousId.ToString();
            string currentName = currentId.ToString();
            FsmKitRegistry.RecordTransition(this, previousName, currentName);
            FsmEditorHook.RaiseStateChanged(this, previousName, currentName);
        }
    }
}
#endif
