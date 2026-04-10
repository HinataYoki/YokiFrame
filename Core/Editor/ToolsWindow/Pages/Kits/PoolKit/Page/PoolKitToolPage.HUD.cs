#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 顶部摘要区。
    /// </summary>
    public partial class PoolKitToolPage
    {
        #region Fields

        private VisualElement mHudCardRow;
        private VisualElement mHudEmptyState;

        #endregion

        /// <summary>
        /// 构建顶部摘要面板。
        /// </summary>
        private VisualElement BuildHudSection()
        {
            var (panel, body) = CreateKitSectionPanel(
                "对象池摘要",
                "先看总体规模，再进入活跃对象排查。",
                KitIcons.CHART);
            panel.AddToClassList("yoki-pool-hud");
            panel.AddToClassList("yoki-kit-panel--slate");
            panel.style.flexGrow = 0;
            panel.style.marginBottom = 10;

            mHudCardRow = CreateKitMetricStrip();
            mHudCardRow.name = "hud-card-row";
            mHudCardRow.style.display = DisplayStyle.None;
            body.Add(mHudCardRow);

            var totalResult = CreateKitMetricCard("总数", "0", "当前池内对象总量", YokiFrameUIComponents.Colors.TextSecondary);
            mTotalLabel = totalResult.valueLabel;
            mHudCardRow.Add(totalResult.card);

            var activeResult = CreateKitMetricCard("使用中", "0", "当前借出的对象数量", YokiFrameUIComponents.Colors.BrandWarning);
            mActiveLabel = activeResult.valueLabel;
            mHudCardRow.Add(activeResult.card);

            var inactiveResult = CreateKitMetricCard("池内", "0", "当前可复用的缓存对象", YokiFrameUIComponents.Colors.BrandSuccess);
            mInactiveLabel = inactiveResult.valueLabel;
            mHudCardRow.Add(inactiveResult.card);

            var peakResult = CreateKitMetricCard("峰值", "0", "历史最高占用数量", YokiFrameUIComponents.Colors.BrandPrimary);
            mPeakLabel = peakResult.valueLabel;
            mHudCardRow.Add(peakResult.card);

            mHudEmptyState = BuildHudEmptyState();
            body.Add(mHudEmptyState);

            mHudSection = body;
            return panel;
        }

        /// <summary>
        /// 构建未选中对象池时的空状态。
        /// </summary>
        private VisualElement BuildHudEmptyState()
        {
            var container = new VisualElement
            {
                name = "hud-empty-state",
                style =
                {
                    flexDirection = FlexDirection.Column,
                    justifyContent = Justify.Center,
                    alignItems = Align.Stretch,
                    minHeight = 92
                }
            };

            var emptyState = CreateEmptyState(
                KitIcons.POOLKIT,
                "未选择对象池",
                "先在左侧选择一个对象池，再查看指标、活跃对象和事件日志。");
            container.Add(emptyState);

            return container;
        }

        /// <summary>
        /// 根据当前选中的对象池刷新摘要区。
        /// </summary>
        private void UpdateHudSection()
        {
            if (mSelectedPool == default)
            {
                mHudCardRow.style.display = DisplayStyle.None;
                mHudEmptyState.style.display = DisplayStyle.Flex;
                return;
            }

            mHudCardRow.style.display = DisplayStyle.Flex;
            mHudEmptyState.style.display = DisplayStyle.None;

            if (mTotalLabel == default)
            {
                return;
            }

            mTotalLabel.text = mSelectedPool.TotalCount.ToString();
            mActiveLabel.text = mSelectedPool.ActiveCount.ToString();
            mInactiveLabel.text = mSelectedPool.InactiveCount.ToString();
            mPeakLabel.text = mSelectedPool.PeakCount.ToString();

            var activeColor = mSelectedPool.UsageRate > 0.8f
                ? YokiFrameUIComponents.Colors.BrandDanger
                : YokiFrameUIComponents.Colors.BrandWarning;
            mActiveLabel.style.color = new StyleColor(activeColor);
        }
    }
}
#endif
