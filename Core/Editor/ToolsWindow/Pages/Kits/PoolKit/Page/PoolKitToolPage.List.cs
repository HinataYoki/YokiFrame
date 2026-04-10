#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 左侧对象池列表区。
    /// </summary>
    public partial class PoolKitToolPage
    {
        /// <summary>
        /// 构建左侧对象池列表面板。
        /// </summary>
        private VisualElement BuildLeftPanel()
        {
            var (leftPanel, body) = CreateKitSectionPanel(
                "对象池列表",
                "展示当前已注册对象池的容量、使用率和健康状态。",
                KitIcons.POOLKIT);
            leftPanel.AddToClassList("left-panel");
            leftPanel.AddToClassList("yoki-kit-panel--blue");

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
            body.Add(mPoolListView);

            return leftPanel;
        }

        /// <summary>
        /// 创建对象池列表项模板。
        /// </summary>
        private VisualElement MakePoolListItem()
        {
            var container = new VisualElement { name = "pool-item-container" };
            container.style.height = LIST_ITEM_HEIGHT;
            container.style.position = Position.Relative;

            var progressBg = new VisualElement { name = "progress-bg" };
            progressBg.style.position = Position.Absolute;
            progressBg.style.left = 0;
            progressBg.style.bottom = 0;
            progressBg.style.height = LIST_ITEM_HEIGHT;
            progressBg.style.opacity = 0.25f;
            container.Add(progressBg);

            var item = new VisualElement();
            item.AddToClassList("list-item");
            item.style.height = LIST_ITEM_HEIGHT;
            item.style.paddingTop = item.style.paddingBottom = 6;
            item.style.paddingLeft = item.style.paddingRight = 8;
            item.style.flexDirection = FlexDirection.Column;
            item.style.justifyContent = Justify.Center;
            item.style.position = Position.Relative;
            container.Add(item);

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

            var indicator = new VisualElement { name = "indicator" };
            indicator.style.width = indicator.style.height = 8;
            indicator.style.borderTopLeftRadius = indicator.style.borderTopRightRadius =
                indicator.style.borderBottomLeftRadius = indicator.style.borderBottomRightRadius = 4;
            indicator.style.marginRight = 8;
            row1.Add(indicator);

            var nameLabel = new Label { name = "pool-name" };
            nameLabel.style.fontSize = 12;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            nameLabel.style.flexGrow = 1;
            row1.Add(nameLabel);

            var countBadge = new Label { name = "count-badge" };
            countBadge.style.fontSize = 10;
            countBadge.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            countBadge.style.paddingLeft = countBadge.style.paddingRight = 6;
            countBadge.style.paddingTop = countBadge.style.paddingBottom = 2;
            countBadge.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.17f));
            countBadge.style.borderTopLeftRadius = countBadge.style.borderTopRightRadius =
                countBadge.style.borderBottomLeftRadius = countBadge.style.borderBottomRightRadius = 4;
            row1.Add(countBadge);

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
        /// 绑定对象池列表项数据。
        /// </summary>
        private void BindPoolListItem(VisualElement element, int index)
        {
            if (index < 0 || index >= mCachedPools.Count)
            {
                return;
            }

            var pool = mCachedPools[index];
            var healthColor = GetHealthColor(pool.HealthStatus);
            var usagePercent = pool.UsageRate * 100f;

            var progressBg = element.Q<VisualElement>("progress-bg");
            progressBg.style.width = new StyleLength(new Length(usagePercent, LengthUnit.Percent));
            progressBg.style.backgroundColor = new StyleColor(healthColor);

            element.Q<VisualElement>("indicator").style.backgroundColor = new StyleColor(healthColor);
            element.Q<Label>("pool-name").text = pool.Name;

            var badge = element.Q<Label>("count-badge");
            if (pool.MaxCacheCount > 0)
            {
                badge.text = $"使用 {pool.ActiveCount} / 池内 {pool.InactiveCount} / 容量 {pool.MaxCacheCount}";
            }
            else
            {
                badge.text = $"使用 {pool.ActiveCount} / 池内 {pool.InactiveCount} / 容量 无上限";
            }

            var progressBar = element.Q<VisualElement>("progress-bar");
            progressBar.style.width = new StyleLength(new Length(usagePercent, LengthUnit.Percent));
            progressBar.style.backgroundColor = new StyleColor(healthColor);
        }

        /// <summary>
        /// 处理对象池选中变化。
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
        /// 获取健康状态对应的颜色。
        /// </summary>
        private static Color GetHealthColor(PoolHealthStatus status) => status switch
        {
            PoolHealthStatus.Healthy => new Color(0.13f, 0.59f, 0.95f),
            PoolHealthStatus.Normal => new Color(0.71f, 0.73f, 0.76f),
            PoolHealthStatus.Busy => new Color(1f, 0.60f, 0f),
            PoolHealthStatus.Warning => new Color(1f, 0.32f, 0.32f),
            _ => YokiFrameUIComponents.Colors.TextTertiary
        };
    }
}
#endif
