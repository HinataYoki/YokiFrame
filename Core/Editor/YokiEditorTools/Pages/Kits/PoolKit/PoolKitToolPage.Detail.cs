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

        private ScrollView mActiveObjectsScrollView;
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

            mActiveObjectsScrollView = new ScrollView(ScrollViewMode.Vertical);
            mActiveObjectsScrollView.style.flexGrow = 1;
            section.Add(mActiveObjectsScrollView);

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
            if (mActiveObjectsScrollView == default) return;

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

            RebuildActiveObjectCards();
        }

        private void RebuildActiveObjectCards()
        {
            mActiveObjectsScrollView.Clear();

            for (int i = 0; i < mFilteredActiveObjects.Count; i++)
            {
                var card = CreateActiveObjectCard(mFilteredActiveObjects[i], i);
                mActiveObjectsScrollView.Add(card);
            }

            if (mFilteredActiveObjects.Count == 0)
            {
                var emptyHint = new Label("无活跃对象")
                {
                    style =
                    {
                        fontSize = 13,
                        color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary),
                        unityTextAlign = TextAnchor.MiddleCenter,
                        marginTop = 20
                    }
                };
                mActiveObjectsScrollView.Add(emptyHint);
            }
        }

        #endregion
        // 卡片创建逻辑已移至 PoolKitToolPage.Card.cs
    }
}
#endif
