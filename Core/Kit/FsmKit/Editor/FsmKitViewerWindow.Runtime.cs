#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// FsmKit 状态机可视化查看器 - 运行时视图
    /// </summary>
    public partial class FsmKitViewerWindow
    {
        #region 运行时视图构建

        /// <summary>
        /// 构建运行时视图
        /// </summary>
        private void BuildRuntimeView()
        {
            mRuntimeView = new VisualElement();
            mRuntimeView.style.flexGrow = 1;
            mRuntimeView.style.flexDirection = FlexDirection.Row;
            mRuntimeView.style.paddingTop = 8;
            mRuntimeView.style.paddingBottom = 8;
            mRuntimeView.style.paddingLeft = 8;
            mRuntimeView.style.paddingRight = 8;
            mRuntimeView.style.display = DisplayStyle.None;
            mContentContainer.Add(mRuntimeView);

            BuildFsmListPanel();
            BuildFsmDetailPanel();
        }

        /// <summary>
        /// 构建状态机列表面板
        /// </summary>
        private void BuildFsmListPanel()
        {
            var panel = new VisualElement();
            panel.style.width = 250;
            panel.style.marginRight = 8;
            panel.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;
            mRuntimeView.Add(panel);

            // 标题
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            panel.Add(header);

            var titleLabel = new Label("活跃状态机");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            header.Add(titleLabel);

            mFsmCountLabel = new Label("(0)");
            mFsmCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            header.Add(mFsmCountLabel);

            // 状态机列表
            mFsmListView = new ListView();
            mFsmListView.fixedItemHeight = 28;
            mFsmListView.makeItem = MakeFsmItem;
            mFsmListView.bindItem = BindFsmItem;
#if UNITY_2022_1_OR_NEWER
            mFsmListView.selectionChanged += OnFsmSelected;
#else
            mFsmListView.onSelectionChange += OnFsmSelected;
#endif
            mFsmListView.style.flexGrow = 1;
            panel.Add(mFsmListView);
        }

        /// <summary>
        /// 构建状态机详情面板
        /// </summary>
        private void BuildFsmDetailPanel()
        {
            var panel = new VisualElement();
            panel.style.flexGrow = 1;
            panel.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            panel.style.borderTopLeftRadius = 4;
            panel.style.borderTopRightRadius = 4;
            panel.style.borderBottomLeftRadius = 4;
            panel.style.borderBottomRightRadius = 4;
            mRuntimeView.Add(panel);

            // 标题
            var header = new VisualElement();
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            panel.Add(header);

            var titleLabel = new Label("状态机详情");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            header.Add(titleLabel);

            // 详情容器
            mFsmDetailContainer = new ScrollView(ScrollViewMode.Vertical);
            mFsmDetailContainer.style.flexGrow = 1;
            mFsmDetailContainer.style.paddingTop = 8;
            mFsmDetailContainer.style.paddingBottom = 8;
            mFsmDetailContainer.style.paddingLeft = 12;
            mFsmDetailContainer.style.paddingRight = 12;
            panel.Add(mFsmDetailContainer);

            // 默认提示
            var hint = new Label("选择左侧状态机查看详情");
            hint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = 50;
            mFsmDetailContainer.Add(hint);
        }

        #endregion

        #region 状态机列表项

        /// <summary>
        /// 创建状态机列表项
        /// </summary>
        private VisualElement MakeFsmItem()
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = 28;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;

            // 状态指示器
            var indicator = new VisualElement();
            indicator.name = "indicator";
            indicator.style.width = 8;
            indicator.style.height = 8;
            indicator.style.borderTopLeftRadius = 4;
            indicator.style.borderTopRightRadius = 4;
            indicator.style.borderBottomLeftRadius = 4;
            indicator.style.borderBottomRightRadius = 4;
            indicator.style.marginRight = 8;
            item.Add(indicator);

            // 名称
            var nameLabel = new Label();
            nameLabel.name = "name";
            nameLabel.style.flexGrow = 1;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(nameLabel);

            // 状态数量
            var countLabel = new Label();
            countLabel.name = "count";
            countLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            item.Add(countLabel);

            return item;
        }

        /// <summary>
        /// 绑定状态机列表项数据
        /// </summary>
        private void BindFsmItem(VisualElement element, int index)
        {
            var fsm = mCachedFsms[index];

            var indicator = element.Q<VisualElement>("indicator");
            var stateColor = fsm.MachineState switch
            {
                MachineState.Running => new Color(0.4f, 0.8f, 0.4f),
                MachineState.Suspend => new Color(0.9f, 0.9f, 0.3f),
                _ => new Color(0.5f, 0.5f, 0.5f)
            };
            indicator.style.backgroundColor = new StyleColor(stateColor);

            element.Q<Label>("name").text = fsm.Name;
            element.Q<Label>("count").text = $"[{fsm.GetAllStates().Count}]";
        }

        /// <summary>
        /// 状态机选中回调
        /// </summary>
        private void OnFsmSelected(IEnumerable<object> selection)
        {
            foreach (var item in selection)
            {
                if (item is IFSM fsm)
                {
                    mSelectedFsm = fsm;
                    RefreshFsmDetail();
                    return;
                }
            }
        }

        #endregion

        #region 运行时视图刷新

        /// <summary>
        /// 刷新运行时视图
        /// </summary>
        private void RefreshRuntimeView()
        {
            if (mFsmListView == null) return;

            if (!EditorApplication.isPlaying)
            {
                mFsmDetailContainer.Clear();
                var hint = new Label("请进入 Play Mode 查看运行时状态机");
                hint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                hint.style.marginTop = 50;
                mFsmDetailContainer.Add(hint);
                return;
            }

            RefreshFsmList();
        }

        /// <summary>
        /// 刷新状态机列表
        /// </summary>
        private void RefreshFsmList()
        {
            if (mFsmListView == null) return;

            mFsmCountLabel.text = $"({mCachedFsms.Count})";
            mFsmListView.itemsSource = mCachedFsms;
            mFsmListView.RefreshItems();

            if (mSelectedFsm != null)
                RefreshFsmDetail();
        }

        /// <summary>
        /// 刷新状态机详情
        /// </summary>
        private void RefreshFsmDetail()
        {
            mFsmDetailContainer.Clear();

            if (mSelectedFsm == null)
            {
                var hint = new Label("选择左侧状态机查看详情");
                hint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                hint.style.unityTextAlign = TextAnchor.MiddleCenter;
                hint.style.marginTop = 50;
                mFsmDetailContainer.Add(hint);
                return;
            }

            // 基本信息区块
            var infoSection = CreateInfoSection();
            mFsmDetailContainer.Add(infoSection);

            // 状态列表区块
            var statesSection = CreateStatesSection();
            mFsmDetailContainer.Add(statesSection);
        }

        #endregion

        #region 详情区块创建

        /// <summary>
        /// 创建基本信息区块
        /// </summary>
        private VisualElement CreateInfoSection()
        {
            var section = new VisualElement();
            section.style.marginBottom = 12;
            section.style.paddingTop = 8;
            section.style.paddingBottom = 8;
            section.style.paddingLeft = 12;
            section.style.paddingRight = 12;
            section.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            section.style.borderTopLeftRadius = 4;
            section.style.borderTopRightRadius = 4;
            section.style.borderBottomLeftRadius = 4;
            section.style.borderBottomRightRadius = 4;

            AddInfoRow(section, "枚举类型:", mSelectedFsm.EnumType.Name);
            AddInfoRow(section, "机器状态:", mSelectedFsm.MachineState.ToString());

            var currentStateName = mSelectedFsm.CurrentStateId >= 0
                ? Enum.GetName(mSelectedFsm.EnumType, mSelectedFsm.CurrentStateId) ?? mSelectedFsm.CurrentStateId.ToString()
                : "None";
            AddInfoRow(section, "当前状态:", currentStateName, true);

            return section;
        }

        /// <summary>
        /// 添加信息行
        /// </summary>
        private void AddInfoRow(VisualElement parent, string label, string value, bool bold = false)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4;

            var labelElement = new Label(label);
            labelElement.style.width = 80;
            labelElement.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            row.Add(labelElement);

            var valueElement = new Label(value);
            if (bold)
                valueElement.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueElement.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            row.Add(valueElement);

            parent.Add(row);
        }

        /// <summary>
        /// 创建状态列表区块
        /// </summary>
        private VisualElement CreateStatesSection()
        {
            var section = new VisualElement();

            var titleLabel = new Label("注册状态:");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 8;
            titleLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            section.Add(titleLabel);

            var states = mSelectedFsm.GetAllStates();
            var currentId = mSelectedFsm.CurrentStateId;

            foreach (var kvp in states)
            {
                var stateItem = CreateStateItem(kvp.Key, kvp.Value, kvp.Key == currentId);
                section.Add(stateItem);
            }

            return section;
        }

        /// <summary>
        /// 创建状态项
        /// </summary>
        private VisualElement CreateStateItem(int stateId, IState state, bool isCurrent)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = 28;
            item.style.marginBottom = 4;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;
            item.style.backgroundColor = new StyleColor(isCurrent
                ? new Color(0.3f, 0.5f, 0.3f)
                : new Color(0.15f, 0.15f, 0.15f));
            item.style.borderTopLeftRadius = 4;
            item.style.borderTopRightRadius = 4;
            item.style.borderBottomLeftRadius = 4;
            item.style.borderBottomRightRadius = 4;

            // 当前状态指示
            var indicator = new Label(isCurrent ? "▶" : "");
            indicator.style.width = 18;
            indicator.style.color = new StyleColor(new Color(0.4f, 0.9f, 0.4f));
            item.Add(indicator);

            // 状态名称
            var stateName = Enum.GetName(mSelectedFsm.EnumType, stateId) ?? stateId.ToString();
            var nameLabel = new Label(stateName);
            nameLabel.style.width = 120;
            if (isCurrent)
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
            item.Add(nameLabel);

            // 状态类型
            var typeLabel = new Label(state.GetType().Name);
            typeLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            typeLabel.style.fontSize = 11;
            item.Add(typeLabel);

            return item;
        }

        #endregion
    }
}
#endif
