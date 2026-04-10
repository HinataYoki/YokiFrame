#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 状态矩阵区。
    /// </summary>
    public partial class FsmKitToolPage
    {
        #region Fields

        private VisualElement mMatrixContainer;

        #endregion

        #region Matrix

        /// <summary>
        /// 构建状态矩阵面板。
        /// </summary>
        private VisualElement BuildMatrixSection()
        {
            var legend = CreateLegend();
            var (panel, body) = CreateKitSectionPanel(
                "状态矩阵",
                "用卡片区分当前状态、已访问状态和未触达状态。",
                KitIcons.CHART,
                legend);
            panel.name = "matrix-section";
            panel.AddToClassList("yoki-state-matrix");
            panel.AddToClassList("yoki-kit-panel--green");
            panel.AddToClassList("yoki-monitor-primary-panel");
            panel.style.minHeight = 220;
            panel.style.marginBottom = 10;

            mMatrixContainer = new VisualElement { name = "matrix-container" };
            mMatrixContainer.AddToClassList("yoki-state-matrix__container");
            body.Add(mMatrixContainer);

            return panel;
        }

        /// <summary>
        /// 创建状态图例。
        /// </summary>
        private VisualElement CreateLegend()
        {
            var legend = new VisualElement();
            legend.AddToClassList("yoki-legend");

            legend.Add(CreateLegendItem(KitIcons.DOT_FILLED, YokiFrameUIComponents.Colors.BrandSuccess, "当前"));
            legend.Add(CreateLegendItem(KitIcons.DOT_EMPTY, YokiFrameUIComponents.Colors.TextSecondary, "已访问"));
            legend.Add(CreateLegendItem(KitIcons.DOT_EMPTY, YokiFrameUIComponents.Colors.TextTertiary, "未触达"));

            return legend;
        }

        /// <summary>
        /// 创建图例项。
        /// </summary>
        private VisualElement CreateLegendItem(string iconId, Color color, string text)
        {
            var item = new VisualElement();
            item.AddToClassList("yoki-legend__item");

            var iconImg = new Image { image = KitIcons.GetTexture(iconId) };
            iconImg.AddToClassList("yoki-legend__icon");
            iconImg.tintColor = color;
            item.Add(iconImg);

            var textLabel = new Label(text);
            textLabel.AddToClassList("yoki-legend__label");
            item.Add(textLabel);

            return item;
        }

        /// <summary>
        /// 刷新状态矩阵。
        /// </summary>
        private void UpdateMatrixSection()
        {
            if (mMatrixContainer == null || mSelectedFsm == null)
            {
                return;
            }

            mMatrixContainer.Clear();

            var fsm = mSelectedFsm;
            var states = fsm.GetAllStates();
            int currentId = fsm.CurrentStateId;
            var stats = FsmDebugger.GetStats(fsm.Name);

            foreach (var kvp in states)
            {
                int stateId = kvp.Key;
                string stateName = Enum.GetName(fsm.EnumType, stateId) ?? stateId.ToString();

                bool isActive = stateId == currentId && fsm.MachineState == MachineState.Running;
                bool isVisited = stats.VisitedStates.Contains(stateName);
                stats.StateVisitCounts.TryGetValue(stateName, out int visitCount);

                mMatrixContainer.Add(CreateStateCard(stateName, isActive, isVisited, visitCount));
            }

            if (states.Count == 0)
            {
                var emptyLabel = new Label("当前状态机尚未注册任何状态。");
                emptyLabel.AddToClassList("yoki-fsm-empty__text");
                mMatrixContainer.Add(emptyLabel);
            }
        }

        /// <summary>
        /// 创建状态卡片。
        /// </summary>
        private VisualElement CreateStateCard(string stateName, bool isActive, bool isVisited, int visitCount)
        {
            var card = new VisualElement();
            card.AddToClassList("yoki-state-card");

            if (isActive)
            {
                card.AddToClassList("yoki-state-card--active");
            }
            else if (isVisited)
            {
                card.AddToClassList("yoki-state-card--visited");
            }
            else
            {
                card.AddToClassList("yoki-state-card--unvisited");
            }

            var indicator = new VisualElement();
            indicator.AddToClassList("yoki-state-card__indicator");
            if (isActive)
            {
                indicator.AddToClassList("yoki-state-card__indicator--active");
            }
            else if (isVisited)
            {
                indicator.AddToClassList("yoki-state-card__indicator--visited");
            }
            else
            {
                indicator.AddToClassList("yoki-state-card__indicator--unvisited");
            }
            card.Add(indicator);

            var nameLabel = new Label(stateName);
            nameLabel.AddToClassList("yoki-state-card__name");
            if (isActive)
            {
                nameLabel.AddToClassList("yoki-state-card__name--active");
            }
            else if (isVisited)
            {
                nameLabel.AddToClassList("yoki-state-card__name--visited");
            }
            else
            {
                nameLabel.AddToClassList("yoki-state-card__name--unvisited");
            }
            card.Add(nameLabel);

            if (visitCount > 0)
            {
                var countLabel = new Label($"x{visitCount}");
                countLabel.AddToClassList("yoki-state-card__count");
                card.Add(countLabel);
            }

            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (!isActive)
                {
                    card.AddToClassList("yoki-state-card--hover");
                }
            });

            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                card.RemoveFromClassList("yoki-state-card--hover");
            });

            return card;
        }

        #endregion
    }
}
#endif
