#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// PoolKitToolPage - HUD 概览面板
    /// 选择池后显示统计卡片，未选择时显示搜索框
    /// </summary>
    public partial class PoolKitToolPage
    {
        #region HUD 字段

        private VisualElement mHudCardRow;
        private VisualElement mHudEmptyState;
        private TextField mSearchField;
        private string mSearchFilter = string.Empty;

        #endregion

        /// <summary>
        /// 构建 HUD 区域
        /// </summary>
        private VisualElement BuildHudSection()
        {
            var section = new VisualElement();
            section.style.paddingTop = section.style.paddingBottom = 12;
            section.style.paddingLeft = section.style.paddingRight = 12;
            section.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerSection);
            section.style.borderBottomWidth = 1;
            section.style.borderBottomColor = new StyleColor(YokiFrameUIComponents.Colors.BorderLight);

            // 统计卡片行（选择池后显示）
            mHudCardRow = new VisualElement
            {
                name = "hud-card-row",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceAround,
                    display = DisplayStyle.None
                }
            };
            section.Add(mHudCardRow);

            // 四个指标卡片
            var totalResult = YokiFrameUIComponents.CreateHudCard("总数", "0", YokiFrameUIComponents.Colors.TextSecondary);
            mTotalLabel = totalResult.valueLabel;
            mHudCardRow.Add(totalResult.card);

            var activeResult = YokiFrameUIComponents.CreateHudCard("使用中", "0", YokiFrameUIComponents.Colors.BrandWarning);
            mActiveLabel = activeResult.valueLabel;
            mHudCardRow.Add(activeResult.card);

            var inactiveResult = YokiFrameUIComponents.CreateHudCard("池内", "0", YokiFrameUIComponents.Colors.BrandSuccess);
            mInactiveLabel = inactiveResult.valueLabel;
            mHudCardRow.Add(inactiveResult.card);

            var peakResult = YokiFrameUIComponents.CreateHudCard("峰值", "0", YokiFrameUIComponents.Colors.BrandPrimary);
            mPeakLabel = peakResult.valueLabel;
            mHudCardRow.Add(peakResult.card);

            // 空状态（未选择池时显示搜索框）
            mHudEmptyState = BuildHudEmptyState();
            section.Add(mHudEmptyState);

            return section;
        }

        /// <summary>
        /// 构建 HUD 空状态（搜索框 + 提示）
        /// </summary>
        private VisualElement BuildHudEmptyState()
        {
            var container = new VisualElement
            {
                name = "hud-empty-state",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    height = 48
                }
            };

            // 搜索图标
            var searchIcon = new Image { image = KitIcons.GetTexture(KitIcons.TARGET) };
            searchIcon.style.width = 16;
            searchIcon.style.height = 16;
            searchIcon.style.marginRight = 8;
            searchIcon.tintColor = YokiFrameUIComponents.Colors.TextTertiary;
            container.Add(searchIcon);

            // 搜索框
            const string placeholder = "搜索活跃对象...";
            mSearchField = new TextField
            {
                value = placeholder,
                style =
                {
                    width = 200,
                    height = 28
                }
            };
            mSearchField.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
            
            mSearchField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (mSearchField.value == placeholder)
                {
                    mSearchField.SetValueWithoutNotify(string.Empty);
                    mSearchField.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
                }
            });
            mSearchField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (string.IsNullOrEmpty(mSearchField.value))
                {
                    mSearchField.SetValueWithoutNotify(placeholder);
                    mSearchField.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary);
                }
            });
            mSearchField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != placeholder)
                {
                    mSearchFilter = evt.newValue;
                    UpdateActiveObjectsList();
                }
            });
            container.Add(mSearchField);

            // 提示文字
            var hint = new Label("选择左侧对象池查看详情")
            {
                style =
                {
                    fontSize = 12,
                    color = new StyleColor(YokiFrameUIComponents.Colors.TextTertiary),
                    marginLeft = 16
                }
            };
            container.Add(hint);

            return container;
        }

        /// <summary>
        /// 更新 HUD 区域显示状态
        /// </summary>
        private void UpdateHudSection()
        {
            if (mSelectedPool == default)
            {
                // 未选择池：显示空状态
                mHudCardRow.style.display = DisplayStyle.None;
                mHudEmptyState.style.display = DisplayStyle.Flex;
                return;
            }

            // 已选择池：显示统计卡片
            mHudCardRow.style.display = DisplayStyle.Flex;
            mHudEmptyState.style.display = DisplayStyle.None;

            if (mTotalLabel == default) return;

            mTotalLabel.text = mSelectedPool.TotalCount.ToString();
            mActiveLabel.text = mSelectedPool.ActiveCount.ToString();
            mInactiveLabel.text = mSelectedPool.InactiveCount.ToString();
            mPeakLabel.text = mSelectedPool.PeakCount.ToString();

            // Active 高亮警告（使用率 > 80%）
            var activeColor = mSelectedPool.UsageRate > 0.8f
                ? YokiFrameUIComponents.Colors.BrandDanger
                : YokiFrameUIComponents.Colors.BrandWarning;
            mActiveLabel.style.color = new StyleColor(activeColor);
        }
    }
}
#endif
