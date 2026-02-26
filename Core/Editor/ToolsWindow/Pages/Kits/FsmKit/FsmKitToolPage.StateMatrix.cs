#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 工具页面 - 状态矩阵区域
    /// 以网格形式展示所有状态，高亮当前状态
    /// </summary>
    public partial class FsmKitToolPage
    {
        #region 字段

        private VisualElement mMatrixContainer;

        #endregion

        #region 构建状态矩阵

        /// <summary>
        /// 构建状态矩阵区域
        /// </summary>
        private VisualElement BuildMatrixSection()
        {
            var section = new VisualElement { name = "matrix-section" };
            section.AddToClassList("yoki-state-matrix");

            // 标题
            var header = new VisualElement();
            header.AddToClassList("yoki-state-matrix__header");
            section.Add(header);

            var titleIcon = new Image { image = KitIcons.GetTexture(KitIcons.CHART) };
            titleIcon.AddToClassList("yoki-state-matrix__icon");
            header.Add(titleIcon);

            var titleLabel = new Label("状态矩阵");
            titleLabel.AddToClassList("yoki-state-matrix__title");
            header.Add(titleLabel);

            // 图例
            var legend = CreateLegend();
            header.Add(legend);

            // 状态卡片容器（Flex-Wrap 网格）
            mMatrixContainer = new VisualElement { name = "matrix-container" };
            mMatrixContainer.AddToClassList("yoki-state-matrix__container");
            section.Add(mMatrixContainer);

            return section;
        }

        /// <summary>
        /// 创建图例
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
        /// 创建图例项
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
        /// 更新状态矩阵
        /// </summary>
        private void UpdateMatrixSection()
        {
            if (mMatrixContainer == null || mSelectedFsm == null) return;

            mMatrixContainer.Clear();

            var fsm = mSelectedFsm;
            var states = fsm.GetAllStates();
            var currentId = fsm.CurrentStateId;
            var stats = FsmDebugger.GetStats(fsm.Name);

            foreach (var kvp in states)
            {
                var stateId = kvp.Key;
                var stateName = Enum.GetName(fsm.EnumType, stateId) ?? stateId.ToString();

                var isActive = stateId == currentId && fsm.MachineState == MachineState.Running;
                var isVisited = stats.VisitedStates.Contains(stateName);
                stats.StateVisitCounts.TryGetValue(stateName, out var visitCount);

                var card = CreateStateCard(stateName, isActive, isVisited, visitCount);
                mMatrixContainer.Add(card);
            }

            // 如果没有状态
            if (states.Count == 0)
            {
                var emptyLabel = new Label("暂无注册状态");
                emptyLabel.AddToClassList("yoki-fsm-empty__text");
                mMatrixContainer.Add(emptyLabel);
            }
        }

        /// <summary>
        /// 创建状态卡片
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

            // 状态指示器
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

            // 状态名称
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

            // 访问次数
            if (visitCount > 0)
            {
                var countLabel = new Label($"×{visitCount}");
                countLabel.AddToClassList("yoki-state-card__count");
                card.Add(countLabel);
            }

            // 悬停效果
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
