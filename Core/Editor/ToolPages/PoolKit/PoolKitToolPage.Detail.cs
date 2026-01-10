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
        private const float WARNING_THRESHOLD = 10f;
        private const float DANGER_THRESHOLD = 30f;
        private const float DURATION_UPDATE_INTERVAL = 1f;

        #endregion

        #region 字段

        private bool mShowLeaksOnly;
        private bool mSortByDuration;
        private Button mLeaksOnlyToggle;
        private Button mSortByDurationToggle;
        private readonly List<ActiveObjectInfo> mFilteredActiveObjects = new(64);
        private ScrollView mActiveObjectsScrollView;
        private readonly HashSet<int> mExpandedCards = new();
        private readonly HashSet<int> mLastObjectIds = new();  // 追踪上次的对象ID，用于增量更新
        private IVisualElementScheduledItem mDurationUpdateScheduler;  // 时间更新调度器

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

            var filterGroup = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            toolbar.Add(filterGroup);

            mLeaksOnlyToggle = YokiFrameUIComponents.CreateFilterButton("仅泄露", false, () =>
            {
                mShowLeaksOnly = !mShowLeaksOnly;
                YokiFrameUIComponents.SetFilterButtonActive(mLeaksOnlyToggle, mShowLeaksOnly);
                UpdateActiveObjectsList();
            });
            mLeaksOnlyToggle.style.fontSize = 12;

            mLeaksOnlyToggle.style.height = 22;

            filterGroup.Add(mLeaksOnlyToggle);

            mSortByDurationToggle = YokiFrameUIComponents.CreateFilterButton("按时长", false, () =>
            {
                mSortByDuration = !mSortByDuration;
                YokiFrameUIComponents.SetFilterButtonActive(mSortByDurationToggle, mSortByDuration);
                UpdateActiveObjectsList();
            });
            mSortByDurationToggle.style.fontSize = 12;

            mSortByDurationToggle.style.height = 22;

            filterGroup.Add(mSortByDurationToggle);

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
                var now = Time.realtimeSinceStartup;
                var hasSearchFilter = !string.IsNullOrEmpty(mSearchFilter);
                
                for (int i = 0; i < mSelectedPool.ActiveObjects.Count; i++)
                {
                    var obj = mSelectedPool.ActiveObjects[i];
                    
                    // 泄露过滤
                    if (mShowLeaksOnly)
                    {
                        var duration = now - obj.SpawnTime;
                        if (duration < PoolDebugger.LEAK_THRESHOLD_SECONDS) continue;
                    }
                    
                    // 搜索过滤
                    if (hasSearchFilter)
                    {
                        var objName = GetObjectDisplayName(obj.Obj);
                        if (!objName.Contains(mSearchFilter, System.StringComparison.OrdinalIgnoreCase))
                            continue;
                    }
                    
                    mFilteredActiveObjects.Add(obj);
                }

                if (mSortByDuration)
                {
                    mFilteredActiveObjects.Sort(static (a, b) => a.SpawnTime.CompareTo(b.SpawnTime));
                }
            }

            RebuildActiveObjectCards();
        }

        private void RebuildActiveObjectCards()
        {
            // 停止之前的时间更新调度
            StopDurationUpdateScheduler();
            
            mActiveObjectsScrollView.Clear();

            for (int i = 0; i < mFilteredActiveObjects.Count; i++)
            {
                var card = CreateActiveObjectCard(mFilteredActiveObjects[i], i);
                mActiveObjectsScrollView.Add(card);
            }

            if (mFilteredActiveObjects.Count == 0)
            {
                var emptyHint = new Label(mShowLeaksOnly ? "无泄露对象" : "无活跃对象")
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
            else
            {
                // 启动时间更新调度器
                StartDurationUpdateScheduler();
            }
        }

        /// <summary>
        /// 启动时间更新调度器
        /// </summary>
        private void StartDurationUpdateScheduler()
        {
            if (mActiveObjectsScrollView == default) return;
            
            mDurationUpdateScheduler = mActiveObjectsScrollView.schedule
                .Execute(UpdateDurationLabels)
                .Every((long)(DURATION_UPDATE_INTERVAL * 1000));
        }

        /// <summary>
        /// 停止时间更新调度器
        /// </summary>
        private void StopDurationUpdateScheduler()
        {
            mDurationUpdateScheduler?.Pause();
            mDurationUpdateScheduler = default;
        }

        /// <summary>
        /// 更新所有卡片的时间标签
        /// </summary>
        private void UpdateDurationLabels()
        {
            if (mActiveObjectsScrollView == default || !IsPlaying) return;

            var now = Time.realtimeSinceStartup;
            
            for (int i = 0; i < mActiveObjectsScrollView.childCount; i++)
            {
                var card = mActiveObjectsScrollView[i];
                if (card.userData is not ActiveObjectInfo info) continue;

                var durationLabel = card.Q<Label>("duration");
                if (durationLabel == default) continue;

                var duration = now - info.SpawnTime;
                durationLabel.text = $"{duration:F1}s";

                // 更新颜色
                if (duration >= DANGER_THRESHOLD)
                    durationLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.BrandDanger);
                else if (duration >= WARNING_THRESHOLD)
                    durationLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.BrandWarning);
                else
                    durationLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextSecondary);
            }
        }

        #endregion
        // 卡片创建逻辑已移至 PoolKitToolPage.Card.cs
    }
}
#endif
