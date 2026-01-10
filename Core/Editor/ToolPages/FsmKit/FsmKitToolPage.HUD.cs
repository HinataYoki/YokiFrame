#if UNITY_EDITOR
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 工具页面 - 当前状态 HUD 区域
    /// 显示当前状态的大字标题、持续时间、上一状态
    /// </summary>
    public partial class FsmKitToolPage
    {
        #region 构建 HUD

        /// <summary>
        /// 构建 HUD 区域（外层容器）
        /// </summary>
        private VisualElement BuildHudSection()
        {
            var section = new VisualElement { name = "hud-section" };
            section.AddToClassList("yoki-fsm-hud");

            // 初始显示空状态提示
            var emptyHint = CreateHelpBox("选择左侧状态机查看详情");
            section.Add(emptyHint);

            return section;
        }

        /// <summary>
        /// 构建 HUD 内容（选中 FSM 后显示）
        /// </summary>
        private VisualElement BuildHudContent()
        {
            var content = new VisualElement();
            content.AddToClassList("yoki-fsm-hud__content");

            // 左侧：状态信息
            var leftArea = new VisualElement();
            leftArea.AddToClassList("yoki-fsm-hud__left");
            content.Add(leftArea);

            // 小标题
            var titleLabel = new Label("CURRENT STATE");
            titleLabel.AddToClassList("yoki-fsm-hud__label");
            leftArea.Add(titleLabel);

            // 当前状态（大字）
            mCurrentStateLabel = new Label("—") { name = "current-state-big" };
            mCurrentStateLabel.AddToClassList("yoki-fsm-hud__state");
            leftArea.Add(mCurrentStateLabel);

            // 上一状态
            mPrevStateLabel = new Label("") { name = "prev-state" };
            mPrevStateLabel.AddToClassList("yoki-fsm-hud__prev-state");
            leftArea.Add(mPrevStateLabel);

            // 右侧：计时器
            var rightArea = new VisualElement();
            rightArea.AddToClassList("yoki-fsm-hud__right");
            content.Add(rightArea);

            // 持续时间标签
            var durationTitle = new Label("DURATION");
            durationTitle.AddToClassList("yoki-fsm-hud__label");
            rightArea.Add(durationTitle);

            // 持续时间（大数字）
            mDurationLabel = new Label("0.0s") { name = "duration-big" };
            mDurationLabel.AddToClassList("yoki-fsm-hud__duration");
            rightArea.Add(mDurationLabel);

            // 机器状态
            var machineStateLabel = new Label("") { name = "machine-state" };
            machineStateLabel.AddToClassList("yoki-fsm-hud__machine-state");
            rightArea.Add(machineStateLabel);

            return content;
        }

        /// <summary>
        /// 更新 HUD 区域
        /// </summary>
        private void UpdateHudSection()
        {
            if (mSelectedFsm == null) return;

            var fsm = mSelectedFsm;
            var isRunning = fsm.MachineState == MachineState.Running;

            // 当前状态
            var currentStateName = GetCurrentStateName(fsm);
            mCurrentStateLabel.text = currentStateName;
            mCurrentStateLabel.RemoveFromClassList("yoki-fsm-hud__state--inactive");
            if (!isRunning)
            {
                mCurrentStateLabel.AddToClassList("yoki-fsm-hud__state--inactive");
            }

            // 上一状态
            var stats = FsmDebugger.GetStats(fsm.Name);
            if (!string.IsNullOrEmpty(stats.PreviousState))
            {
                mPrevStateLabel.text = $"← Prev: {stats.PreviousState}";
                mPrevStateLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                mPrevStateLabel.style.display = DisplayStyle.None;
            }

            // 持续时间
            var duration = FsmDebugger.GetStateDuration(fsm.Name);
            mDurationLabel.text = isRunning ? $"{duration:F1}s" : "—";

            // 机器状态
            var machineStateLabel = mHudSection.Q<Label>("machine-state");
            if (machineStateLabel != null)
            {
                machineStateLabel.RemoveFromClassList("yoki-fsm-hud__machine-state--running");
                machineStateLabel.RemoveFromClassList("yoki-fsm-hud__machine-state--suspended");
                machineStateLabel.RemoveFromClassList("yoki-fsm-hud__machine-state--stopped");

                var (stateText, stateClass) = fsm.MachineState switch
                {
                    MachineState.Running => ("Running", "yoki-fsm-hud__machine-state--running"),
                    MachineState.Suspend => ("Suspended", "yoki-fsm-hud__machine-state--suspended"),
                    MachineState.End => ("Stopped", "yoki-fsm-hud__machine-state--stopped"),
                    _ => ("?", "yoki-fsm-hud__machine-state--stopped")
                };
                machineStateLabel.text = stateText;
                machineStateLabel.AddToClassList(stateClass);
            }
        }

        #endregion
    }
}
#endif
