#if UNITY_EDITOR
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 顶部状态摘要区。
    /// </summary>
    public partial class FsmKitToolPage
    {
        #region HUD

        /// <summary>
        /// 构建顶部摘要面板。
        /// </summary>
        private VisualElement BuildHudSection()
        {
            var (panel, body) = CreateKitSectionPanel(
                "状态摘要",
                "先看当前状态，再看持续时间和上一个状态。",
                KitIcons.CHART);
            panel.name = "hud-section";
            panel.AddToClassList("yoki-fsm-hud");
            panel.AddToClassList("yoki-kit-panel--slate");
            panel.style.flexGrow = 0;
            panel.style.marginBottom = 10;

            body.Add(CreateEmptyState(
                KitIcons.FSMKIT,
                "未选择状态机",
                "先在左侧选择一个状态机，再查看状态摘要。"));

            mHudSection = body;
            return panel;
        }

        /// <summary>
        /// 构建摘要正文。
        /// </summary>
        private VisualElement BuildHudContent()
        {
            var content = new VisualElement();
            content.AddToClassList("yoki-fsm-hud__content");

            var leftArea = new VisualElement();
            leftArea.AddToClassList("yoki-fsm-hud__left");
            content.Add(leftArea);

            var titleLabel = new Label("当前状态");
            titleLabel.AddToClassList("yoki-fsm-hud__label");
            leftArea.Add(titleLabel);

            mCurrentStateLabel = new Label("--") { name = "current-state-big" };
            mCurrentStateLabel.AddToClassList("yoki-fsm-hud__state");
            leftArea.Add(mCurrentStateLabel);

            mPrevStateLabel = new Label(string.Empty) { name = "prev-state" };
            mPrevStateLabel.AddToClassList("yoki-fsm-hud__prev-state");
            leftArea.Add(mPrevStateLabel);

            var rightArea = new VisualElement();
            rightArea.AddToClassList("yoki-fsm-hud__right");
            content.Add(rightArea);

            var durationTitle = new Label("持续时间");
            durationTitle.AddToClassList("yoki-fsm-hud__label");
            rightArea.Add(durationTitle);

            mDurationLabel = new Label("0.0s") { name = "duration-big" };
            mDurationLabel.AddToClassList("yoki-fsm-hud__duration");
            rightArea.Add(mDurationLabel);

            var machineStateLabel = new Label(string.Empty) { name = "machine-state" };
            machineStateLabel.AddToClassList("yoki-fsm-hud__machine-state");
            rightArea.Add(machineStateLabel);

            return content;
        }

        /// <summary>
        /// 刷新摘要区内容。
        /// </summary>
        private void UpdateHudSection()
        {
            if (mSelectedFsm == null)
            {
                return;
            }

            var fsm = mSelectedFsm;
            bool isRunning = fsm.MachineState == MachineState.Running;

            string currentStateName = GetCurrentStateName(fsm);
            mCurrentStateLabel.text = currentStateName;
            mCurrentStateLabel.RemoveFromClassList("yoki-fsm-hud__state--inactive");
            if (!isRunning)
            {
                mCurrentStateLabel.AddToClassList("yoki-fsm-hud__state--inactive");
            }

            var stats = FsmDebugger.GetStats(fsm.Name);
            if (!string.IsNullOrEmpty(stats.PreviousState))
            {
                mPrevStateLabel.text = $"上一状态: {stats.PreviousState}";
                mPrevStateLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                mPrevStateLabel.style.display = DisplayStyle.None;
            }

            float duration = FsmDebugger.GetStateDuration(fsm.Name);
            mDurationLabel.text = isRunning ? $"{duration:F1}s" : "--";

            var machineStateLabel = mHudSection.Q<Label>("machine-state");
            if (machineStateLabel == null)
            {
                return;
            }

            machineStateLabel.RemoveFromClassList("yoki-fsm-hud__machine-state--running");
            machineStateLabel.RemoveFromClassList("yoki-fsm-hud__machine-state--suspended");
            machineStateLabel.RemoveFromClassList("yoki-fsm-hud__machine-state--stopped");

            var (stateText, stateClass) = fsm.MachineState switch
            {
                MachineState.Running => ("运行中", "yoki-fsm-hud__machine-state--running"),
                MachineState.Suspend => ("已挂起", "yoki-fsm-hud__machine-state--suspended"),
                MachineState.End => ("已停止", "yoki-fsm-hud__machine-state--stopped"),
                _ => ("未知", "yoki-fsm-hud__machine-state--stopped")
            };

            machineStateLabel.text = stateText;
            machineStateLabel.AddToClassList(stateClass);
        }

        #endregion
    }
}
#endif
