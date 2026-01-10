#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKitToolPage - 池列表面板
    /// </summary>
    public partial class PoolKitToolPage
    {
        /// <summary>
        /// 构建左侧面板（池列表）
        /// </summary>
        private VisualElement BuildLeftPanel()
        {
            var leftPanel = new VisualElement();
            leftPanel.AddToClassList("left-panel");

            var leftHeader = YokiFrameUIComponents.CreateSectionHeader("对象池列表");
            leftPanel.Add(leftHeader);

            mPoolListView = new ListView
            {
                fixedItemHeight = LIST_ITEM_HEIGHT,
                makeItem = MakePoolListItem,
                bindItem = BindPoolListItem
            };
#if UNITY_2022_1_OR_NEWER
            mPoolListView.selectionChanged += OnPoolSelected;
#else
            mPoolListView.onSelectionChange += OnPoolSelected;
#endif
            mPoolListView.style.flexGrow = 1;
            leftPanel.Add(mPoolListView);

            return leftPanel;
        }

        /// <summary>
        /// 创建池列表项模板（全宽进度条背景）
        /// </summary>
        private VisualElement MakePoolListItem()
        {
            // 外层容器（用于进度条背景）
            var container = new VisualElement { name = "pool-item-container" };
            container.style.height = LIST_ITEM_HEIGHT;
            container.style.position = Position.Relative;

            // 进度条背景层（绝对定位，铺满底部）
            var progressBg = new VisualElement { name = "progress-bg" };
            progressBg.style.position = Position.Absolute;
            progressBg.style.left = 0;
            progressBg.style.bottom = 0;
            progressBg.style.height = LIST_ITEM_HEIGHT;
            progressBg.style.opacity = 0.25f;
            container.Add(progressBg);

            // 内容层
            var item = new VisualElement();
            item.AddToClassList("list-item");
            item.style.height = LIST_ITEM_HEIGHT;
            item.style.paddingTop = item.style.paddingBottom = 6;
            item.style.paddingLeft = item.style.paddingRight = 8;
            item.style.flexDirection = FlexDirection.Column;
            item.style.justifyContent = Justify.Center;
            item.style.position = Position.Relative;
            container.Add(item);

            // 第一行：池名称 + 借出/总量徽章
            var row1 = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4
                }
            };
            item.Add(row1);

            // 健康状态指示器
            var indicator = new VisualElement { name = "indicator" };
            indicator.style.width = indicator.style.height = 8;
            indicator.style.borderTopLeftRadius = indicator.style.borderTopRightRadius =
                indicator.style.borderBottomLeftRadius = indicator.style.borderBottomRightRadius = 4;
            indicator.style.marginRight = 8;
            row1.Add(indicator);

            // 池名称
            var nameLabel = new Label { name = "pool-name" };
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            nameLabel.style.flexGrow = 1;
            row1.Add(nameLabel);

            // 借出/总量徽章
            var countBadge = new Label { name = "count-badge" };
            countBadge.style.fontSize = 10;
            countBadge.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            countBadge.style.paddingLeft = countBadge.style.paddingRight = 6;
            countBadge.style.paddingTop = countBadge.style.paddingBottom = 2;
            countBadge.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.17f));
            countBadge.style.borderTopLeftRadius = countBadge.style.borderTopRightRadius =
                countBadge.style.borderBottomLeftRadius = countBadge.style.borderBottomRightRadius = 4;
            row1.Add(countBadge);

            // 第二行：使用率进度条（细条）
            var progressContainer = new VisualElement
            {
                name = "progress-container",
                style =
                {
                    height = 3,
                    backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.14f)),
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2,
                    marginLeft = 16
                }
            };
            item.Add(progressContainer);

            var progressBar = new VisualElement { name = "progress-bar" };
            progressBar.style.height = 3;
            progressBar.style.borderTopLeftRadius = progressBar.style.borderTopRightRadius =
                progressBar.style.borderBottomLeftRadius = progressBar.style.borderBottomRightRadius = 2;
            progressContainer.Add(progressBar);

            return container;
        }

        /// <summary>
        /// 绑定池列表项数据（全宽进度条背景）
        /// </summary>
        private void BindPoolListItem(VisualElement element, int index)
        {
            if (index >= mCachedPools.Count) return;

            var pool = mCachedPools[index];
            var healthColor = GetHealthColor(pool.HealthStatus);
            var usagePercent = pool.UsageRate * 100f;

            // 全宽进度条背景
            var progressBg = element.Q<VisualElement>("progress-bg");
            progressBg.style.width = new StyleLength(new Length(usagePercent, LengthUnit.Percent));
            progressBg.style.backgroundColor = new StyleColor(healthColor);

            // 健康状态指示器
            element.Q<VisualElement>("indicator").style.backgroundColor = new StyleColor(healthColor);

            // 池名称
            element.Q<Label>("pool-name").text = pool.Name;

            // 借出/总量徽章
            element.Q<Label>("count-badge").text = $"{pool.ActiveCount} / {pool.TotalCount}";

            // 细条进度条
            var progressBar = element.Q<VisualElement>("progress-bar");
            progressBar.style.width = new StyleLength(new Length(usagePercent, LengthUnit.Percent));
            progressBar.style.backgroundColor = new StyleColor(healthColor);
        }

        /// <summary>
        /// 池选择事件
        /// </summary>
        private void OnPoolSelected(IEnumerable<object> selection)
        {
            foreach (var item in selection)
            {
                if (item is PoolDebugInfo pool)
                {
                    mSelectedPool = pool;
                    UpdateRightPanel();
                    return;
                }
            }
        }

        /// <summary>
        /// 获取健康状态对应颜色
        /// </summary>
        private static Color GetHealthColor(PoolHealthStatus status) => status switch
        {
            PoolHealthStatus.Healthy => new Color(0.13f, 0.59f, 0.95f),  // 蓝色 #2196F3
            PoolHealthStatus.Normal => new Color(0.71f, 0.73f, 0.76f),   // 默认灰色
            PoolHealthStatus.Busy => new Color(1f, 0.60f, 0f),           // 橙色 #FF9800
            PoolHealthStatus.Warning => new Color(1f, 0.32f, 0.32f),     // 红色 #FF5252
            _ => YokiFrameUIComponents.Colors.TextTertiary
        };
    }
}
#endif
