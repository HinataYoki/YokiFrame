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
            var section = new VisualElement();
            section.name = "matrix-section";
            section.style.flexGrow = 1;
            section.style.minHeight = 100;
            section.style.paddingLeft = YokiFrameUIComponents.Spacing.MD;
            section.style.paddingRight = YokiFrameUIComponents.Spacing.MD;
            section.style.paddingTop = YokiFrameUIComponents.Spacing.MD;
            section.style.paddingBottom = YokiFrameUIComponents.Spacing.MD;
            section.style.borderBottomWidth = 1;
            section.style.borderBottomColor = new StyleColor(YokiFrameUIComponents.Colors.BorderDefault);

            // 标题
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = YokiFrameUIComponents.Spacing.SM;
            section.Add(header);

            var titleIcon = new Image { image = KitIcons.GetTexture(KitIcons.CHART) };
            titleIcon.style.width = 14;
            titleIcon.style.height = 14;
            titleIcon.style.marginRight = 4;
            header.Add(titleIcon);
            
            var titleLabel = new Label("状态矩阵");
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextSecondary);
            header.Add(titleLabel);

            // 图例
            var legend = CreateLegend();
            legend.style.marginLeft = YokiFrameUIComponents.Spacing.LG;
            header.Add(legend);

            // 状态卡片容器（Flex-Wrap 网格）
            mMatrixContainer = new VisualElement();
            mMatrixContainer.name = "matrix-container";
            mMatrixContainer.style.flexDirection = FlexDirection.Row;
            mMatrixContainer.style.flexWrap = Wrap.Wrap;
            mMatrixContainer.style.alignContent = Align.FlexStart;
            section.Add(mMatrixContainer);

            return section;
        }

        /// <summary>
        /// 创建图例
        /// </summary>
        private VisualElement CreateLegend()
        {
            var legend = new VisualElement();
            legend.style.flexDirection = FlexDirection.Row;
            legend.style.alignItems = Align.Center;

            // 当前状态
            legend.Add(CreateLegendItem("●", YokiFrameUIComponents.Colors.BrandSuccess, "当前"));
            // 已访问
            legend.Add(CreateLegendItem("○", YokiFrameUIComponents.Colors.TextSecondary, "已访问"));
            // 未触达
            legend.Add(CreateLegendItem("○", YokiFrameUIComponents.Colors.TextTertiary, "未触达"));

            return legend;
        }

        /// <summary>
        /// 创建图例项
        /// </summary>
        private VisualElement CreateLegendItem(string icon, Color color, string text)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.marginRight = YokiFrameUIComponents.Spacing.MD;

            var iconLabel = new Label(icon);
            iconLabel.style.fontSize = 10;
            iconLabel.style.color = new StyleColor(color);
            iconLabel.style.marginRight = 2;
            item.Add(iconLabel);

            var textLabel = new Label(text);
            textLabel.style.fontSize = 9;
            textLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
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

                var card = YokiFrameUIComponents.CreateStateCard(stateName, isActive, isVisited, visitCount);
                
                // 悬停效果
                card.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    if (!isActive)
                    {
                        card.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerHover);
                    }
                });
                card.RegisterCallback<MouseLeaveEvent>(_ =>
                {
                    if (!isActive)
                    {
                        card.style.backgroundColor = new StyleColor(isVisited 
                            ? new Color(0.22f, 0.22f, 0.25f) 
                            : new Color(0.15f, 0.15f, 0.17f));
                    }
                });

                mMatrixContainer.Add(card);
            }

            // 如果没有状态
            if (states.Count == 0)
            {
                var emptyLabel = new Label("暂无注册状态");
                emptyLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
                emptyLabel.style.fontSize = 11;
                mMatrixContainer.Add(emptyLabel);
            }
        }

        #endregion
    }
}
