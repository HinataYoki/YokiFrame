using UnityEngine;
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
        #region 常量

        private const float HUD_HEIGHT = 120f;

        #endregion

        #region 构建 HUD

        /// <summary>
        /// 构建 HUD 区域（外层容器）
        /// </summary>
        private VisualElement BuildHudSection()
        {
            var section = new VisualElement();
            section.name = "hud-section";
            section.style.height = HUD_HEIGHT;
            section.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f));
            section.style.borderBottomWidth = 2;
            section.style.borderBottomColor = new StyleColor(YokiFrameUIComponents.Colors.BorderDefault);

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
            content.style.flexGrow = 1;
            content.style.paddingLeft = YokiFrameUIComponents.Spacing.LG;
            content.style.paddingRight = YokiFrameUIComponents.Spacing.LG;
            content.style.paddingTop = YokiFrameUIComponents.Spacing.MD;
            content.style.paddingBottom = YokiFrameUIComponents.Spacing.MD;
            content.style.flexDirection = FlexDirection.Row;
            content.style.justifyContent = Justify.SpaceBetween;
            content.style.alignItems = Align.Center;

            // 左侧：状态信息
            var leftArea = new VisualElement { style = { flexGrow = 1 } };
            content.Add(leftArea);

            // 小标题
            var titleLabel = new Label("CURRENT STATE");
            titleLabel.style.fontSize = 10;
            titleLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            titleLabel.style.letterSpacing = 1;
            leftArea.Add(titleLabel);

            // 当前状态（大字）
            mCurrentStateLabel = new Label("—") { name = "current-state-big" };
            mCurrentStateLabel.style.fontSize = 32;
            mCurrentStateLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            mCurrentStateLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.BrandPrimary);
            mCurrentStateLabel.style.marginTop = 4;
            leftArea.Add(mCurrentStateLabel);

            // 上一状态
            mPrevStateLabel = new Label("") { name = "prev-state" };
            mPrevStateLabel.style.fontSize = 11;
            mPrevStateLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            mPrevStateLabel.style.marginTop = 4;
            leftArea.Add(mPrevStateLabel);

            // 右侧：计时器
            var rightArea = new VisualElement { style = { alignItems = Align.FlexEnd } };
            content.Add(rightArea);

            // 持续时间标签
            var durationTitle = new Label("DURATION");
            durationTitle.style.fontSize = 10;
            durationTitle.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            durationTitle.style.letterSpacing = 1;
            rightArea.Add(durationTitle);

            // 持续时间（大数字）
            mDurationLabel = new Label("0.0s") { name = "duration-big" };
            mDurationLabel.style.fontSize = 28;
            mDurationLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            mDurationLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            mDurationLabel.style.marginTop = 4;
            rightArea.Add(mDurationLabel);

            // 机器状态
            var machineStateLabel = new Label("") { name = "machine-state" };
            machineStateLabel.style.fontSize = 10;
            machineStateLabel.style.marginTop = 4;
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
            mCurrentStateLabel.style.color = new StyleColor(isRunning 
                ? YokiFrameUIComponents.Colors.BrandPrimary 
                : YokiFrameUIComponents.Colors.TextTertiary);

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
                var (stateText, stateColor) = fsm.MachineState switch
                {
                    MachineState.Running => ("● Running", YokiFrameUIComponents.Colors.BrandSuccess),
                    MachineState.Suspend => ("◐ Suspended", YokiFrameUIComponents.Colors.BrandWarning),
                    MachineState.End => ("○ Stopped", YokiFrameUIComponents.Colors.TextTertiary),
                    _ => ("?", YokiFrameUIComponents.Colors.TextTertiary)
                };
                machineStateLabel.text = stateText;
                machineStateLabel.style.color = new StyleColor(stateColor);
            }
        }

        #endregion
    }
}
