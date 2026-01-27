#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKitToolPage - 活跃对象详情面板
    /// 可折叠卡片式布局，展开显示详细调用堆栈
    /// </summary>
    public partial class PoolKitToolPage
    {
        #region 常量

        private const float STACK_FRAME_HEIGHT = 22f;

        #endregion

        #region 字段

        private ListView mActiveObjectsListView;
        private readonly HashSet<int> mExpandedCards = new();
        private readonly List<ActiveObjectInfo> mFilteredActiveObjects = new(64);

        #endregion
        #region 构建 UI

        private VisualElement BuildActiveObjectsSection()
        {
            var section = new VisualElement();
            section.style.flexGrow = 1;
            section.style.flexDirection = FlexDirection.Column;
            section.style.minHeight = 150;

            var toolbar = BuildActiveObjectsToolbar();
            section.Add(toolbar);

            // 使用 ListView 虚拟化，提升大数据量性能
            mActiveObjectsListView = new ListView
            {
                itemsSource = mFilteredActiveObjects,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight, // 动态高度支持展开
                selectionType = SelectionType.None,
                makeItem = () =>
                {
                    // 创建容器，复用卡片实例
                    var container = new VisualElement();
                    container.style.flexGrow = 1;
                    return container;
                },
                bindItem = (element, index) =>
                {
                    if (index < 0 || index >= mFilteredActiveObjects.Count) return;
                    
                    // 检查是否已有卡片，如果有则更新，否则创建新卡片
                    var existingCard = element.childCount > 0 ? element[0] : null;
                    var info = mFilteredActiveObjects[index];
                    
                    if (existingCard != default && existingCard.userData is ActiveObjectInfo oldInfo)
                    {
                        // 如果是同一个对象，仅更新内容，不重建卡片
                        if (oldInfo.Obj == info.Obj && oldInfo.SpawnTime == info.SpawnTime)
                        {
                            UpdateCardContent(existingCard, info, index);
                            return;
                        }
                    }
                    
                    // 不同对象或首次创建，重建卡片
                    element.Clear();
                    var card = CreateActiveObjectCard(info, index);
                    element.Add(card);
                },
                unbindItem = (element, index) =>
                {
                    // 保留卡片实例，不清空
                }
            };
            mActiveObjectsListView.style.flexGrow = 1;
            section.Add(mActiveObjectsListView);

            return section;
        }

        private VisualElement BuildActiveObjectsToolbar()
        {
            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 34,
                    paddingLeft = 8,
                    paddingRight = 8,
                    backgroundColor = new StyleColor(new Color(0.13f, 0.13f, 0.15f)),
                    borderBottomWidth = 1,
                    borderBottomColor = new StyleColor(YokiFrameUIComponents.Colors.BorderLight)
                }
            };

            var title = new Label("活跃对象")
            {
                style =
                {
                    fontSize = 13,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary),
                    flexGrow = 1
                }
            };
            toolbar.Add(title);

            return toolbar;
        }

        #endregion
        #region 数据更新

        private void UpdateActiveObjectsList()
        {
            if (mActiveObjectsListView == default) return;

            mFilteredActiveObjects.Clear();

            if (mSelectedPool != default)
            {
                var hasSearchFilter = !string.IsNullOrEmpty(mSearchFilter);
                
                for (int i = 0; i < mSelectedPool.ActiveObjects.Count; i++)
                {
                    var obj = mSelectedPool.ActiveObjects[i];
                    
                    // 搜索过滤
                    if (hasSearchFilter)
                    {
                        var objName = GetObjectDisplayName(obj.Obj);
                        if (!objName.Contains(mSearchFilter, System.StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    
                    mFilteredActiveObjects.Add(obj);
                }
            }

            // 仅刷新数据，不重建元素（避免中断交互）
            mActiveObjectsListView.RefreshItems();
        }

        #endregion
        
        #region 卡片更新

        /// <summary>
        /// 更新卡片内容（不重建，保持展开状态和事件绑定）
        /// </summary>
        private void UpdateCardContent(VisualElement card, ActiveObjectInfo info, int index)
        {
            if (card == default) return;
            
            // 更新 userData
            card.userData = info;
            
            // 计算使用时长
            var usageDuration = Time.realtimeSinceStartup - info.SpawnTime;
            var isLongUsage = usageDuration > LONG_USAGE_WARNING_THRESHOLD;
            
            // 更新边框颜色
            var borderColor = isLongUsage 
                ? YokiFrameUIComponents.Colors.BrandWarning 
                : YokiFrameUIComponents.Colors.BorderDefault;
            card.style.borderLeftColor = card.style.borderRightColor =
                card.style.borderTopColor = card.style.borderBottomColor = new StyleColor(borderColor);
            
            // 更新 header 中的时长标签
            var header = card.Q("card-header");
            if (header != default)
            {
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
        }

        #endregion
        // 卡片创建逻辑已移至 PoolKitToolPage.Card.cs
    }
}
#endif
