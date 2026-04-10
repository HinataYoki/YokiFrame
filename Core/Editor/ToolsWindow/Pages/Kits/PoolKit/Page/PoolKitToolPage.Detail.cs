#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKit 活跃对象主监视区。
    /// </summary>
    public partial class PoolKitToolPage
    {
        #region 字段

        private ListView mActiveObjectsListView;
        private readonly HashSet<int> mExpandedCards = new();
        private readonly List<ActiveObjectInfo> mFilteredActiveObjects = new(64);

        #endregion

        #region 构建 UI

        private VisualElement BuildActiveObjectsSection()
        {
            var (section, body) = CreateKitSectionPanel(
                "活跃对象",
                "优先排查当前借出且仍未归还的对象。",
                KitIcons.POOLKIT);
            section.AddToClassList("yoki-monitor-primary-panel");
            section.AddToClassList("yoki-kit-panel--green");
            section.style.minHeight = 180;
            section.style.marginBottom = 10;

            mActiveObjectsListView = new ListView
            {
                itemsSource = mFilteredActiveObjects,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.None,
                showBorder = false,
                showAlternatingRowBackgrounds = AlternatingRowBackground.None,
                makeItem = () =>
                {
                    var container = new VisualElement();
                    container.style.flexGrow = 1;
                    return container;
                },
                bindItem = (element, index) =>
                {
                    if (index < 0 || index >= mFilteredActiveObjects.Count)
                    {
                        return;
                    }

                    var existingCard = element.childCount > 0 ? element[0] : null;
                    var info = mFilteredActiveObjects[index];

                    if (existingCard != default && existingCard.userData is ActiveObjectInfo oldInfo)
                    {
                        if (ReferenceEquals(oldInfo.Obj, info.Obj) && Mathf.Approximately(oldInfo.SpawnTime, info.SpawnTime))
                        {
                            UpdateCardContent(existingCard, info, index);
                            return;
                        }
                    }

                    element.Clear();
                    var card = CreateActiveObjectCard(info, index);
                    element.Add(card);
                },
                unbindItem = (_, _) =>
                {
                }
            };
            mActiveObjectsListView.style.flexGrow = 1;
            body.Add(mActiveObjectsListView);

            return section;
        }

        #endregion

        #region 数据更新

        private void UpdateActiveObjectsList()
        {
            if (mActiveObjectsListView == default)
            {
                return;
            }

            mFilteredActiveObjects.Clear();

            if (mSelectedPool != default)
            {
                for (int i = 0; i < mSelectedPool.ActiveObjects.Count; i++)
                {
                    var activeInfo = mSelectedPool.ActiveObjects[i];
                    mFilteredActiveObjects.Add(activeInfo);
                }
            }

            mActiveObjectsListView.RefreshItems();
        }

        #endregion

        #region 卡片更新

        /// <summary>
        /// 更新卡片内容（不重建，保持展开状态和事件绑定）。
        /// </summary>
        private void UpdateCardContent(VisualElement card, ActiveObjectInfo info, int index)
        {
            if (card == default)
            {
                return;
            }

            card.userData = info;

            var usageDuration = Time.realtimeSinceStartup - info.SpawnTime;
            var isLongUsage = usageDuration > LONG_USAGE_WARNING_THRESHOLD;

            var borderColor = isLongUsage
                ? YokiFrameUIComponents.Colors.BrandWarning
                : YokiFrameUIComponents.Colors.BorderDefault;
            card.style.borderLeftColor = card.style.borderRightColor =
                card.style.borderTopColor = card.style.borderBottomColor = new StyleColor(borderColor);

            var header = card.Q("card-header");
            if (header == default)
            {
                return;
            }

            var durationLabel = header.Q<Label>("duration-label");
            if (durationLabel != default)
            {
                var durationText = FormatDuration(usageDuration);
                durationLabel.text = $" | {durationText}";

                var durationColor = isLongUsage
                    ? YokiFrameUIComponents.Colors.BrandWarning
                    : YokiFrameUIComponents.Colors.TextTertiary;
                durationLabel.style.color = new StyleColor(durationColor);

                durationLabel.tooltip = isLongUsage
                    ? $"警告：对象已使用 {durationText}，可能存在泄漏"
                    : $"使用时长：{durationText}";
            }
        }

        #endregion
    }
}
#endif
